using CodeAgentDemo.Providers;
using FluentAssertions;
using Xunit;

namespace CodeAgentDemo.Tests.Providers;

public class ChatProviderFactoryTests
{
    [Fact]
    public void Create_WithoutApiKey_ShouldThrowInvalidOperationException()
    {
        var originalKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);

        try
        {
            var act = () => ChatProviderFactory.Create();

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*OPENAI_API_KEY*");
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalKey);
        }
    }

    [Fact]
    public void Create_WithApiKey_ShouldCreateOpenAIProvider()
    {
        var originalKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-api-key");

        try
        {
            var provider = ChatProviderFactory.Create();

            provider.Should().BeOfType<OpenAIProvider>();
            provider.ProviderName.Should().Be("OpenAI");
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalKey);
        }
    }

    [Fact]
    public void Create_WithCustomModel_ShouldUseCustomModel()
    {
        var originalKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var originalModel = Environment.GetEnvironmentVariable("OPENAI_MODEL");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-api-key");
        Environment.SetEnvironmentVariable("OPENAI_MODEL", "gpt-4-turbo");

        try
        {
            var provider = ChatProviderFactory.Create();

            provider.Should().BeOfType<OpenAIProvider>();
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalKey);
            Environment.SetEnvironmentVariable("OPENAI_MODEL", originalModel);
        }
    }

    [Fact]
    public void Create_WithCustomEndpoint_ShouldCreateProvider()
    {
        var originalKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var originalUrl = Environment.GetEnvironmentVariable("OPENAI_API_URL");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-api-key");
        Environment.SetEnvironmentVariable("OPENAI_API_URL", "https://custom.api/v1");

        try
        {
            var provider = ChatProviderFactory.Create();

            provider.Should().BeOfType<OpenAIProvider>();
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalKey);
            Environment.SetEnvironmentVariable("OPENAI_API_URL", originalUrl);
        }
    }

    [Fact]
    public void Create_WithThinkingEnabled_ShouldCreateProvider()
    {
        var originalKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var originalThinking = Environment.GetEnvironmentVariable("ENABLE_THINKING");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-api-key");
        Environment.SetEnvironmentVariable("ENABLE_THINKING", "true");

        try
        {
            var provider = ChatProviderFactory.Create();

            provider.Should().BeOfType<OpenAIProvider>();
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalKey);
            Environment.SetEnvironmentVariable("ENABLE_THINKING", originalThinking);
        }
    }

    [Fact]
    public void Create_WithoutModelEnvVar_ShouldUseDefaultModel()
    {
        var originalKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var originalModel = Environment.GetEnvironmentVariable("OPENAI_MODEL");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-api-key");
        Environment.SetEnvironmentVariable("OPENAI_MODEL", null);

        try
        {
            var provider = ChatProviderFactory.Create();

            provider.Should().BeOfType<OpenAIProvider>();
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalKey);
            Environment.SetEnvironmentVariable("OPENAI_MODEL", originalModel);
        }
    }
}