using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.AzureServiceBus;

/// <summary>
/// Idempotent topic / subscription / rule provisioning. Every call swallows "already exists", so concurrent instances
/// racing at startup all succeed and an existing deployment's entity settings are never rewritten.
/// </summary>
internal sealed partial class AzureServiceBusTopology(AzureServiceBusOptions options, ILogger<AzureServiceBusTopology> logger)
{
    /// <summary>Provision the topic, and for each endpoint its subscription plus one correlation rule per routing key.</summary>
    public async ValueTask ProvisionAsync(IReadOnlyList<EndpointTopology> endpoints, CancellationToken cancellationToken)
    {
        var opt = options;
        var admin = new ServiceBusAdministrationClient(opt.ConnectionString);
        var topic = AzureServiceBusEntityNameMapper.Sanitize(opt.Topic, AzureServiceBusEntityNameMapper.MaxTopicLength);

        await CreateTopicAsync(admin, opt, topic, cancellationToken);

        foreach (var endpoint in endpoints)
        {
            var subscription = AzureServiceBusEntityNameMapper.Sanitize(endpoint.Queue, AzureServiceBusEntityNameMapper.MaxSubscriptionLength);
            await CreateSubscriptionAsync(admin, opt, topic, subscription, cancellationToken);
            await SyncRulesAsync(admin, opt, topic, subscription, endpoint.RoutingKeys, cancellationToken);
        }
    }

    private static async ValueTask CreateTopicAsync(ServiceBusAdministrationClient admin, AzureServiceBusOptions opt, string topic, CancellationToken cancellationToken)
    {
        var createOptions = new CreateTopicOptions(topic)
        {
            // Duplicate detection is immutable after creation, so this governs only a topic created here.
            RequiresDuplicateDetection = opt.EnableDuplicateDetection,
        };

        if (opt.EnableDuplicateDetection)
            createOptions.DuplicateDetectionHistoryTimeWindow = opt.DuplicateDetectionWindow;

        await TryCreateAsync(async () => await admin.CreateTopicAsync(createOptions, cancellationToken));
    }

    private static async ValueTask CreateSubscriptionAsync(ServiceBusAdministrationClient admin, AzureServiceBusOptions opt, string topic, string subscription, CancellationToken cancellationToken)
    {
        var createOptions = new CreateSubscriptionOptions(topic, subscription)
        {
            RequiresSession = opt.RequiresSession,
            MaxDeliveryCount = opt.MaxDeliveryCount,
            LockDuration = opt.LockDuration,

            // A message that outlives its TTL goes to the dead-letter queue instead of vanishing.
            DeadLetteringOnMessageExpiration = true,
        };

        // A false filter first: one that briefly matched everything pulls in other services' messages before the sync.
        var seedRule = new CreateRuleOptions(RuleProperties.DefaultRuleName, new FalseRuleFilter());
        await TryCreateAsync(async () => await admin.CreateSubscriptionAsync(createOptions, seedRule, cancellationToken));
    }

    /// <summary>
    /// Ensure exactly one correlation rule per routing key. A correlation filter on <c>Subject</c> is the Service Bus
    /// analogue of a RabbitMQ topic binding — the broker evaluates it against an indexed property, so it is the
    /// cheapest filter kind, and it keeps the send path's routing key the single source of what a subscriber receives.
    /// </summary>
    private async ValueTask SyncRulesAsync(
        ServiceBusAdministrationClient admin,
        AzureServiceBusOptions opt,
        string topic,
        string subscription,
        IReadOnlyList<string> routingKeys,
        CancellationToken cancellationToken)
    {
        foreach (var routingKey in routingKeys)
        {
            var ruleName = AzureServiceBusEntityNameMapper.Sanitize(routingKey, AzureServiceBusEntityNameMapper.MaxSubscriptionLength);

            // Skip a key sanitizing to $Default: it would replace the seed's false filter and leave this undiagnosable.
            if (string.Equals(ruleName, RuleProperties.DefaultRuleName, StringComparison.OrdinalIgnoreCase))
                continue;

            var rule = new CreateRuleOptions(ruleName, new CorrelationRuleFilter(routingKey));
            await TryCreateAsync(async () => await admin.CreateRuleAsync(topic, subscription, rule, cancellationToken));
        }

        if (!opt.RemoveCatchAllRule)
            return;

        // Best-effort: an externally provisioned subscription's true-filter $Default rule matches every topic message.
        try
        {
            await admin.DeleteRuleAsync(topic, subscription, RuleProperties.DefaultRuleName, cancellationToken);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            // Already removed, or the seed rule this adapter created was never a catch-all to begin with.
        }
        catch (ServiceBusException ex)
        {
            LogCatchAllRuleRemovalFailed(subscription, ex);
        }
    }

    /// <summary>Run a management create, treating "already exists" as success — the entity is provisioned, which is all the caller asked for.</summary>
    private static async ValueTask TryCreateAsync(Func<Task> create)
    {
        try
        {
            await create();
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            // Provisioned by an earlier start or by another instance racing this one.
        }
    }

    [LoggerMessage(EventId = 6710, Level = LogLevel.Warning, Message = "Removing the $Default catch-all rule from Service Bus subscription {Subscription} failed; it may still receive message types this service does not handle")]
    private partial void LogCatchAllRuleRemovalFailed(string subscription, Exception exception);
}
