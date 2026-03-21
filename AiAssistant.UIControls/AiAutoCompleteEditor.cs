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
        private bool _isGhostActive = false;
        private int _ghostStart = 0;
        private int _ghostLength = 0;
        private const int GhostStyleIndex = 50;

        public event EventHandler<AiActionRequestedEventArgs> OnAiActionRequested;
        public event EventHandler OnFocusChatRequested;

        public AiConnectionMode ConnectionMode { get; set; } = AiConnectionMode.LocalServer;
        public string ServerApiUrl { get; set; } = "http://localhost:5000/api/completion";
        public string DirectApiBaseUrl { get; set; } = "https://api.openai.com/v1";
        public string DirectApiKey { get; set; } = "";
        public string DirectApiModel { get; set; } = "gpt-3.5-turbo";
        public string SystemPrompt { get; set; } = "你是一个代码补全引擎，只输出光标后的补全代码，不要任何解释";

        public AiAutoCompleteEditor()
        {
            InitEditorStyle();

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

        private void InitEditorStyle()
        {
            // Stage 1: Basic properties and font settings
            foreach (var style in this.Styles)
            {
                style.Font = "Consolas";
                style.Size = 10;
            }
            this.CaretLineVisible = true;
            this.CaretLineBackColor = Color.FromArgb(240, 240, 240);

            // Stage 2: Configure margins (line numbers and folding)
            this.Margins[0].Type = MarginType.Number;
            this.Margins[0].Width = 35;

            this.SetProperty("fold", "1");
            this.SetProperty("fold.compact", "1");
            this.Margins[2].Type = MarginType.Symbol;
            this.Margins[2].Mask = Marker.MaskFolders;
            this.Margins[2].Sensitive = true;
            this.Margins[2].Width = 20;

            this.Markers[Marker.Folder].Symbol = MarkerSymbol.BoxPlus;
            this.Markers[Marker.FolderOpen].Symbol = MarkerSymbol.BoxMinus;
            this.Markers[Marker.FolderEnd].Symbol = MarkerSymbol.BoxPlus;
            this.Markers[Marker.FolderOpenMid].Symbol = MarkerSymbol.BoxMinus;
            this.Markers[Marker.FolderSub].Symbol = MarkerSymbol.VLine;
            this.Markers[Marker.FolderTail].Symbol = MarkerSymbol.LCorner;

            for (int i = Marker.Folder; i <= Marker.FolderTail; i++)
            {
                this.Markers[i].SetForeColor(Color.Gray);
                this.Markers[i].SetBackColor(Color.White);
            }

            // Stage 3: Enable C# syntax highlighting (Lexer)
            this.Lexer = Lexer.Cpp;
            this.SetKeywords(0, "abstract as base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while get set value");

            this.Styles[Style.Cpp.Default].ForeColor = Color.Black;
            this.Styles[Style.Cpp.Identifier].ForeColor = Color.Black;
            this.Styles[Style.Cpp.Number].ForeColor = Color.DarkMagenta;
            this.Styles[Style.Cpp.String].ForeColor = Color.FromArgb(163, 21, 21);
            this.Styles[Style.Cpp.Character].ForeColor = Color.FromArgb(163, 21, 21);
            this.Styles[Style.Cpp.Word].ForeColor = Color.Blue;
            this.Styles[Style.Cpp.Comment].ForeColor = Color.Green;
            this.Styles[Style.Cpp.CommentLine].ForeColor = Color.Green;

            // Ghost text style
            this.Styles[GhostStyleIndex].ForeColor = Color.Gray;
            this.Styles[GhostStyleIndex].Italic = true;
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
                    DisplayGhostSuggestion(completion);
                }
            }
            catch (Exception)
            {
                // Ignore completion errors
            }
        }

        private void DisplayGhostSuggestion(string suggestion)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => DisplayGhostSuggestion(suggestion)));
                return;
            }

            if (_isGhostActive)
            {
                ClearGhostSuggestion();
            }

            if (string.IsNullOrEmpty(suggestion)) return;

            _ghostStart = this.CurrentPosition;
            _ghostLength = suggestion.Length;

            this.AutoCCompleteCancel();

            this.InsertText(_ghostStart, suggestion);
            this.Anchor = _ghostStart;
            this.CurrentPosition = _ghostStart;

            this.StartStyling(_ghostStart);
            this.SetStyling(_ghostLength, GhostStyleIndex);

            _isGhostActive = true;
        }

        private void ClearGhostSuggestion()
        {
            if (!_isGhostActive) return;

            this.DeleteRange(_ghostStart, _ghostLength);

            _isGhostActive = false;
            _ghostStart = 0;
            _ghostLength = 0;
        }

        private void AcceptGhostSuggestion()
        {
            if (!_isGhostActive) return;

            this.StartStyling(_ghostStart);
            this.SetStyling(_ghostLength, Style.Cpp.Default);

            this.CurrentPosition = _ghostStart + _ghostLength;
            this.Anchor = this.CurrentPosition;

            _isGhostActive = false;
            _ghostStart = 0;
            _ghostLength = 0;

            this.Invalidate();
        }

        private void AiAutoCompleteEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (_isGhostActive)
            {
                // 1. 采纳逻辑 (Tab 键)
                if (e.KeyCode == Keys.Tab && e.Modifiers == Keys.None)
                {
                    AcceptGhostSuggestion();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return;
                }

                // 2. 取消逻辑 (Esc 键)
                if (e.KeyCode == Keys.Escape)
                {
                    ClearGhostSuggestion();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return;
                }

                // 3. 任何其他非纯修饰键的按键都会取消建议，然后正常处理该按键
                switch (e.KeyCode)
                {
                    case Keys.ControlKey:
                    case Keys.ShiftKey:
                    case Keys.Menu: // Alt
                    case Keys.LWin:
                    case Keys.RWin:
                        // 如果只是修饰键，则不执行任何操作
                        break;
                    default:
                        // 对于字符、方向键、删除等，先清除建议
                        ClearGhostSuggestion();
                        // 然后让 Scintilla 正常处理该按键事件
                        break;
                }
            }

            // 保留焦点切换逻辑
            if (e.Control && e.KeyCode == Keys.J)
            {
                e.SuppressKeyPress = true;
                OnFocusChatRequested?.Invoke(this, EventArgs.Empty);
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

        public void FocusEditor() { this.Focus(); }

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
