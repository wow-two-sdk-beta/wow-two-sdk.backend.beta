using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

namespace WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling;

/// <summary>Provides registration for MVC exception filters that map thrown exceptions to ProblemDetails responses.</summary>
public static class ExceptionHandlingMvcBuilderExtensions
{
    /// <summary>Adds the <see cref="ValidationExceptionFilter"/> mapping a <see cref="ValidationException"/> to a 400 ProblemDetails for MVC controllers.</summary>
    /// <param name="builder">The MVC builder to configure.</param>
    public static IMvcBuilder AddValidationExceptionFilter(this IMvcBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddMvcOptions(options => options.Filters.Add<ValidationExceptionFilter>());
        return builder;
    }
}
