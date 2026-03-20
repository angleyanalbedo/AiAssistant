using System.Threading.Tasks;

namespace AiAssistant.Server.Interfaces
{
    public interface IAiEngine
    {
        Task<string> ChatAsync(string message);
    }
}
