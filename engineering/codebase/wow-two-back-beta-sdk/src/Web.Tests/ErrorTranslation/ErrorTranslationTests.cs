using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;
using WoW.Two.Sdk.Backend.Beta.Identity.Core;
using WoW.Two.Sdk.Backend.Beta.Web.Contracts;
using WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;
using WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling.Factories;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.ErrorTranslation;

/// <summary>Error translation is switched purely by configuration and renders through the default mappers.</summary>
public sealed class ErrorTranslationTests
{
    private const string EnglishNotEmpty = "'Email' must not be empty.";

    private static readonly FieldError EmailRequired = new()
    {
        Property = "email",
        Code = "NotEmptyValidator",
        Message = EnglishNotEmpty,
        Params = new Dictionary<string, object> { ["PropertyName"] = "Email" },
    };

    [Fact]
    public void WithoutConfiguration_MessagesStayAsAuthored()
    {
        using var provider = Build(new Dictionary<string, string?>());

        var problem = Render(provider, ValidationError.From([EmailRequired]), "ru");

        problem.Detail.Should().Be("One or more validation errors occurred.");
        Errors(problem).Single().Message.Should().Be(EnglishNotEmpty);
    }

    [Fact]
    public void Enabled_TranslatesTypeTextsAndValidatorMessages()
    {
        using var provider = Build(Enabled("en", "ru", "uz"));

        var validation = Render(provider, ValidationError.From([EmailRequired]), "ru-RU,ru;q=0.9,en;q=0.8");
        validation.Detail.Should().Be("Одно или несколько полей заполнены неверно.");
        Errors(validation).Single().Message.Should().Contain("Email").And.NotBe(EnglishNotEmpty);

        Render(provider, AppErrorFactory.NotFound("Order 42 was not found."), "ru").Detail.Should().Be("Запрошенный ресурс не найден.");
        Render(provider, AppErrorFactory.NotFound("Order 42 was not found."), "uz").Detail.Should().Be("Soʻralgan resurs topilmadi.");

        var uzbek = Errors(Render(provider, ValidationError.From([EmailRequired]), "uz")).Single().Message;
        uzbek.Should().Contain("Email").And.NotBe(EnglishNotEmpty);
    }

    [Fact]
    public void Catalog_OverridesBuiltInsAndFillsPlaceholders()
    {
        var settings = Enabled("en", "ru");
        settings["ErrorTranslation:Messages:ru:OrderMissing"] = "Заказ {orderId} не найден.";
        settings["ErrorTranslation:Messages:ru:NotEmptyValidator"] = "Заполните поле «{PropertyName}».";
        settings["ErrorTranslation:Messages:ru:OrderLimit"] = "Не больше {Max} заказов, у вас {Count}.";
        settings["ErrorTranslation:Messages:ru:Broken"] = "Нет значения {Unknown}.";
        using var provider = Build(settings);

        var keyed = AppError.Of(AppErrorType.NotFound, "Order 42 was not found.", new Dictionary<string, object?> { ["messageKey"] = "OrderMissing", ["orderId"] = 42 });
        Render(provider, keyed, "ru").Detail.Should().Be("Заказ 42 не найден.");

        var limit = new FieldError { Property = "orders", Code = "OrderLimit", Message = "Too many orders.", Params = new Dictionary<string, object> { ["Max"] = 3, ["Count"] = 5 } };
        var broken = new FieldError { Property = "x", Code = "Broken", Message = "Authored.", Params = new Dictionary<string, object>() };
        Errors(Render(provider, ValidationError.From([EmailRequired, limit, broken]), "ru")).Select(e => e.Message).Should().Equal(
            "Заполните поле «Email».",
            "Не больше 3 заказов, у вас 5.",
            "Authored.");
    }

    [Fact]
    public void CultureSelection_HonoursDefaultSupportedAndTheLocalizationFeature()
    {
        using var provider = Build(Enabled("en", "ru"));
        var notFound = AppErrorFactory.NotFound("Order 42 was not found.");

        Render(provider, notFound, "en-US").Detail.Should().Be("Order 42 was not found.");
        Render(provider, notFound, "de").Detail.Should().Be("Order 42 was not found.");
        Render(provider, notFound, "de, ru;q=0.5").Detail.Should().Be("Запрошенный ресурс не найден.");
        Render(provider, notFound, "en", feature: "ru").Detail.Should().Be("Запрошенный ресурс не найден.");
    }

