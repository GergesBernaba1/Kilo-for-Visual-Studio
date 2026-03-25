using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Kilo.VisualStudio.App.Services;
using Kilo.VisualStudio.Contracts.Models;
using Kilo.VisualStudio.Integration;

namespace Kilo.VisualStudio.Tests
{
    [MemoryDiagnoser]
    public class KiloBenchmarks
    {
        private KiloBackendClient _backendClient;
        private AssistantService _assistantService;

        public KiloBenchmarks()
        {
            _backendClient = new KiloBackendClient(new System.Net.Http.HttpClient(), true)
            {
                BackendUrl = "http://localhost:4000",
                ApiKey = "test"
            };
            _assistantService = new AssistantService(new MockKiloSessionHostAdapter(), () => new KiloServerEndpoint());
        }

        [Benchmark]
        public async Task<AssistantResponse> AskAssistant_ThroughSessionHostAsync()
        {
            var request = new AssistantRequest
            {
                ActiveFilePath = ".",
                LanguageId = "csharp",
                Prompt = "Hi",
                SessionId = "test"
            };

            return await _assistantService.AskAssistantAsync(request);
        }

        [Benchmark]
        public string BuildEndpoint_OpenAI()
        {
            return _backendClient.BuildEndpoint("openai", "gpt-4o");
        }
    }
}
