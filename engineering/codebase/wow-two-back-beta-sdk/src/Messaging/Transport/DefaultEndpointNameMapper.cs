using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Default formatter — kebab-cases the message type's simple name and joins it to an optional prefix with <c>.</c>:
/// <c>OrderPlaced</c> under prefix <c>wt.events</c> becomes <c>wt.events.order-placed</c>, dead-lettering to
/// <c>wt.events.order-placed.dlq</c>. Generic arguments are appended (<c>Envelope&lt;Order&gt;</c> → <c>envelope-order</c>).
/// The simple name is used, not the full name: queue names stay readable, at the cost of collapsing two same-named
/// types from different namespaces onto one endpoint. Register a replacement when that matters.
/// </summary>
public sealed class DefaultEndpointNameMapper : IEndpointNameMapper
{
    private readonly string? _prefix;
    private readonly string _deadLetterSuffix;

    /// <summary>Create the formatter.</summary>
    /// <param name="prefix">Dotted prefix put in front of every endpoint name (e.g. <c>wt.events</c>); null or empty for none.</param>
    /// <param name="deadLetterSuffix">Segment appended to an endpoint name to form its dead-letter queue. Default <c>dlq</c>.</param>
    public DefaultEndpointNameMapper(string? prefix = null, string deadLetterSuffix = "dlq")
    {
        ArgumentException.ThrowIfNullOrEmpty(deadLetterSuffix);
        _prefix = prefix;
        _deadLetterSuffix = deadLetterSuffix;
    }

    /// <inheritdoc />
    public string Endpoint(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        var builder = new StringBuilder();
        AppendTypeName(builder, messageType);
        var name = builder.ToString();

        return string.IsNullOrEmpty(_prefix) ? name : string.Concat(_prefix, ".", name);
    }

    /// <inheritdoc />
    public string DeadLetter(string endpointName)
    {
        ArgumentException.ThrowIfNullOrEmpty(endpointName);
        return string.Concat(endpointName, ".", _deadLetterSuffix);
    }

    private static void AppendTypeName(StringBuilder builder, Type type)
    {
        var name = type.Name;

        // Arity marker on a generic type name (`Envelope`1`); the arguments are appended below in readable form.
        var arity = name.IndexOf('`');
        if (arity >= 0)
            name = name[..arity];

        AppendKebabCase(builder, name);

        if (!type.IsGenericType)
            return;

        foreach (var argument in type.GetGenericArguments())
        {
            AppendSeparator(builder);
            AppendTypeName(builder, argument);
        }
    }

    private static void AppendKebabCase(StringBuilder builder, string name)
    {
        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];
            if (!char.IsLetterOrDigit(character))
            {
                AppendSeparator(builder);
                continue;
            }

            // A word starts after a lower-case letter or a digit (orderPlaced), or at an acronym's last capital (HTTPRequest).
            var startsWord = char.IsUpper(character)
                && index > 0
                && (!char.IsUpper(name[index - 1]) || (index + 1 < name.Length && char.IsLower(name[index + 1])));

            if (startsWord)
                AppendSeparator(builder);

            builder.Append(char.ToLowerInvariant(character));
        }
    }

    private static void AppendSeparator(StringBuilder builder)
    {
        // Never leads, never doubles — a name is a sequence of words, not of separators.
        if (builder.Length > 0 && builder[^1] is not ('-' or '.'))
            builder.Append('-');
    }
}
