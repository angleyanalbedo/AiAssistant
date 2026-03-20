using AiAssistant.Server.Interfaces;
using AiAssistant.Server.Utils;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
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
                    return "错误: 未配置活动 AI 提供商";
                }

                var configPath = $"AiProviders:Endpoints:{activeProvider}";
                var baseUrl = _configuration[$"{configPath}:BaseUrl"];
                var apiKey = _configuration[$"{configPath}:ApiKey"];
                var model = _configuration[$"{configPath}:Model"];

                if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(model))
                {
                    return $"错误: 提供商 '{activeProvider}' 配置不完整";
                }

                if (string.IsNullOrEmpty(apiKey))
                {
                    apiKey = "dummy-key";
                }

                var client = new OpenAIClient(
                    new System.ClientModel.ApiKeyCredential(apiKey),
                    new OpenAIClientOptions
                    {
                        Endpoint = new Uri(baseUrl)
                    }
                );

                var chatClient = client.GetChatClient(model);

                var messages = new List<ChatMessage>
                {
                    new UserChatMessage(message)
                };

                var response = await chatClient.CompleteChatAsync(messages);

                return AnsiStripper.Clean(response.Value.Content[0].Text);
            }
            catch (Exception ex)
            {
                return $"调用 AI 服务异常: {ex.Message}";
            }
        }
    }
}
