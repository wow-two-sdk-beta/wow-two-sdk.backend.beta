using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Web.Captcha;

/// <summary>Registers captcha checks and gates endpoints on them.</summary>
public static class CaptchaServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ICaptchaValidator"/> over the siteverify broker. Checks stay off until
    /// <see cref="CaptchaOptions.Enabled"/>; options come from <paramref name="configure"/>, then the host section
    /// <c>Web:Captcha</c>. An enabled check needs a secret.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Provider, secret and rules.</param>
    public static IServiceCollection AddCaptcha(this IServiceCollection services, Action<CaptchaOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddModuleOptions(
            CaptchaOptions.SectionName,
            configure,
            builder => builder
                .Validate(o => !o.Enabled || !string.IsNullOrWhiteSpace(o.Secret), "CaptchaOptions.Secret is required once captcha checks are enabled.")
                .Validate(o => o.MinimumScore is >= 0 and <= 1, "CaptchaOptions.MinimumScore must be between 0 and 1."));
        services.AddHttpClient(SiteVerifyCaptchaBroker.HttpClientName);
        services.TryAddSingleton<ICaptchaBroker, SiteVerifyCaptchaBroker>();
        services.TryAddSingleton<ICaptchaValidator, CaptchaValidator>();
        return services;
    }

    /// <summary>
    /// Answers <c>400</c> before the handler unless the request carries a passing captcha token (header or form field);
    /// <c>503</c> while the provider is down, unless it fails open. Inert while checks are off.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint or group builder.</typeparam>
    /// <param name="builder">The endpoint, such as the sign-up route.</param>
    /// <param name="action">The widget action the token must carry, such as <c>signup</c>; null accepts any.</param>
    public static TBuilder RequireCaptcha<TBuilder>(this TBuilder builder, string? action = null)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var result = await http.RequestServices.GetRequiredService<ICaptchaValidator>().ValidateAsync(http, action, http.RequestAborted);
            if (result.Passed)
                return await next(context);

            var metadata = new Dictionary<string, object?>(StringComparer.Ordinal) { ["messageKey"] = "CaptchaInvalid" };
            throw (result.Failure == CaptchaFailure.Unavailable
                ? AppError.Of(AppErrorType.ExternalUnavailable, "The captcha check is unavailable; try again shortly.", metadata)
                : AppError.Of(AppErrorType.Validation, "Complete the captcha check and try again.", metadata)).ToException();
        });
    }
}
