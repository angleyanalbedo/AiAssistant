using AiAssistant.Server.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace AiAssistant.Server.Engines
{
    public class StandardAiEngine : IAiEngine
    {
        private readonly IConfiguration _configuration;

        public StandardAiEngine(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<string> ChatAsync(string message)
        {
            try
            {
                var activeProvider = _configuration["AiProviders:Active"];
                if (string.IsNullOrEmpty(activeProvider))
                {
                    return "错误: 未在 appsettings.json 中配置活动的 AI 提供商 (AiProviders:Active)。";
                }

                var configPath = $"AiProviders:Endpoints:{activeProvider}";
                var baseUrl = _configuration[$"{configPath}:BaseUrl"];
                var apiKey = _configuration[$"{configPath}:ApiKey"];
                var model = _configuration[$"{configPath}:Model"];

                if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(model))
                {
                    return $"错误: 提供商 '{activeProvider}' 的 BaseUrl 或 Model 未配置。";
                }

                // 兼容本地 Ollama 等不需要 key 的情况
                if (string.IsNullOrEmpty(apiKey))
                {
                    apiKey = "dummy-key";
                }

                var client = new OpenAI.OpenAIClient(new System.ClientModel.ApiKeyCredential(apiKey), new OpenAI.OpenAIClientOptions { Endpoint = new Uri(baseUrl) });
                var chatClient = client.AsChatClient(model);

                var response = await chatClient.CompleteAsync(message);

                return response.Message.Text;
            }
            catch (Exception ex)
            {
                return $"调用 AI 服务时发生异常: {ex.Message}";
            }
        }
    }
}
