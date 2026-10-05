using AwesomeAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Ai.Ollama;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Mcp;

/// <summary>Verifies AI provider registrations against the dependency generation MCP requires.</summary>
public sealed class AiRegistrationTests
{
    [Fact]
    public void Ollama_ResolvesChatAndEmbeddingClientsWithoutContactingTheProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOllamaChatClient("http://localhost:11434", "test-model");
        services.AddOllamaEmbeddingGenerator(new Uri("http://localhost:11434"), "test-model");
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        provider.GetRequiredService<IChatClient>().Should().NotBeNull();
        provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>().Should().NotBeNull();
    }
}
