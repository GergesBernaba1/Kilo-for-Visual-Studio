using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Kilo.VisualStudio.App.Services;
using Kilo.VisualStudio.Contracts.Models;
using Kilo.VisualStudio.Contracts.Services;
using Kilo.VisualStudio.Integration;
using Xunit;

namespace Kilo.VisualStudio.Tests
{
    public class AssistantServiceTests
    {
        private sealed class FakeBackendClient : IKiloBackendClient
        {
            public string? ApiKey { get; set; }
            public string BackendUrl { get; set; } = string.Empty;

            public Task<AssistantResponse> SendRequestAsync(AssistantRequest request)
            {
                return Task.FromResult(new AssistantResponse
                {
                    IsSuccess = true,
                    Message = "OK",
                    SuggestedCode = "// generated code"
                });
            }
        }

        private sealed class FakeSessionHostAdapter : IKiloSessionHostAdapter
        {
            public event EventHandler<KiloSessionEvent>? SessionEventReceived;

            public KiloConnectionState ConnectionState { get; private set; } = KiloConnectionState.Disconnected;

            public Task ConnectAsync(KiloServerEndpoint endpoint, string workspaceDirectory, CancellationToken cancellationToken)
            {
                ConnectionState = KiloConnectionState.Connected;
                return Task.CompletedTask;
            }

            public Task DisconnectAsync(CancellationToken cancellationToken)
            {
                ConnectionState = KiloConnectionState.Disconnected;
                return Task.CompletedTask;
            }

            public Task<KiloSessionSummary> CreateSessionAsync(string workspaceDirectory, CancellationToken cancellationToken)
            {
                return Task.FromResult(new KiloSessionSummary
                {
                    SessionId = "session-1",
                    WorkspaceDirectory = workspaceDirectory,
                    Status = KiloSessionStatus.Idle
                });
            }

            public Task<IReadOnlyList<KiloSessionSummary>> ListSessionsAsync(string workspaceDirectory, CancellationToken cancellationToken)
            {
                IReadOnlyList<KiloSessionSummary> sessions = Array.Empty<KiloSessionSummary>();
                return Task.FromResult(sessions);
            }

            public Task<KiloSessionSummary?> GetSessionAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken)
            {
                return Task.FromResult<KiloSessionSummary?>(null);
            }

            public Task DeleteSessionAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<KiloSessionMessage>> GetMessagesAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken)
            {
                IReadOnlyList<KiloSessionMessage> messages = Array.Empty<KiloSessionMessage>();
                return Task.FromResult(messages);
            }

            public Task SendPromptAsync(KiloChatRequest request, CancellationToken cancellationToken)
            {
                SessionEventReceived?.Invoke(this, new KiloSessionEvent
                {
                    Kind = KiloSessionEventKind.TextDelta,
                    SessionId = request.SessionId,
                    Delta = "streamed response"
                });

                SessionEventReceived?.Invoke(this, new KiloSessionEvent
                {
                    Kind = KiloSessionEventKind.ToolExecutionUpdated,
                    SessionId = request.SessionId,
                    ToolExecution = new KiloToolExecution
                    {
                        SuggestedCode = "// streamed code",
                        PatchDiff = "--- a/file.cs\n+++ b/file.cs"
                    }
                });

                SessionEventReceived?.Invoke(this, new KiloSessionEvent
                {
                    Kind = KiloSessionEventKind.TurnCompleted,
                    SessionId = request.SessionId
                });

                return Task.CompletedTask;
            }

