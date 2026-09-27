using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Web.Antiforgery;

/// <summary>Provides antiforgery protection for single-page apps that authenticate with cookies.</summary>
public static class SpaAntiforgeryServiceCollectionExtensions
{
    /// <summary>Registers antiforgery with the header a single-page app echoes its token in.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Sets the token cookie, header and exempt paths.</param>
    /// <remarks>Pair with <see cref="UseSpaAntiforgery"/> after authentication, so tokens bind to the signed-in user.</remarks>
    public static IServiceCollection AddSpaAntiforgery(
        this IServiceCollection services,
        Action<SpaAntiforgeryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = new SpaAntiforgeryOptions();
        configure?.Invoke(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.CookieName, nameof(configure));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.HeaderName, nameof(configure));

        services.AddSingleton(options);
        services.AddAntiforgery(antiforgery =>
        {
            antiforgery.HeaderName = options.HeaderName;
            antiforgery.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            antiforgery.Cookie.SameSite = SameSiteMode.Strict;
        });
        return services;
    }

    /// <summary>Issues a readable token on safe requests and rejects cookie-carrying unsafe requests without a valid echo.</summary>
    /// <param name="app">The application request pipeline, after authentication.</param>
    /// <remarks>
    ///   - a request without cookies cannot be forged by a browser, so bearer and server calls pass unchanged
    ///   - a rejected request gets a 400 problem with code <c>AntiforgeryValidationFailed</c>
    /// </remarks>
    public static IApplicationBuilder UseSpaAntiforgery(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.Use(async (context, next) =>
        {
            var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
            var options = context.RequestServices.GetRequiredService<SpaAntiforgeryOptions>();
            if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)
                || HttpMethods.IsOptions(context.Request.Method) || HttpMethods.IsTrace(context.Request.Method))
            {
                AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(context);
                context.Response.Cookies.Append(options.CookieName, tokens.RequestToken!, new CookieOptions
                {
                    HttpOnly = false,
                    Secure = context.Request.IsHttps,
                    SameSite = SameSiteMode.Strict,
                    Path = "/",
                });
            }
            else if (context.Request.Cookies.Count > 0
                && !options.ExemptPathPrefixes.Any(prefix => context.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
                && !await antiforgery.IsRequestValidAsync(context).ConfigureAwait(false))
            {
                await Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "The request is missing a valid antiforgery token.",
                        extensions: new Dictionary<string, object?> { ["code"] = "AntiforgeryValidationFailed" })
                    .ExecuteAsync(context)
                    .ConfigureAwait(false);
                return;
            }

            await next(context).ConfigureAwait(false);
        });
    }
}
