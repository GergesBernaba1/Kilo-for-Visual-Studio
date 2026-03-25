using System;
using Kilo.VisualStudio.Integration;
using Xunit;

namespace Kilo.VisualStudio.Tests
{
    public class BackendClientTests
    {
        [Theory]
        [InlineData("http://localhost:4000", "openai", "gpt-4o", "http://localhost:4000/openai/gpt-4o")]
        [InlineData("http://localhost:4000/", "anthropic", "claude-3", "http://localhost:4000/anthropic/claude-3")]
        [InlineData("http://localhost:4000", "google", "gemini-2", "http://localhost:4000/google/gemini-2")]
        [InlineData("http://localhost:4000", "local", "mymodel", "http://localhost:4000/local/mymodel")]
        [InlineData("http://localhost:4000", "ollama", "mixtral", "http://localhost:4000/local/mixtral")]
        [InlineData("http://localhost:4000", "", "google:gemini-2", "http://localhost:4000/google/gemini-2")]
        public void BuildEndpoint_ProviderModelMapping_Works(string backendUrl, string provider, string model, string expected)
        {
            var client = new KiloBackendClient(new System.Net.Http.HttpClient(), true)
            {
                BackendUrl = backendUrl
            };

            var endpoint = client.BuildEndpoint(provider, model);
            Assert.Equal(expected, endpoint);
        }
    }
}
