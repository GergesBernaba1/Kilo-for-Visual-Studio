using System.Threading.Tasks;
using Kilo.VisualStudio.Contracts.Models;

namespace Kilo.VisualStudio.Contracts.Services
{
    public interface IKiloBackendClient
    {
        string? ApiKey { get; set; }

        Task<AssistantResponse> SendRequestAsync(AssistantRequest request);

        Task<TResponse> SendGenericRequestAsync<TRequest, TResponse>(string endpoint, TRequest request);
    }
}
