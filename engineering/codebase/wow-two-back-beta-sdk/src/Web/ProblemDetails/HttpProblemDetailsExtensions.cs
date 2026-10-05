using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling;

namespace WoW.Two.Sdk.Backend.Beta.Web.ProblemDetails;

/// <summary>Composes the opt-in HTTP ProblemDetails boundary without changing success response contracts.</summary>
public static class HttpProblemDetailsExtensions
{
    /// <summary>Registers trace-aware SDK exception handling and MVC error-result normalization.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddHttpProblemDetails(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(HttpProblemDetailsRegistration))) return services;
        services.AddSingleton<HttpProblemDetailsRegistration>();
        services.AddSingleton<IPostConfigureOptions<ProblemDetailsOptions>>(provider =>
            provider.GetRequiredService<HttpProblemDetailsRegistration>());
        services.Configure<HostFilteringOptions>(options => options.IncludeFailureMessage = false);
        services.Insert(0, ServiceDescriptor.Transient<IStartupFilter, HttpProblemDetailsStartupFilter>());
        services.Insert(0, ServiceDescriptor.Singleton<IProblemDetailsWriter, HttpProblemDetailsWriter>());
        services.AddTraceAwareProblemDetails();
        services.Insert(0, ServiceDescriptor.Singleton<IExceptionHandler, HttpRequestProblemExceptionHandler>());
        services.AddAppExceptionHandling();
        services.Configure<MvcOptions>(options => options.Filters.Add<HttpProblemDetailsResultFilter>());
        return services;
    }

    /// <summary>Handles exceptions and empty error statuses, including clients whose Accept header excludes JSON.</summary>
    /// <param name="app">The application pipeline, before product middleware.</param>
    /// <returns>The same application builder for chaining.</returns>
    /// <remarks>Started responses and disconnected requests remain owned by the transport; HEAD receives no body.</remarks>
    public static IApplicationBuilder UseHttpProblemDetails(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        const string marker = "WoW.Two.Sdk.Backend.Beta.HttpProblemDetails";
        if (app.Properties.ContainsKey(marker)) return app;
        app.Properties[marker] = true;
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                if (context.Response.StatusCode is >= 400 and <= 599)
                    context.Response.Headers.CacheControl = "no-store";
                return Task.CompletedTask;
            });
            await next(context).ConfigureAwait(false);
        });
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            AllowStatusCode404Response = true,
            ExceptionHandler = HttpProblemDetailsResponse.WriteExceptionAsync,
            StatusCodeSelector = exception => exception is BadHttpRequestException request
                ? request.StatusCode : StatusCodes.Status500InternalServerError,
        });
        app.UseStatusCodePages(context => HttpProblemDetailsResponse.WriteAsync(
            context.HttpContext, new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = context.HttpContext.Response.StatusCode,
            }));
        return app;
    }
}
