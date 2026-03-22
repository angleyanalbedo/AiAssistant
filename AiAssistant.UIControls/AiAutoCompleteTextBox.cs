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
    /// <summary>
    /// 一个基于 ScintillaNET 的智能文本框，提供基础的 AI 代码补全（幽灵文本）功能。
    /// </summary>
    public class AiAutoCompleteTextBox : Scintilla
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private System.Threading.CancellationTokenSource _aiCts;
        private string _ghostText = string.Empty;
        private int _ghostTextPosition = -1;
        private bool _isInternalChange = false;
        private const int GHOST_TEXT_STYLE = Style.Default + 1; // Use a style not used by lexers

        /// <summary>
        /// 定义编辑器支持的编程语言。
        /// </summary>
        public enum CodeLanguage { PlainText, CSharp, ST }
        private CodeLanguage _currentLanguage = CodeLanguage.PlainText;
        /// <summary>
        /// 获取或设置编辑器的当前编程语言，这将影响语法高亮。
        /// </summary>
        public CodeLanguage CurrentLanguage
        {
            get { return _currentLanguage; }
            set
            {
                _currentLanguage = value;
                ApplyLanguageSettings();
            }
        }

        /// <summary>
        /// 获取或设置本地服务器的补全 API 地址。
        /// </summary>
        public string ServerApiUrl { get; set; } = "http://localhost:5000/api/completion";
        /// <summary>
        /// 获取或设置 AI 连接模式（本地服务器或直连）。
        /// </summary>
        public AiConnectionMode ConnectionMode { get; set; } = AiConnectionMode.LocalServer;
        /// <summary>
        /// 获取或设置直连 OpenAI 兼容 API 的基础 URL。
        /// </summary>
        public string DirectApiBaseUrl { get; set; } = "https://api.openai.com/v1";
        /// <summary>
        /// 获取或设置直连 API 所需的密钥。
        /// </summary>
        public string DirectApiKey { get; set; } = "";
        /// <summary>
        /// 获取或设置直连 API 使用的模型名称。
        /// </summary>
        public string DirectApiModel { get; set; } = "gpt-3.5-turbo";
        /// <summary>
        /// 获取或设置发送给 AI 模型的系统提示，用于代码补全场景。
        /// </summary>
        public string SystemPrompt { get; set; } = "你是一个代码补全助手，只输出补全的代码，不要任何解释";

        /// <summary>
        /// 初始化 AiAutoCompleteTextBox 控件的新实例。
        /// </summary>
        public AiAutoCompleteTextBox()
        {
            // Basic Scintilla setup
            this.WrapMode = WrapMode.Word;
            this.CaretForeColor = Color.Black;
            this.Margins[0].Width = 30;

            this.CurrentLanguage = CodeLanguage.CSharp; // Set default and trigger styling

            this.KeyDown += AiAutoCompleteTextBox_KeyDown;
            this.UpdateUI += AiAutoCompleteTextBox_UpdateUI;
        }


        private void ApplyLanguageSettings()
        {
            // 1. Set default styles
            this.Styles[Style.Default].Font = "Consolas";
            this.Styles[Style.Default].Size = 10;

            // 2. Apply default styles to all other styles
            this.StyleClearAll();

            // 3. Set lexer-specific styles
            switch (_currentLanguage)
            {
                case CodeLanguage.CSharp:
                    this.Lexer = Lexer.Cpp;
                    this.SetKeywords(0, "abstract as base bool break byte case catch char class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while get set value");
                    this.Styles[Style.Cpp.Word].ForeColor = Color.Blue;
                    this.Styles[Style.Cpp.CommentLine].ForeColor = Color.Green;
                    this.Styles[Style.Cpp.String].ForeColor = Color.Brown;
                    SystemPrompt = "你是一个代码补全助手，只输出补全的代码，不要任何解释";
                    break;
                case CodeLanguage.ST:
                    this.Lexer = Lexer.Pascal;
                    this.SetKeywords(0, "IF THEN ELSE ELSIF END_IF CASE OF END_CASE FOR TO BY DO END_FOR WHILE END_WHILE REPEAT UNTIL END_REPEAT VAR VAR_INPUT VAR_OUTPUT VAR_IN_OUT VAR_GLOBAL END_VAR TYPE END_TYPE STRUCT END_STRUCT PROGRAM FUNCTION_BLOCK AND OR NOT XOR");
                    this.Styles[Style.Pascal.Word].ForeColor = Color.Blue;
                    this.Styles[Style.Pascal.Comment].ForeColor = Color.Green;
                    this.Styles[Style.Pascal.String].ForeColor = Color.DarkRed;
                    SystemPrompt = "你是一个工业自动化专家，请只输出符合 IEC 61131-3 标准的 ST (Structured Text) 补全代码，不要解释。";
                    break;
                default:
                    this.Lexer = Lexer.Null;
                    break;
            }

            // 4. Re-apply ghost text style as it was cleared
            this.Styles[GHOST_TEXT_STYLE].ForeColor = SystemColors.GrayText;
            this.Styles[GHOST_TEXT_STYLE].Italic = true;
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
