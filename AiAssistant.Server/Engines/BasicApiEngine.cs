using AiAssistant.Server.Interfaces;
using System.Threading.Tasks;

namespace AiAssistant.Server.Engines
{
    public class BasicApiEngine : IAiEngine
    {
        public async Task<string> ChatAsync(string message)
        {
            await Task.Delay(500); // 模拟基础 API 的网络延迟
            return $"【基础模式 API】回复: {message}";
        }
    }
}
