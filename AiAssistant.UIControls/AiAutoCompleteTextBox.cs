using Newtonsoft.Json;
using ScintillaNET;
using System;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AiAssistant.UIControls
{
    public class AiAutoCompleteTextBox : Scintilla
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private System.Threading.CancellationTokenSource _aiCts;
        private string _ghostText = string.Empty;
        private int _ghostTextPosition = -1;
        private bool _isInternalChange = false;
        private const int GHOST_TEXT_STYLE = Style.Default + 1; // Use a style not used by lexers

        public string ServerApiUrl { get; set; } = "http://localhost:5000/api/completion";
        public AiConnectionMode ConnectionMode { get; set; } = AiConnectionMode.LocalServer;
        public string DirectApiBaseUrl { get; set; } = "https://api.openai.com/v1";
        public string DirectApiKey { get; set; } = "";
        public string DirectApiModel { get; set; } = "gpt-3.5-turbo";
        public string SystemPrompt { get; set; } = "你是一个代码补全助手，只输出补全的代码，不要任何解释";

        public AiAutoCompleteTextBox()
        {
            // Basic Scintilla setup
            this.WrapMode = WrapMode.Word;
            this.LexerName = "null";

            // Styling
            this.Styles[Style.Default].Font = "Consolas";
            this.Styles[Style.Default].Size = 10;
            this.CaretForeColor = Color.Black;
            this.ClearAll();

            // Line numbers
            this.Margins[0].Width = 30;

            // Ghost text style
            this.Styles[GHOST_TEXT_STYLE].ForeColor = SystemColors.GrayText;
            this.Styles[GHOST_TEXT_STYLE].Italic = true;

            this.KeyDown += AiAutoCompleteTextBox_KeyDown;
            this.UpdateUI += AiAutoCompleteTextBox_UpdateUI;
        }


        private void AiAutoCompleteTextBox_UpdateUI(object sender, UpdateUIEventArgs e)
        {
            // If the user moves the caret or selects text, remove the ghost text
            if ((e.Change & UpdateChange.Selection) != 0 && !string.IsNullOrEmpty(_ghostText))
            {
                if (this.SelectedText.Length > 0 || this.CurrentPosition < _ghostTextPosition || this.CurrentPosition > _ghostTextPosition + _ghostText.Length)
                {
                    RemoveGhostText();
                }
            }
        }


        private async void TriggerCompletion()
        {
            if (this.SelectedText.Length > 0) return;

            _aiCts?.Cancel();
            _aiCts = new System.Threading.CancellationTokenSource();
            var token = _aiCts.Token;

            try
            {
                this.CallTipShow(this.CurrentPosition, "AI 正在思考...");

                string completion = "";
                if (ConnectionMode == AiConnectionMode.LocalServer)
                {
                    var request = new { text = this.Text };
                    var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync(ServerApiUrl, content, token);
                    response.EnsureSuccessStatusCode();
                    var responseString = await response.Content.ReadAsStringAsync();
                    var completionResponse = JsonConvert.DeserializeObject<CompletionResponse>(responseString);
                    completion = completionResponse.Completion;
                }
                else // DirectOpenAI
                {
                    HttpRequestMessage request;
                    if (DirectApiBaseUrl.Contains("googleapis.com"))
                    {
                        var geminiPayload = new
                        {
                            systemInstruction = new { parts = new[] { new { text = this.SystemPrompt } } },
                            contents = new[] { new { parts = new[] { new { text = this.Text } } } }
                        };
                        var requestUrl = $"{DirectApiBaseUrl.TrimEnd('/')}/models/{DirectApiModel}:generateContent?key={DirectApiKey}";
                        request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                        request.Content = new StringContent(JsonConvert.SerializeObject(geminiPayload), Encoding.UTF8, "application/json");
                    }
                    else
                    {
                        var openAiPayload = new
                        {
                            model = DirectApiModel,
                            messages = new[]
                            {
                                new { role = "system", content = this.SystemPrompt },
                                new { role = "user", content = this.Text }
                            }
                        };
                        var requestUrl = DirectApiBaseUrl.TrimEnd('/') + "/chat/completions";
                        request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                        request.Headers.Add("Authorization", $"Bearer {DirectApiKey}");
                        request.Content = new StringContent(JsonConvert.SerializeObject(openAiPayload), Encoding.UTF8, "application/json");
                    }

                    using (var response = await _httpClient.SendAsync(request, token))
                    {
                        var responseString = await response.Content.ReadAsStringAsync();
                        response.EnsureSuccessStatusCode();

                        dynamic result = JsonConvert.DeserializeObject(responseString);
                        if (DirectApiBaseUrl.Contains("googleapis.com"))
                        {
                            completion = result.candidates[0].content.parts[0].text;
                        }
                        else
                        {
                            completion = result.choices[0].message.content;
                        }
                    }
                }

                if (token.IsCancellationRequested) return;

                if (!string.IsNullOrEmpty(completion))
                {
                    ShowGhostText(completion);
                }
            }
            catch (Exception)
            {
                // Ignore completion errors
            }
            finally
            {
                if (this.CallTipActive)
                {
                    this.CallTipCancel();
                }
            }
        }

        private void ShowGhostText(string completion)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => ShowGhostText(completion)));
                return;
            }

            _isInternalChange = true;
            try
            {
                RemoveGhostText(); // Clear any existing ghost text

                _ghostText = completion;
                _ghostTextPosition = this.CurrentPosition;

                this.InsertText(_ghostTextPosition, _ghostText);
                this.StartStyling(_ghostTextPosition);
                this.SetStyling(_ghostText.Length, GHOST_TEXT_STYLE);
            }
            finally
            {
                _isInternalChange = false;
            }
        }

        private void RemoveGhostText()
        {
            if (!string.IsNullOrEmpty(_ghostText))
            {
                this.DeleteRange(_ghostTextPosition, _ghostText.Length);
                _ghostText = string.Empty;
                _ghostTextPosition = -1;
            }
        }

        private void AcceptGhostText()
        {
            if (string.IsNullOrEmpty(_ghostText)) return;

            this.StartStyling(_ghostTextPosition);
            this.SetStyling(_ghostText.Length, Style.Default);
            this.GotoPosition(_ghostTextPosition + _ghostText.Length);

            _ghostText = string.Empty;
            _ghostTextPosition = -1;
        }

        private void AiAutoCompleteTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (!string.IsNullOrEmpty(_ghostText))
            {
                if (e.KeyCode == Keys.Tab)
                {
                    AcceptGhostText();
                    e.SuppressKeyPress = true;
                    return;
                }

                // Any other significant key press removes ghost text
                switch (e.KeyCode)
                {
                    case Keys.ControlKey:
                    case Keys.ShiftKey:
                    case Keys.Alt:
                    case Keys.LWin:
                    case Keys.RWin:
                        // Do nothing if it's just a modifier key
                        break;
                    default:
                        _isInternalChange = true;
                        try
                        {
                            RemoveGhostText();
                        }
                        finally
                        {
                            _isInternalChange = false;
                        }
                        if (e.KeyCode == Keys.Escape)
                        {
                            e.SuppressKeyPress = true; // Prevent other controls from processing Escape
                        }
                        break;
                }
            }

            // Manual AI suggestion trigger
            if (e.Alt && e.KeyCode == Keys.Oem5) // Oem5 is usually backslash '\'
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                TriggerCompletion();
                return;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _aiCts?.Cancel();
                _aiCts?.Dispose();
            }
            base.Dispose(disposing);
        }

        private class CompletionResponse
        {
            public string Completion { get; set; }
        }
    }
}
