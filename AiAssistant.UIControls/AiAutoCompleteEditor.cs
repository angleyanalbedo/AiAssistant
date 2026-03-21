using AiAssistant.UIControls.Utils;
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
    public class AiAutoCompleteEditor : Scintilla
    {
        private Timer _debounceTimer;
        private static readonly HttpClient _httpClient = new HttpClient();

        public event EventHandler<AiActionRequestedEventArgs> OnAiActionRequested;

        public AiConnectionMode ConnectionMode { get; set; } = AiConnectionMode.LocalServer;
        public string ServerApiUrl { get; set; } = "http://localhost:5000/api/completion";
        public string DirectApiBaseUrl { get; set; } = "https://api.openai.com/v1";
        public string DirectApiKey { get; set; } = "";
        public string DirectApiModel { get; set; } = "gpt-3.5-turbo";
        public string SystemPrompt { get; set; } = "你是一个代码补全引擎，只输出光标后的补全代码，不要任何解释";

        public AiAutoCompleteEditor()
        {
            InitializeEditorStyle();

            _debounceTimer = new Timer();
            _debounceTimer.Interval = 500;
            _debounceTimer.Tick += OnDebounceTimerTick;

            this.TextChanged += AiAutoCompleteEditor_TextChanged;
            this.KeyDown += AiAutoCompleteEditor_KeyDown;
            SetupContextMenu();
        }

        private void SetupContextMenu()
        {
            var contextMenu = new ContextMenuStrip();
            var itemExplain = new ToolStripMenuItem("解释此段代码");
            var itemFindBugs = new ToolStripMenuItem("寻找 Bug");
            var itemAddComments = new ToolStripMenuItem("添加注释");

            itemExplain.Click += (sender, e) => HandleContextMenuAction("请解释以下代码");
            itemFindBugs.Click += (sender, e) => HandleContextMenuAction("请检查以下代码是否存在 Bug");
            itemAddComments.Click += (sender, e) => HandleContextMenuAction("请为以下代码添加注释");

            contextMenu.Items.Add(itemExplain);
            contextMenu.Items.Add(itemFindBugs);
            contextMenu.Items.Add(itemAddComments);

            this.ContextMenuStrip = contextMenu;
        }

        private void HandleContextMenuAction(string actionPrefix)
        {
            if (!string.IsNullOrEmpty(this.SelectedText))
            {
                string prompt = $"{actionPrefix}:\n```csharp\n{this.SelectedText}\n```";
                OnAiActionRequested?.Invoke(this, new AiActionRequestedEventArgs { Prompt = prompt });
            }
        }

        private void InitializeEditorStyle()
        {
            this.Margins[0].Width = 35;
            this.WrapMode = WrapMode.Word;

            this.LexerName = "pascal";

            this.Styles[Style.Default].Font = "Consolas";
            this.Styles[Style.Default].Size = 10;
            this.Styles[Style.Default].ForeColor = Color.Black;
            this.ClearAll();

            this.Styles[Style.Cpp.Default].ForeColor = Color.Black;
            this.Styles[Style.Cpp.Word].ForeColor = Color.Blue;
            this.Styles[Style.Cpp.String].ForeColor = Color.FromArgb(163, 21, 21);
            this.Styles[Style.Cpp.Comment].ForeColor = Color.Green;
            this.Styles[Style.Cpp.CommentLine].ForeColor = Color.Green;
            this.Styles[Style.Cpp.Number].ForeColor = Color.DarkMagenta;
        }

        private void AiAutoCompleteEditor_TextChanged(object sender, EventArgs e)
        {
            _debounceTimer.Stop();
            if (this.SelectedText.Length == 0)
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
            if (this.SelectedText.Length > 0 || string.IsNullOrWhiteSpace(this.Text)) return;

            try
            {
                string completion = "";
                if (ConnectionMode == AiConnectionMode.LocalServer)
                {
                    var requestPayload = new { text = this.Text };
                    var content = new StringContent(JsonConvert.SerializeObject(requestPayload), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync(ServerApiUrl, content);
                    response.EnsureSuccessStatusCode();
                    var responseString = await response.Content.ReadAsStringAsync();
                    var completionResponse = JsonConvert.DeserializeObject<CompletionResponse>(responseString);
                    completion = completionResponse.Completion;
                }
                else // DirectOpenAI
                {
                    object payload;
                    string requestUrl;
                    HttpRequestMessage request;

                    if (DirectApiBaseUrl.Contains("googleapis.com"))
                    {
                        payload = new
                        {
                            systemInstruction = new { parts = new[] { new { text = this.SystemPrompt } } },
                            contents = new[] { new { parts = new[] { new { text = this.Text } } } }
                        };
                        requestUrl = $"{DirectApiBaseUrl.TrimEnd('/')}/models/{DirectApiModel}:generateContent?key={DirectApiKey}";
                        request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                    }
                    else
                    {
                        payload = new
                        {
                            model = DirectApiModel,
                            messages = new[]
                            {
                                new { role = "system", content = this.SystemPrompt },
                                new { role = "user", content = this.Text }
                            }
                        };
                        requestUrl = DirectApiBaseUrl.TrimEnd('/') + "/chat/completions";
                        request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                        request.Headers.Add("Authorization", $"Bearer {DirectApiKey}");
                    }

                    request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                    var response = await _httpClient.SendAsync(request);
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

                if (!string.IsNullOrEmpty(completion))
                {
                    ShowGhostText(completion);
                }
            }
            catch (Exception)
            {
                // Ignore completion errors
            }
        }

        private void ShowGhostText(string completionText)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => ShowGhostText(completionText)));
                return;
            }

            if (this.SelectedText.Length > 0) return;

            int currentPos = this.CurrentPosition;
            this.InsertText(currentPos, completionText);
            this.SetSelection(currentPos + completionText.Length, currentPos);
        }

        private void AiAutoCompleteEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Tab && this.SelectedText.Length > 0)
            {
                this.GotoPosition(this.SelectionEnd);
                e.SuppressKeyPress = true;
            }
        }

        public void InsertTextAtCursor(string text)
        {
            this.InsertText(this.CurrentPosition, text);
        }

        public void ReplaceSelectedText(string text)
        {
            this.ReplaceSelection(text);
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
