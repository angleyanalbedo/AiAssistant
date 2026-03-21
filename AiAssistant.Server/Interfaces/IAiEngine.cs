using AiAssistant.Server.Models;
using System.Threading.Tasks;

namespace AiAssistant.Server.Interfaces
{
    public interface IAiEngine
    {
        Task<string> ChatAsync(ChatRequest request);
    }
}
