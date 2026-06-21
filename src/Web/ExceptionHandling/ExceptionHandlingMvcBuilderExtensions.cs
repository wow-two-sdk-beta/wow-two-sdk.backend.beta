using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

namespace WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling;

/// <summary>Provides registration for MVC exception filters that map thrown exceptions to ProblemDetails responses.</summary>
public static class ExceptionHandlingMvcBuilderExtensions
{
    /// <summary>Registers the <see cref="ValidationExceptionFilter"/> that maps a <see cref="ValidationException"/> to a 400 ProblemDetails response for MVC controllers.</summary>
    /// <param name="builder">The MVC builder.</param>
    /// <returns>The same MVC builder, for chaining.</returns>
    public static IMvcBuilder AddValidationExceptionFilter(this IMvcBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddMvcOptions(options => options.Filters.Add<ValidationExceptionFilter>());
        return builder;
    }
}
