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
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly string _model;

        // 用于 Google Gemini API 结构的私有记录
        private record GeminiPart([property: JsonPropertyName("text")] string Text);
        private record GeminiContent([property: JsonPropertyName("parts")] List<GeminiPart> Parts, [property: JsonPropertyName("role")] string Role = "user");
        private record GeminiRequest([property: JsonPropertyName("contents")] List<GeminiContent> Contents);
        private record GeminiResponseCandidate([property: JsonPropertyName("content")] GeminiContent Content);
        private record GeminiResponse([property: JsonPropertyName("candidates")] List<GeminiResponseCandidate> Candidates);

        public BasicApiEngine(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _baseUrl = configuration["OpenAiConfig:BaseUrl"];
            _apiKey = configuration["OpenAiConfig:ApiKey"];
            _model = configuration["OpenAiConfig:Model"] ?? "gemini-1.5-flash-latest"; // 默认模型

            if (string.IsNullOrEmpty(_baseUrl) || string.IsNullOrEmpty(_apiKey))
            {
                // 如果未配置，不抛出异常，而是优雅地降级或记录警告
                // 这里为了演示，我们返回一个提示信息
            }
        }

        public async Task<string> ChatAsync(string message)
        {
            if (string.IsNullOrEmpty(_baseUrl) || string.IsNullOrEmpty(_apiKey) || _apiKey == "YOUR_API_KEY")
            {
                return "错误: API BaseUrl 或 ApiKey 未在配置文件中正确设置。请检查 appsettings.json 中的 OpenAiConfig 节点，并确保 ApiKey 已被替换。";
            }

            // Google Gemini API 使用 API Key 作为查询参数，而不是 Bearer Token
            _httpClient.DefaultRequestHeaders.Authorization = null;

            var requestPayload = new GeminiRequest(
                new List<GeminiContent> {
                    new(new List<GeminiPart> { new(message) })
                }
            );

            try
            {
                // Gemini API 的 URL 结构: .../v1beta/models/{model}:generateContent?key={apiKey}
                var requestUrl = $"{_baseUrl.TrimEnd('/')}/models/{_model}:generateContent?key={_apiKey}";
                var response = await _httpClient.PostAsJsonAsync(requestUrl, requestPayload);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return $"API 错误: {response.StatusCode}。详情: {errorContent}";
                }

                var apiResponse = await response.Content.ReadFromJsonAsync<GeminiResponse>();
                // Gemini 的回复结构与 OpenAI 不同
                return apiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text?.Trim() ?? "AI 未返回有效回复。";
            }
            catch (Exception ex)
            {
                return $"联系 AI 服务时发生错误: {ex.Message}";
            }
        }
    }
}