    [Fact]
    public void ConfigurationReload_SwitchesTranslationLive()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        using var provider = Build(configuration);
        var notFound = AppErrorFactory.NotFound("Order 42 was not found.");
        Render(provider, notFound, "ru").Detail.Should().Be("Order 42 was not found.");

        configuration["ErrorTranslation:Enabled"] = "true";
        configuration.Reload();

        Render(provider, notFound, "ru").Detail.Should().Be("Запрошенный ресурс не найден.");
    }

    [Fact]
    public void PseudoLocalization_MarksEveryMessageIncludingAuthoredOnes()
    {
        var settings = Enabled("en", "ru");
        settings["ErrorTranslation:PseudoLocalization"] = "true";
        using var provider = Build(settings);

        Render(provider, AppErrorFactory.NotFound("Order 42 was not found."), "en").Detail.Should().Be("[!! Öŕđéŕ 42 ŵåš ñöţ ƒöûñđ. !!]");
        Render(provider, AppErrorFactory.NotFound("Order 42 was not found."), "ru").Detail.Should().StartWith("[!! Запрошенный");
    }

    [Fact]
    public void IdentityFailures_TranslateThroughBuiltInTextsWithTheirValues()
    {
        using var provider = Build(Enabled("en", "ru"));
        var result = IdentityResult.Failed(
            new IdentityError { Code = IdentityErrorCodeConstants.PasswordTooShort, Description = "Passwords must be at least 12 characters.", Params = new Dictionary<string, object> { ["MinLength"] = 12 } },
            new IdentityError { Code = IdentityErrorCodeConstants.DuplicateEmail, Description = "Email 'a@b.uz' is already taken.", Params = new Dictionary<string, object> { ["Email"] = "a@b.uz" } });

        var errors = Errors(Render(provider, result.ToValidationError(), "ru"));

        errors.Select(e => (e.Property, e.Message)).Should().Equal(
            ("password", "Пароль должен содержать не менее 12 символов."),
            ("email", "Email a@b.uz уже используется."));
        Errors(Render(provider, result.ToValidationError(), "en")).Select(e => e.Message)
            .Should().Equal("Passwords must be at least 12 characters.", "Email 'a@b.uz' is already taken.");
    }

    [Fact]
    public void ToApiFailure_CarriesTheMappedStatusAndTheTranslatedMessage()
    {
        using var provider = Build(Enabled("en", "ru"));
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Headers.AcceptLanguage = "ru";

        var failure = AppErrorFactory.NotFound("Order 42 was not found.").ToApiFailure<OrderDto>(context);

        failure.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        failure.Error.Should().Be("Запрошенный ресурс не найден.");
        AppErrorFactory.NotFound("Order 42 was not found.").ToApiFailure<OrderDto>(new DefaultHttpContext()).Error
            .Should().Be("Order 42 was not found.");
    }

    private sealed record OrderDto
    {
        public int Id { get; init; }
    }

    private static Dictionary<string, string?> Enabled(params string[] cultures)
    {
        var settings = new Dictionary<string, string?> { ["ErrorTranslation:Enabled"] = "true" };
        for (var index = 0; index < cultures.Length; index++)
            settings[$"ErrorTranslation:SupportedCultures:{index}"] = cultures[index];
        return settings;
    }

    private static ServiceProvider Build(Dictionary<string, string?> settings)
        => Build(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

    private static ServiceProvider Build(IConfigurationRoot configuration)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddErrorHttpStatusMapping();
        services.AddSingleton<IAppErrorProblemDetailsFactory, AppErrorProblemDetailsFactory>();
        return services.BuildServiceProvider();
    }

    private static Microsoft.AspNetCore.Mvc.ProblemDetails Render(ServiceProvider provider, AppError error, string acceptLanguage, string? feature = null)
    {
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Headers.AcceptLanguage = acceptLanguage;
        if (feature is not null)
            context.Features.Set<IRequestCultureFeature>(new RequestCultureFeature(new RequestCulture(feature), provider: null));

        return provider.GetRequiredService<IAppErrorProblemDetailsFactory>().Create(error, context);
    }

    private static IReadOnlyList<FieldError> Errors(Microsoft.AspNetCore.Mvc.ProblemDetails problem)
        => (IReadOnlyList<FieldError>)problem.Extensions["errors"]!;
}
