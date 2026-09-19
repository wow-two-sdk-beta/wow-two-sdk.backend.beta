using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;
using WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;
using WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling.Factories;

namespace WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling;

/// <summary>Provides registration for MVC exception filters that map thrown exceptions to ProblemDetails responses.</summary>
public static class ExceptionHandlingMvcBuilderExtensions
{
    /// <summary>Adds the <see cref="ValidationExceptionFilter"/> mapping a <see cref="ValidationException"/> to a 400 ProblemDetails for MVC controllers.</summary>
    /// <param name="builder">The MVC builder to configure.</param>
    public static IMvcBuilder AddValidationExceptionFilter(this IMvcBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddErrorHttpStatusMapping();
        builder.Services.TryAddSingleton<IAppErrorProblemDetailsFactory, AppErrorProblemDetailsFactory>();
        builder.AddMvcOptions(options => options.Filters.Add<ValidationExceptionFilter>());
        return builder;
    }
}
