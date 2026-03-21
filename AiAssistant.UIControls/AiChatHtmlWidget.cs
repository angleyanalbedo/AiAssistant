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
    [ComVisible(true)]
    public class AiChatHtmlWidget : UserControl
    {
        public event EventHandler<InsertCodeEventArgs> OnInsertCodeRequested;
        public event EventHandler OnFocusEditorRequested;

        private WebBrowser _webBrowser;
        private TextBox _inputTextBox;
        private IconButton _sendButton;
        private IconButton _clearButton;
        private Panel _inputAreaPanel;
        private const string PlaceholderText = "输入测绘问题...";

        private static readonly HttpClient _httpClient = new HttpClient();

        // API Properties
        public AiConnectionMode ConnectionMode { get; set; } = AiConnectionMode.LocalServer;
        public string ServerApiUrl { get; set; } = "http://localhost:5000/api/chat";
        public string DirectApiBaseUrl { get; set; } = "https://api.openai.com/v1";
        public string DirectApiKey { get; set; } = "";
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

        public AiChatHtmlWidget()
        {
            InitializeComponent();
            _webBrowser.DocumentText = _htmlTemplate;
            _webBrowser.DocumentCompleted += (s, e) => {
                // Append a welcome message or initial state if needed
                AppendMessage("ai", "你好！我能为你做些什么？");
            };
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
            _clearButton.Click += (s, e) => { /* No history management in this widget */ };
            _clearButton.MouseEnter += (s, e) => { _clearButton.ForeColor = Color.DarkGray; _clearButton.IconColor = Color.DarkGray; };
            _clearButton.MouseLeave += (s, e) => { _clearButton.ForeColor = Color.Gray; _clearButton.IconColor = Color.Gray; };

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
            _sendButton.Click += async (s, e) => await SendMessageAsync();
            _sendButton.MouseEnter += (s, e) => { _sendButton.BackColor = Color.FromArgb(0, 100, 200); };
            _sendButton.MouseLeave += (s, e) => { _sendButton.BackColor = Color.FromArgb(0, 120, 215); };

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
            _inputTextBox.Text = PlaceholderText;
            _inputTextBox.ForeColor = Color.Gray;
            _inputTextBox.Enter += (s, e) => { if (_inputTextBox.Text == PlaceholderText) { _inputTextBox.Text = ""; _inputTextBox.ForeColor = Color.Black; } };
            _inputTextBox.Leave += (s, e) => { if (string.IsNullOrWhiteSpace(_inputTextBox.Text)) { _inputTextBox.Text = PlaceholderText; _inputTextBox.ForeColor = Color.Gray; } };
            _inputTextBox.KeyDown += (s, e) =>
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
            };

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

        private async Task SendMessageAsync()
        {
            var message = _inputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(message) || (_inputTextBox.Text == PlaceholderText && _inputTextBox.ForeColor == Color.Gray)) return;

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
                    object payload;
                    string requestUrl;
                    var requestMsg = new HttpRequestMessage();

                    if (DirectApiBaseUrl.Contains("googleapis.com"))
                    {
                        payload = new { contents = new[] { new { parts = new[] { new { text = message } } } } };
                        requestUrl = $"{DirectApiBaseUrl.TrimEnd('/')}/models/{DirectApiModel}:generateContent?key={DirectApiKey}";
                        requestMsg.Method = HttpMethod.Post;
                        requestMsg.RequestUri = new Uri(requestUrl);
                    }
                    else
                    {
                        payload = new
                        {
                            model = DirectApiModel,
                            messages = new[] { new { role = "user", content = message } }
                        };
                        requestUrl = DirectApiBaseUrl.TrimEnd('/') + "/chat/completions";
                        requestMsg.Method = HttpMethod.Post;
                        requestMsg.RequestUri = new Uri(requestUrl);
                        requestMsg.Headers.Add("Authorization", $"Bearer {DirectApiKey}");
                    }

                    requestMsg.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                    var response = await _httpClient.SendAsync(requestMsg);
                    var responseString = await response.Content.ReadAsStringAsync();
                    response.EnsureSuccessStatusCode();

                    dynamic result = JsonConvert.DeserializeObject(responseString);
                    if (DirectApiBaseUrl.Contains("googleapis.com"))
                    {
                        responseText = result.candidates[0].content.parts[0].text;
                    }
                    else
                    {
                        responseText = result.choices[0].message.content;
                    }
                }

                UpdateMessage(thinkingMessageId, responseText);
            }
            catch (Exception ex)
            {
                UpdateMessage(thinkingMessageId, $"出现错误: {ex.Message}");
            }
            finally
            {
                _sendButton.Enabled = true;
                _inputTextBox.Enabled = true;
                _inputTextBox.Focus();
            }
        }

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

        public void JsInvokeInsertCode(string code)
        {
            // This method is called from JavaScript
            OnInsertCodeRequested?.Invoke(this, new InsertCodeEventArgs { Code = code, ReplaceSelection = false });
        }

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
