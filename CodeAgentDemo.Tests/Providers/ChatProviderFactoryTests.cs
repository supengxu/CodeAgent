using CodeAgentDemo.Providers;
using FluentAssertions;
using Xunit;

namespace CodeAgentDemo.Tests.Providers;

public class ChatProviderFactoryTests
{
    [Fact]
    public void Create_WithoutProviderEnvVar_ShouldThrowInvalidOperationException()
    {
        Environment.SetEnvironmentVariable("AI_PROVIDER", null);

        var act = () => ChatProviderFactory.Create();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AI_PROVIDER*required*");
    }

    [Fact]
    public void Create_WithUnknownProvider_ShouldThrowInvalidOperationException()
    {
        Environment.SetEnvironmentVariable("AI_PROVIDER", "unknown_provider");

        var act = () => ChatProviderFactory.Create();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unknown provider*");
    }

    [Fact]
    public void Create_WithAnthropicProvider_ShouldCreateAnthropicProvider()
    {
        Environment.SetEnvironmentVariable("AI_PROVIDER", "anthropic");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "test-api-key");
        Environment.SetEnvironmentVariable("ANTHROPIC_MODEL", "claude-sonnet-4");

        var provider = ChatProviderFactory.Create();

        provider.Should().BeOfType<AnthropicProvider>();
        provider.ProviderName.Should().Be("Anthropic");

        CleanupEnvironment();
    }

    [Fact]
    public void Create_WithOpenAIProvider_ShouldCreateOpenAIProvider()
    {
        Environment.SetEnvironmentVariable("AI_PROVIDER", "openai");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-api-key");
        Environment.SetEnvironmentVariable("OPENAI_MODEL", "gpt-4");

        var provider = ChatProviderFactory.Create();

        provider.Should().BeOfType<OpenAIProvider>();
        provider.ProviderName.Should().Be("OpenAI");

        CleanupEnvironment();
    }

    [Fact]
    public void Create_WithAnthropicWithoutApiKey_ShouldThrowInvalidOperationException()
    {
        Environment.SetEnvironmentVariable("AI_PROVIDER", "anthropic");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);

        var act = () => ChatProviderFactory.Create();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ANTHROPIC_API_KEY*");

        CleanupEnvironment();
    }

    [Fact]
    public void Create_WithOpenAIWithoutApiKey_ShouldThrowInvalidOperationException()
    {
        Environment.SetEnvironmentVariable("AI_PROVIDER", "openai");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);

        var act = () => ChatProviderFactory.Create();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*OPENAI_API_KEY*");

        CleanupEnvironment();
    }

    [Fact]
    public void Create_WithThinkingEnabled_ShouldPassThinkingParameters()
    {
        Environment.SetEnvironmentVariable("AI_PROVIDER", "anthropic");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "test-api-key");
        Environment.SetEnvironmentVariable("ENABLE_THINKING", "true");
        Environment.SetEnvironmentVariable("THINKING_BUDGET_TOKENS", "5000");

        var provider = ChatProviderFactory.Create();

        provider.Should().BeOfType<AnthropicProvider>();

        CleanupEnvironment();
    }

    [Fact]
    public void Create_WithCustomEndpoint_ShouldPassEndpoint()
    {
        Environment.SetEnvironmentVariable("AI_PROVIDER", "openai");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-api-key");
        Environment.SetEnvironmentVariable("OPENAI_API_URL", "https://custom.api/v1");

        var provider = ChatProviderFactory.Create();

        provider.Should().BeOfType<OpenAIProvider>();

        CleanupEnvironment();
    }

    private static void CleanupEnvironment()
    {
        Environment.SetEnvironmentVariable("AI_PROVIDER", null);
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        Environment.SetEnvironmentVariable("ANTHROPIC_MODEL", null);
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
        Environment.SetEnvironmentVariable("OPENAI_MODEL", null);
        Environment.SetEnvironmentVariable("OPENAI_API_URL", null);
        Environment.SetEnvironmentVariable("ENABLE_THINKING", null);
        Environment.SetEnvironmentVariable("THINKING_BUDGET_TOKENS", null);
    }
}