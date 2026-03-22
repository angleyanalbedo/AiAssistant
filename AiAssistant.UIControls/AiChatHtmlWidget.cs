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
        private Panel containedInputPanel;
        private Panel topPaddingPanel;

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
            this._webBrowser = new System.Windows.Forms.WebBrowser();
            this._inputTextBox = new System.Windows.Forms.TextBox();
            this._sendButton = new FontAwesome.Sharp.IconButton();
            this._clearButton = new FontAwesome.Sharp.IconButton();
            this._inputAreaPanel = new System.Windows.Forms.Panel();
            this.containedInputPanel = new System.Windows.Forms.Panel();
            this.topPaddingPanel = new System.Windows.Forms.Panel();
            this._inputAreaPanel.SuspendLayout();
            this.containedInputPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // _webBrowser
            // 
            this._webBrowser.AllowWebBrowserDrop = false;
            this._webBrowser.Dock = System.Windows.Forms.DockStyle.Fill;
            this._webBrowser.IsWebBrowserContextMenuEnabled = false;
            this._webBrowser.Location = new System.Drawing.Point(0, 0);
            this._webBrowser.Name = "_webBrowser";
            this._webBrowser.ScriptErrorsSuppressed = true;
            this._webBrowser.Size = new System.Drawing.Size(150, 75);
            this._webBrowser.TabIndex = 0;
            // 
            // _inputTextBox
            // 
            this._inputTextBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(245)))), ((int)(((byte)(245)))));
            this._inputTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this._inputTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this._inputTextBox.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this._inputTextBox.ForeColor = System.Drawing.Color.Gray;
            this._inputTextBox.Location = new System.Drawing.Point(8, 5);
            this._inputTextBox.Multiline = true;
            this._inputTextBox.Name = "_inputTextBox";
            this._inputTextBox.Size = new System.Drawing.Size(0, 45);
            this._inputTextBox.TabIndex = 0;
            this._inputTextBox.Text = "输入测绘问题...";
            this._inputTextBox.Enter += new System.EventHandler(this._inputTextBox_Enter);
            this._inputTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this._inputTextBox_KeyDown);
            this._inputTextBox.Leave += new System.EventHandler(this._inputTextBox_Leave);
            // 
            // _sendButton
            // 
            this._sendButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this._sendButton.Dock = System.Windows.Forms.DockStyle.Right;
            this._sendButton.FlatAppearance.BorderSize = 0;
            this._sendButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._sendButton.ForeColor = System.Drawing.Color.White;
            this._sendButton.IconChar = FontAwesome.Sharp.IconChar.PaperPlane;
            this._sendButton.IconColor = System.Drawing.Color.White;
            this._sendButton.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this._sendButton.IconSize = 22;
            this._sendButton.Location = new System.Drawing.Point(80, 10);
            this._sendButton.Name = "_sendButton";
            this._sendButton.Size = new System.Drawing.Size(60, 55);
            this._sendButton.TabIndex = 2;
            this._sendButton.UseVisualStyleBackColor = false;
            this._sendButton.Click += new System.EventHandler(this._sendButton_Click);
            this._sendButton.MouseEnter += new System.EventHandler(this._sendButton_MouseEnter);
            this._sendButton.MouseLeave += new System.EventHandler(this._sendButton_MouseLeave);
            // 
            // _clearButton
            // 
            this._clearButton.BackColor = System.Drawing.Color.Transparent;
            this._clearButton.Dock = System.Windows.Forms.DockStyle.Left;
            this._clearButton.FlatAppearance.BorderSize = 0;
            this._clearButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._clearButton.ForeColor = System.Drawing.Color.Gray;
            this._clearButton.IconChar = FontAwesome.Sharp.IconChar.TrashAlt;
            this._clearButton.IconColor = System.Drawing.Color.Gray;
            this._clearButton.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this._clearButton.IconSize = 18;
            this._clearButton.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this._clearButton.Location = new System.Drawing.Point(0, 10);
            this._clearButton.Name = "_clearButton";
            this._clearButton.Size = new System.Drawing.Size(80, 55);
            this._clearButton.TabIndex = 1;
            this._clearButton.Text = "清空历史";
            this._clearButton.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._clearButton.UseVisualStyleBackColor = false;
            this._clearButton.Click += new System.EventHandler(this._clearButton_Click);
            this._clearButton.MouseEnter += new System.EventHandler(this._clearButton_MouseEnter);
            this._clearButton.MouseLeave += new System.EventHandler(this._clearButton_MouseLeave);
            // 
            // _inputAreaPanel
            // 
            this._inputAreaPanel.BackColor = System.Drawing.Color.White;
            this._inputAreaPanel.Controls.Add(this.containedInputPanel);
            this._inputAreaPanel.Controls.Add(this._clearButton);
            this._inputAreaPanel.Controls.Add(this._sendButton);
            this._inputAreaPanel.Controls.Add(this.topPaddingPanel);
            this._inputAreaPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._inputAreaPanel.Location = new System.Drawing.Point(0, 75);
            this._inputAreaPanel.Name = "_inputAreaPanel";
            this._inputAreaPanel.Padding = new System.Windows.Forms.Padding(0, 0, 10, 10);
            this._inputAreaPanel.Size = new System.Drawing.Size(150, 75);
            this._inputAreaPanel.TabIndex = 1;
            // 
            // containedInputPanel
            // 
            this.containedInputPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(245)))), ((int)(((byte)(245)))));
            this.containedInputPanel.Controls.Add(this._inputTextBox);
            this.containedInputPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.containedInputPanel.Location = new System.Drawing.Point(80, 10);
            this.containedInputPanel.Name = "containedInputPanel";
            this.containedInputPanel.Padding = new System.Windows.Forms.Padding(8, 5, 8, 5);
            this.containedInputPanel.Size = new System.Drawing.Size(0, 55);
            this.containedInputPanel.TabIndex = 0;
            // 
            // topPaddingPanel
            // 
            this.topPaddingPanel.BackColor = System.Drawing.Color.White;
            this.topPaddingPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.topPaddingPanel.Location = new System.Drawing.Point(0, 0);
            this.topPaddingPanel.Name = "topPaddingPanel";
            this.topPaddingPanel.Size = new System.Drawing.Size(140, 10);
            this.topPaddingPanel.TabIndex = 3;
            // 
            // AiChatHtmlWidget
            // 
            this.Controls.Add(this._webBrowser);
            this.Controls.Add(this._inputAreaPanel);
            this.Name = "AiChatHtmlWidget";
            this._inputAreaPanel.ResumeLayout(false);
            this.containedInputPanel.ResumeLayout(false);
            this.containedInputPanel.PerformLayout();
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
