using AiAssistant.Server.Interfaces;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AiAssistant.Server.Engines
{
    public class BasicApiEngine : IAiEngine
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiUrl;
        private readonly string _apiKey;
        private readonly string _model;

        // 用于 OpenAI 兼容 API 结构的私有记录
        private record OpenAiMessage([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("content")] string Content);
        private record OpenAiRequest([property: JsonPropertyName("model")] string Model, [property: JsonPropertyName("messages")] List<OpenAiMessage> Messages);
        private record OpenAiChoice([property: JsonPropertyName("message")] OpenAiMessage Message);
        private record OpenAiResponse([property: JsonPropertyName("choices")] List<OpenAiChoice> Choices);

        public BasicApiEngine(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _apiUrl = configuration["OpenAI:ApiUrl"];
            _apiKey = configuration["OpenAI:ApiKey"];
            _model = configuration["OpenAI:Model"] ?? "gpt-3.5-turbo"; // 默认模型

            if (string.IsNullOrEmpty(_apiUrl) || string.IsNullOrEmpty(_apiKey))
            {
                // 如果未配置，不抛出异常，而是优雅地降级或记录警告
                // 这里为了演示，我们返回一个提示信息
            }
        }

        public async Task<string> ChatAsync(string message)
        {
            if (string.IsNullOrEmpty(_apiUrl) || string.IsNullOrEmpty(_apiKey))
            {
                return "错误: OpenAI API URL 或 ApiKey 未在配置文件中设置。";
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var requestPayload = new OpenAiRequest(
                _model,
                new List<OpenAiMessage> { new("user", message) }
            );

            try
            {
                var response = await client.PostAsJsonAsync(_apiUrl, requestPayload);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return $"API 错误: {response.StatusCode}。详情: {errorContent}";
                }

                var apiResponse = await response.Content.ReadFromJsonAsync<OpenAiResponse>();
                return apiResponse?.Choices?.FirstOrDefault()?.Message?.Content?.Trim() ?? "AI 未返回有效回复。";
            }
            catch (Exception ex)
            {
                return $"联系 AI 服务时发生错误: {ex.Message}";
            }
        }
    }
}