            public Task AbortSessionAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<KiloFileDiff>> GetSessionDiffAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken)
            {
                IReadOnlyList<KiloFileDiff> diffs = Array.Empty<KiloFileDiff>();
                return Task.FromResult(diffs);
            }

            public Task<IReadOnlyList<string>> GetRegisteredToolIdsAsync(string workspaceDirectory, CancellationToken cancellationToken)
            {
                IReadOnlyList<string> tools = Array.Empty<string>();
                return Task.FromResult(tools);
            }

            public Task ReplyToToolPermissionAsync(KiloToolPermissionReply reply, string workspaceDirectory, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task AskAssistantAsync_EmptyPrompt_ReturnsFailureResponse()
        {
            var backend = new FakeBackendClient { ApiKey = "test" };
            var assistant = new AssistantService(backend);

            var request = new AssistantRequest
            {
                ActiveFilePath = "C:\\project\\file.cs",
                LanguageId = "csharp",
                SelectedText = "int x = 1;",
                Prompt = string.Empty
            };

            var result = await assistant.AskAssistantAsync(request);

            Assert.False(result.IsSuccess);
            Assert.Equal("Prompt cannot be empty", result.Error);
        }

        [Fact]
        public async Task AskAssistantAsync_ValidRequest_ReturnsSuccessResponse()
        {
            var backend = new FakeBackendClient { ApiKey = "test" };
            var assistant = new AssistantService(backend);

            var request = new AssistantRequest
            {
                ActiveFilePath = "C:\\project\\file.cs",
                LanguageId = "csharp",
                SelectedText = "int x = 1;",
                Prompt = "Refactor this snippet"
            };

            var result = await assistant.AskAssistantAsync(request);

            Assert.True(result.IsSuccess);
            Assert.Equal("OK", result.Message);
            Assert.Equal("// generated code", result.SuggestedCode);
        }

        [Fact]
        public async Task MockBackend_ExplainPrompt_ReturnsExplanation()
        {
            var mockClient = new KiloBackendClient(new HttpClient(), useMock: true);

            var request = new AssistantRequest
            {
                ActiveFilePath = "C:\\project\\file.cs",
                LanguageId = "csharp",
                SelectedText = "public void Test() { }",
                Prompt = "Explain this code"
            };

            var result = await mockClient.SendRequestAsync(request);

            Assert.True(result.IsSuccess);
            Assert.Contains("EXPLANATION", result.Message);
        }

        [Fact]
        public async Task MockBackend_RefactorPrompt_ReturnsRefactoredCode()
        {
            var mockClient = new KiloBackendClient(new HttpClient(), useMock: true);

            var request = new AssistantRequest
            {
                ActiveFilePath = "C:\\project\\file.cs",
                LanguageId = "csharp",
                SelectedText = "public void Test() { }",
                Prompt = "Refactor this code"
            };

            var result = await mockClient.SendRequestAsync(request);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.SuggestedCode);
            Assert.NotNull(result.PatchDiff);
        }

        [Fact]
        public async Task MockBackend_GeneratePrompt_ReturnsCodeSnippet()
        {
            var mockClient = new KiloBackendClient(new HttpClient(), useMock: true);

            var request = new AssistantRequest
            {
                ActiveFilePath = "C:\\project\\file.cs",
                LanguageId = "csharp",
                SelectedText = string.Empty,
                Prompt = "Generate a method that fetches data"
            };

            var result = await mockClient.SendRequestAsync(request);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.SuggestedCode);
            Assert.Contains("async Task", result.SuggestedCode);
        }

        [Fact]
        public async Task AskAssistantAsync_SessionHostAdapter_CollectsStreamedResponse()
        {
            var sessionHostAdapter = new FakeSessionHostAdapter();
            var assistant = new AssistantService(sessionHostAdapter, () => new KiloServerEndpoint
            {
                BaseUrl = "http://127.0.0.1:4096",
                Password = "test"
            });

            var request = new AssistantRequest
            {
                ActiveFilePath = "C:\\project\\file.cs",
                LanguageId = "csharp",
                SelectedText = "int x = 1;",
                Prompt = "Refactor this snippet"
            };

            var result = await assistant.AskAssistantAsync(request);

            Assert.True(result.IsSuccess);
            Assert.Equal("streamed response", result.Message);
            Assert.Equal("// streamed code", result.SuggestedCode);
            Assert.Equal("--- a/file.cs\n+++ b/file.cs", result.PatchDiff);
        }

        [Fact]
        public async Task MockSessionHostAdapter_StreamsArtifactsThroughAssistantService()
        {
            var sessionHostAdapter = new MockKiloSessionHostAdapter();
            var assistant = new AssistantService(sessionHostAdapter, () => new KiloServerEndpoint());

            var request = new AssistantRequest
            {
                ActiveFilePath = "C:\\project\\file.cs",
                LanguageId = "csharp",
                SelectedText = "public void Test() { }",
                Prompt = "Refactor this code"
            };

            var result = await assistant.AskAssistantAsync(request);

            Assert.True(result.IsSuccess);
            Assert.Contains("session-based protocol", result.Message);
            Assert.NotNull(result.SuggestedCode);
            Assert.NotNull(result.PatchDiff);
        }
    }
}
