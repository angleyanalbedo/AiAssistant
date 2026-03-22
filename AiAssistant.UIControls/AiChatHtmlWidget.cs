using AiAssistant.UIControls.Utils;
using FontAwesome.Sharp;
using Newtonsoft.Json;
using System;
using System.Drawing;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AiAssistant.UIControls
{
    /// <summary>
    /// 一个基于 IE WebBrowser 的兼容性聊天控件，用于在缺少 WebView2 环境的旧系统上提供基础聊天功能。
    /// </summary>
    [ComVisible(true)]
    public class AiChatHtmlWidget : UserControl
    {
        /// <summary>
        /// 当用户点击代码块中的“插入”按钮时触发。
        /// </summary>
        public event EventHandler<InsertCodeEventArgs> OnInsertCodeRequested;
        /// <summary>
        /// 当用户在输入框中按下 Escape 键时触发，请求将焦点切换回主编辑器。
        /// </summary>
        public event EventHandler OnFocusEditorRequested;

        private WebBrowser _webBrowser;
        private TextBox _inputTextBox;
        private IconButton _sendButton;
        private IconButton _clearButton;
        private Panel _inputAreaPanel;

        private static readonly HttpClient _httpClient = new HttpClient();
        private System.Collections.Generic.List<object> _messageHistory = new System.Collections.Generic.List<object>();

        // API Properties
        /// <summary>
        /// 获取或设置发送给 AI 模型的系统提示。
        /// </summary>
        public string SystemPrompt { get; set; } = "你是一个专业的数据处理与测绘工程AI助手。请给出准确、专业的回答。";
        /// <summary>
        /// 获取或设置连接模式（本地服务器或直连）。
        /// </summary>
        public AiConnectionMode ConnectionMode { get; set; } = AiConnectionMode.LocalServer;
        /// <summary>
        /// 获取或设置本地服务器的 API 地址。
        /// </summary>
        public string ServerApiUrl { get; set; } = "http://localhost:5000/api/chat";
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

        private readonly string _htmlTemplate = @"
<!DOCTYPE html>
<html>
<head>
    <meta http-equiv='X-UA-Compatible' content='IE=edge' />
    <meta charset='UTF-8'>
    <link rel=""stylesheet"" href=""https://cdnjs.cloudflare.com/ajax/libs/highlight.js/10.7.3/styles/github.min.css"">
    <script src=""https://cdnjs.cloudflare.com/ajax/libs/highlight.js/10.7.3/highlight.min.js""></script>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, 'Open Sans', 'Helvetica Neue', sans-serif; margin: 0; padding: 10px; background-color: #F0F0F0; overflow-y: scroll; }
        #chat-container { display: flex; flex-direction: column; }
        .msg { max-width: 80%; padding: 10px 15px; margin-bottom: 10px; border-radius: 15px; line-height: 1.5; word-wrap: break-word; }
        .msg-user { align-self: flex-end; background-color: #DCF8C6; color: #000; }
        .msg-ai { align-self: flex-start; background-color: #FFF; color: #000; border: 1px solid #EAEAEA; }
        pre { position: relative; background-color: #F4F4F4; border: 1px solid #DDD; border-radius: 5px; padding: 10px; font-family: Consolas, 'Courier New', monospace; white-space: pre-wrap; word-wrap: break-word; }
        .insert-btn { position: absolute; top: 5px; right: 5px; padding: 4px 8px; cursor: pointer; background: #fff; border: 1px solid #ccc; border-radius: 3px; font-size: 12px; }
        code { font-family: Consolas, 'Courier New', monospace; }
        p { margin: 0 0 10px 0; }
        p:last-child { margin-bottom: 0; }
        table { border-collapse: collapse; width: 100%; margin: 10px 0; background-color: #fff; }
        th, td { border: 1px solid #ccc; padding: 8px 12px; text-align: left; }
        th { background-color: #f0f0f0; font-weight: bold; }
        /* 防止表格过宽导致撑破气泡 */
        .msg-ai, .msg-user { overflow-x: auto; }
        hr {
            height: 1px;
            border: none;
            background-color: #E5E7EB; /* 现代的浅灰实线 */
            margin: 16px 0; /* 上下留出呼吸空间 */
        }
    </style>
    <script>
        function enhanceAndScroll() {
            // 1. Trigger highlighting
            hljs.highlightAll();

            // 2. Add insert buttons to code blocks
            var blocks = document.getElementsByTagName('pre');
            for (var i = 0; i < blocks.length; i++) {
                var block = blocks[i];
                if (block.getAttribute('data-btn-added') === 'true') continue;
                block.setAttribute('data-btn-added', 'true');

                var btn = document.createElement('button');
                btn.innerText = '插入光标处';
                btn.className = 'insert-btn';

                // Closure to handle click event correctly
                (function(currentBlock, currentBtn) {
                    currentBtn.onclick = function() {
                        var codeNode = currentBlock.getElementsByTagName('code')[0];
                        var codeText = codeNode.innerText || codeNode.textContent; // IE-compatible text retrieval

                        // Call C# method via window.external
                        window.external.JsInvokeInsertCode(codeText);

                        currentBtn.innerText = '已插入!';
                        setTimeout(function() { currentBtn.innerText = '插入光标处'; }, 2000);
                    };
                })(block, btn);

                block.appendChild(btn);
            }
            window.scrollTo(0, document.body.scrollHeight);
        }

        function appendBubble(role, htmlContent, id) {
            var container = document.getElementById('chat-container');
            if (!container) return;

            var newBubble = document.createElement('div');
            newBubble.id = id;
            newBubble.className = 'msg msg-' + role;
            newBubble.innerHTML = htmlContent;
            container.appendChild(newBubble);
            enhanceAndScroll();
        }

        function updateBubble(id, htmlContent) {
            var bubble = document.getElementById(id);
            if (bubble) {
                bubble.innerHTML = htmlContent;
                enhanceAndScroll();
            }
        }
    </script>
</head>
<body>
    <div id='chat-container'></div>
</body>
</html>";

        /// <summary>
        /// 初始化 AiChatHtmlWidget 控件的新实例。
        /// </summary>
        public AiChatHtmlWidget()
        {
            InitializeComponent();
            _webBrowser.DocumentText = _htmlTemplate;
            _webBrowser.DocumentCompleted += (s, e) => {
                // Append a welcome message or initial state if needed
                AppendMessage("ai", "你好！我能为你做些什么？");
            };
        }

        /// <summary>
        /// 清空聊天历史记录并重置视图。
        /// </summary>
        public void ClearHistory()
        {
            _messageHistory.Clear();
            // 重新加载空白模板，达到清屏的视觉效果
            if (_webBrowser != null)
            {
                _webBrowser.DocumentText = _htmlTemplate;
            }
        }

        private void InitializeComponent()
        {
            _webBrowser = new WebBrowser();
            _inputTextBox = new TextBox();
            _sendButton = new IconButton();
            _clearButton = new IconButton();
            _inputAreaPanel = new Panel();
            var containedInputPanel = new Panel();
            var topPaddingPanel = new Panel();

            _inputAreaPanel.SuspendLayout();
            containedInputPanel.SuspendLayout();
            this.SuspendLayout();

            // --- Overall Input Area Container ---
            _inputAreaPanel.Dock = DockStyle.Bottom;
            _inputAreaPanel.Height = 75;
            _inputAreaPanel.BackColor = Color.White;
            _inputAreaPanel.Padding = new Padding(0, 0, 10, 10);

            // --- Top Padding Panel ---
            topPaddingPanel.Dock = DockStyle.Top;
            topPaddingPanel.Height = 10;
            topPaddingPanel.BackColor = Color.White;

            // --- Clear History Button ---
            _clearButton.IconChar = IconChar.TrashAlt;
            _clearButton.IconSize = 18;
            _clearButton.IconColor = Color.Gray;
            _clearButton.FlatStyle = FlatStyle.Flat;
            _clearButton.FlatAppearance.BorderSize = 0;
            _clearButton.BackColor = Color.Transparent;
            _clearButton.ForeColor = Color.Gray;
            _clearButton.Text = "清空历史";
            _clearButton.ImageAlign = ContentAlignment.MiddleLeft;
            _clearButton.TextAlign = ContentAlignment.MiddleRight;
            _clearButton.Width = 80;
            _clearButton.Dock = DockStyle.Left;
            _clearButton.Click += new System.EventHandler(this._clearButton_Click);
            _clearButton.MouseEnter += new System.EventHandler(this._clearButton_MouseEnter);
            _clearButton.MouseLeave += new System.EventHandler(this._clearButton_MouseLeave);

            // --- Send Button ---
            _sendButton.IconChar = IconChar.PaperPlane;
            _sendButton.IconSize = 22;
            _sendButton.IconColor = Color.White;
            _sendButton.FlatStyle = FlatStyle.Flat;
            _sendButton.FlatAppearance.BorderSize = 0;
            _sendButton.BackColor = Color.FromArgb(0, 120, 215);
            _sendButton.ForeColor = Color.White;
            _sendButton.Text = "";
            _sendButton.Width = 60;
            _sendButton.Dock = DockStyle.Right;
            _sendButton.Click += new System.EventHandler(this._sendButton_Click);
            _sendButton.MouseEnter += new System.EventHandler(this._sendButton_MouseEnter);
            _sendButton.MouseLeave += new System.EventHandler(this._sendButton_MouseLeave);

            // --- Contained Input TextBox ---
            containedInputPanel.Dock = DockStyle.Fill;
            containedInputPanel.BackColor = Color.FromArgb(245, 245, 245);
            containedInputPanel.Padding = new Padding(8, 5, 8, 5);
            containedInputPanel.Controls.Add(_inputTextBox);

            _inputTextBox.Multiline = true;
            _inputTextBox.BorderStyle = BorderStyle.None;
            _inputTextBox.Dock = DockStyle.Fill;
            _inputTextBox.BackColor = Color.FromArgb(245, 245, 245);
            _inputTextBox.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(134)));
            _inputTextBox.Text = "输入测绘问题...";
            _inputTextBox.ForeColor = Color.Gray;
            _inputTextBox.Enter += new System.EventHandler(this._inputTextBox_Enter);
            _inputTextBox.Leave += new System.EventHandler(this._inputTextBox_Leave);
            _inputTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this._inputTextBox_KeyDown);

            // --- Assemble Input Area ---
            _inputAreaPanel.Controls.Add(containedInputPanel);
            _inputAreaPanel.Controls.Add(_clearButton);
            _inputAreaPanel.Controls.Add(_sendButton);
            _inputAreaPanel.Controls.Add(topPaddingPanel);

            // --- WebBrowser ---
            _webBrowser.Dock = DockStyle.Fill;
            _webBrowser.IsWebBrowserContextMenuEnabled = false;
            _webBrowser.AllowWebBrowserDrop = false;
            _webBrowser.ScriptErrorsSuppressed = true;
            _webBrowser.ObjectForScripting = this;

            // --- Main Control Assembly ---
            this.Controls.Add(_webBrowser);
            this.Controls.Add(_inputAreaPanel);

            containedInputPanel.ResumeLayout(false);
            containedInputPanel.PerformLayout();
            _inputAreaPanel.ResumeLayout(false);
            _inputAreaPanel.PerformLayout();
            this.ResumeLayout(false);
        }

        private void _clearButton_Click(object sender, EventArgs e)
        {
            ClearHistory();
        }

        private void _clearButton_MouseEnter(object sender, EventArgs e)
        {
            _clearButton.ForeColor = Color.DarkGray;
            _clearButton.IconColor = Color.DarkGray;
        }

        private void _clearButton_MouseLeave(object sender, EventArgs e)
        {
            _clearButton.ForeColor = Color.Gray;
            _clearButton.IconColor = Color.Gray;
        }

        private async void _sendButton_Click(object sender, EventArgs e)
        {
            await SendMessageAsync();
        }

        private void _sendButton_MouseEnter(object sender, EventArgs e)
        {
            _sendButton.BackColor = Color.FromArgb(0, 100, 200);
        }

        private void _sendButton_MouseLeave(object sender, EventArgs e)
        {
            _sendButton.BackColor = Color.FromArgb(0, 120, 215);
        }

        private void _inputTextBox_Enter(object sender, EventArgs e)
        {
            if (_inputTextBox.Text == "输入测绘问题...")
            {
                _inputTextBox.Text = "";
                _inputTextBox.ForeColor = Color.Black;
            }
        }

        private void _inputTextBox_Leave(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_inputTextBox.Text))
            {
                _inputTextBox.Text = "输入测绘问题...";
                _inputTextBox.ForeColor = Color.Gray;
            }
        }

        private void _inputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && e.Modifiers == Keys.None)
            {
                e.SuppressKeyPress = true;
                _sendButton.PerformClick();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                OnFocusEditorRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private async Task SendMessageAsync()
        {
            var message = _inputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(message) || (_inputTextBox.Text == "输入测绘问题..." && _inputTextBox.ForeColor == Color.Gray)) return;

            _sendButton.Enabled = false;
            _inputTextBox.Enabled = false;

            AppendMessage("user", message);
            _inputTextBox.Clear();

            var thinkingMessageId = AppendMessage("ai", "思考中...");

            try
            {
                string responseText = "";
                if (ConnectionMode == AiConnectionMode.LocalServer)
                {
                    var request = new { message = message };
                    var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync(ServerApiUrl, content);
                    response.EnsureSuccessStatusCode();
                    responseText = await response.Content.ReadAsStringAsync();
                }
                else // DirectOpenAI
                {
                    var messages = new System.Collections.Generic.List<object>();

                    if (!string.IsNullOrWhiteSpace(SystemPrompt))
                    {
                        messages.Add(new { role = "system", content = SystemPrompt });
                    }
                    messages.AddRange(_messageHistory);
                    messages.Add(new { role = "user", content = message });

                    var payload = new
                    {
                        model = DirectApiModel,
                        messages = messages
                    };

                    _messageHistory.Add(new { role = "user", content = message });

                    var requestMsg = new HttpRequestMessage(HttpMethod.Post, DirectApiBaseUrl.TrimEnd('/') + "/chat/completions");
                    requestMsg.Headers.Add("Authorization", $"Bearer {DirectApiKey}");
                    requestMsg.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                    var response = await _httpClient.SendAsync(requestMsg);
                    var responseString = await response.Content.ReadAsStringAsync();
                    response.EnsureSuccessStatusCode();

                    dynamic result = JsonConvert.DeserializeObject(responseString);
                    responseText = result.choices[0].message.content;

                    _messageHistory.Add(new { role = "ai", content = responseText });
                }

                UpdateMessage(thinkingMessageId, responseText);
            }
            catch (Exception ex)
            {
                if (ConnectionMode == AiConnectionMode.DirectOpenAI && _messageHistory.Count > 0)
                {
                    _messageHistory.RemoveAt(_messageHistory.Count - 1);
                }
                UpdateMessage(thinkingMessageId, $"出现错误: {ex.Message}");
            }
            finally
            {
                _sendButton.Enabled = true;
                _inputTextBox.Enabled = true;
                _inputTextBox.Focus();
            }
        }

        /// <summary>
        /// 在聊天视图中追加一个新的消息气泡。
        /// </summary>
        /// <param name="role">发送者角色，'user' 或 'ai'。</param>
        /// <param name="markdownText">Markdown 格式的消息内容。</param>
        /// <returns>新消息气泡的唯一 ID。</returns>
        public string AppendMessage(string role, string markdownText)
        {
            if (_webBrowser.Document == null) return null;

            if (_webBrowser.InvokeRequired)
            {
                return (string)_webBrowser.Invoke(new Func<string, string, string>(AppendMessage), role, markdownText);
            }

            var messageId = "msg-" + Guid.NewGuid().ToString("N");
            var htmlContent = MarkdownRenderHelper.ConvertToHtml(markdownText);
            _webBrowser.Document.InvokeScript("appendBubble", new object[] { role, htmlContent, messageId });
            return messageId;
        }

        private void UpdateMessage(string messageId, string newMarkdownText)
        {
            if (string.IsNullOrEmpty(messageId) || _webBrowser.Document == null) return;

            if (_webBrowser.InvokeRequired)
            {
                _webBrowser.Invoke(new Action(() => UpdateMessage(messageId, newMarkdownText)));
                return;
            }
            
            var htmlContent = MarkdownRenderHelper.ConvertToHtml(newMarkdownText);
            _webBrowser.Document.InvokeScript("updateBubble", new object[] { messageId, htmlContent });
        }

        /// <summary>
        /// 由 JavaScript 调用，用于触发代码插入事件。
        /// </summary>
        /// <param name="code">从网页中提取的代码文本。</param>
        public void JsInvokeInsertCode(string code)
        {
            // This method is called from JavaScript
            OnInsertCodeRequested?.Invoke(this, new InsertCodeEventArgs { Code = code, ReplaceSelection = false });
        }

        /// <summary>
        /// 从外部源发送消息到聊天窗口，模拟用户输入。
        /// </summary>
        /// <param name="message">要发送的消息文本。</param>
        public void SendExternalMessage(string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => SendExternalMessage(message)));
                return;
            }

            _inputTextBox.Text = message;
            _inputTextBox.ForeColor = Color.Black;
            _sendButton.PerformClick();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inputTextBox?.Dispose();
                _sendButton?.Dispose();
                _clearButton?.Dispose();
                _inputAreaPanel?.Dispose();
                _webBrowser?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
