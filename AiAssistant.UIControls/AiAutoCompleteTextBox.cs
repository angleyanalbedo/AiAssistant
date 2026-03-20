using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AiAssistant.UIControls
{
    public class AiAutoCompleteTextBox : TextBox
    {
        private Timer _debounceTimer;
        private static readonly HttpClient _httpClient = new HttpClient();
        private string _ghostText = string.Empty;
        private bool _isAcceptingGhostText = false;

        public string ServerApiUrl { get; set; } = "http://localhost:5000/api/completion";
        public AiConnectionMode ConnectionMode { get; set; } = AiConnectionMode.LocalServer;
        public string DirectApiBaseUrl { get; set; } = "https://api.openai.com/v1";
        public string DirectApiKey { get; set; } = "";
        public string DirectApiModel { get; set; } = "gpt-3.5-turbo";

        public AiAutoCompleteTextBox()
        {
            this.Multiline = true;

            _debounceTimer = new Timer();
            _debounceTimer.Interval = 500;
            _debounceTimer.Tick += OnDebounceTimerTick;
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);

            if (_isAcceptingGhostText) return;

            if (!string.IsNullOrEmpty(_ghostText))
            {
                _ghostText = string.Empty;
            }

            _debounceTimer.Stop();
            if (!string.IsNullOrWhiteSpace(this.Text))
            {
                _debounceTimer.Start();
            }
        }

        private async void OnDebounceTimerTick(object sender, EventArgs e)
        {
            _debounceTimer.Stop();
            await TriggerCompletion();
        }

        private async Task TriggerCompletion()
        {
            if (this.SelectionLength > 0 || string.IsNullOrWhiteSpace(this.Text) || this.SelectionStart < this.Text.Length) return;

            try
            {
                if (ConnectionMode == AiConnectionMode.LocalServer)
                {
                    var request = new { text = this.Text };
                    var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync(ServerApiUrl, content);
                    response.EnsureSuccessStatusCode();
                    var responseString = await response.Content.ReadAsStringAsync();
                    var completionResponse = JsonConvert.DeserializeObject<CompletionResponse>(responseString);

                    if (!string.IsNullOrEmpty(completionResponse.Completion))
                    {
                        ShowGhostText(completionResponse.Completion);
                    }
                }
                else // DirectOpenAI
                {
                    if (DirectApiBaseUrl.Contains("googleapis.com"))
                    {
                        // Google Gemini API logic
                        var geminiPayload = new
                        {
                            systemInstruction = new { parts = new[] { new { text = "你是一个代码补全助手，只输出补全的代码，不要任何解释" } } },
                            contents = new[] { new { parts = new[] { new { text = this.Text } } } }
                        };
                        var requestUrl = $"{DirectApiBaseUrl.TrimEnd('/')}/models/{DirectApiModel}:generateContent?key={DirectApiKey}";
                        var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                        request.Content = new StringContent(JsonConvert.SerializeObject(geminiPayload), Encoding.UTF8, "application/json");

                        var response = await _httpClient.SendAsync(request);
                        var responseString = await response.Content.ReadAsStringAsync();
                        response.EnsureSuccessStatusCode();

                        dynamic result = JsonConvert.DeserializeObject(responseString);
                        string completion = result.candidates[0].content.parts[0].text;

                        if (!string.IsNullOrEmpty(completion))
                        {
                            ShowGhostText(completion);
                        }
                    }
                    else
                    {
                        // Standard OpenAI API logic
                        var openAiPayload = new
                        {
                            model = DirectApiModel,
                            messages = new[]
                            {
                                new { role = "system", content = "你是一个代码补全助手，只输出补全的代码，不要任何解释" },
                                new { role = "user", content = this.Text }
                            }
                        };
                        var requestUrl = DirectApiBaseUrl.TrimEnd('/') + "/chat/completions";
                        var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                        request.Headers.Add("Authorization", $"Bearer {DirectApiKey}");
                        request.Content = new StringContent(JsonConvert.SerializeObject(openAiPayload), Encoding.UTF8, "application/json");

                        var response = await _httpClient.SendAsync(request);
                        var responseString = await response.Content.ReadAsStringAsync();
                        response.EnsureSuccessStatusCode();

                        dynamic result = JsonConvert.DeserializeObject(responseString);
                        string completion = result.choices[0].message.content;

                        if (!string.IsNullOrEmpty(completion))
                        {
                            ShowGhostText(completion);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Ignore completion errors
            }
        }

        private void ShowGhostText(string completion)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => ShowGhostText(completion)));
                return;
            }

            _ghostText = completion;
            int start = this.TextLength;
            this.AppendText(completion);
            this.Select(start, completion.Length);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (this.SelectionLength > 0 && !string.IsNullOrEmpty(_ghostText))
            {
                if (e.KeyCode == Keys.Tab)
                {
                    e.SuppressKeyPress = true;
                    _isAcceptingGhostText = true;
                    this.SelectionStart = this.TextLength;
                    this.SelectionLength = 0;
                    _ghostText = string.Empty;
                    _isAcceptingGhostText = false;
                }
            }
            base.OnKeyDown(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && (_debounceTimer != null))
            {
                _debounceTimer.Dispose();
                _debounceTimer = null;
            }
            base.Dispose(disposing);
        }

        private class CompletionResponse
        {
            public string Completion { get; set; }
        }
    }
}
