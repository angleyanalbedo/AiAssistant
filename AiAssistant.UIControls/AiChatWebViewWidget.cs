using AiAssistant.UIControls.Utils;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using System;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace AiAssistant.UIControls
{
    public class AiChatWebViewWidget : UserControl
    {
        public event EventHandler<InsertCodeEventArgs> OnInsertCodeRequested;

        private WebView2 _chatWebView;
        private TextBox _inputTextBox;
        private Button _sendButton;
        private Button _clearButton;
        private Panel _inputAreaPanel;
        private Panel _topBorderPanel;
        private const string PlaceholderText = "输入消息...";

        private static readonly HttpClient _httpClient = new HttpClient();
        private bool _isWebViewReady = false;
        private System.Collections.Generic.List<object> _messageHistory = new System.Collections.Generic.List<object>();

        // API Properties
        public string SystemPrompt { get; set; } = "你是一个专业的AI编程助手。请提供准确、简洁的代码和解释。";
        public AiConnectionMode ConnectionMode { get; set; } = AiConnectionMode.LocalServer;
        public string ServerApiUrl { get; set; } = "http://localhost:5000/api/chat";
        public string DirectApiBaseUrl { get; set; } = "https://api.openai.com/v1";
        public string DirectApiKey { get; set; } = "";
        public string DirectApiModel { get; set; } = "gpt-3.5-turbo";

        public void ClearHistory() { _messageHistory.Clear(); AppendMessage("ai", "上下文已清空，我们可以开始新的对话了。"); }

        private readonly string _htmlTemplate = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <link rel=""stylesheet"" href=""https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/styles/github.min.css"">
    <script src=""https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/highlight.min.js""></script>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, 'Open Sans', 'Helvetica Neue', sans-serif; margin: 0; padding: 10px; background-color: #F0F0F0; overflow-y: scroll; }
        #chat-container { display: flex; flex-direction: column; gap: 10px; }
        .msg { max-width: 80%; padding: 10px 15px; border-radius: 18px; line-height: 1.5; word-wrap: break-word; box-shadow: 0 1px 2px rgba(0,0,0,0.1); }
        .msg-user { align-self: flex-end; background-color: #DCF8C6; color: #000; }
        .msg-ai { align-self: flex-start; background-color: #FFF; color: #000; border: 1px solid #EAEAEA; }
        pre { position: relative; background-color: #F4F4F4; border: 1px solid #DDD; border-radius: 5px; padding: 10px; font-family: Consolas, 'Courier New', monospace; white-space: pre-wrap; word-wrap: break-word; }
        .insert-btn {
            position: absolute;
            top: 5px;
            right: 5px;
            padding: 4px 8px;
            cursor: pointer;
            background-color: #888;
            color: white;
            border: none;
            border-radius: 3px;
            font-size: 12px;
            display: none;
        }
        pre:hover .insert-btn { display: block; }
        code { font-family: Consolas, 'Courier New', monospace; }
        p { margin: 0 0 10px 0; }
        p:last-child { margin-bottom: 0; }
        table { border-collapse: collapse; width: 100%; margin: 10px 0; background-color: #fff; box-shadow: 0 1px 2px rgba(0,0,0,0.1); }
        th, td { border: 1px solid #ccc; padding: 8px 12px; text-align: left; }
        th { background-color: #f0f0f0; font-weight: bold; }
        hr { height: 1px; border: none; background-color: #E5E7EB; margin: 16px 0; }
    </style>
    <script>
        function enhanceCodeBlocks() {
            // 1. Trigger syntax highlighting
            hljs.highlightAll();

            // 2. Iterate over all code blocks to add the 'insert' button
            document.querySelectorAll('pre').forEach((block) => {
                // Prevent adding buttons multiple times
                if (block.hasAttribute('data-btn-added')) return;
                block.setAttribute('data-btn-added', 'true');

                let btn = document.createElement('button');
                btn.innerText = '插入光标处';
                btn.className = 'insert-btn';

                btn.onclick = function () {
                    // Extract pure code text
                    let codeText = block.querySelector('code').innerText;
                    // Send JSON message to C# using WebView2 native API
                    window.chrome.webview.postMessage(JSON.stringify({ action: 'insert', code: codeText }));
                    btn.innerText = '已触发!';
                    setTimeout(() => btn.innerText = '插入光标处', 2000);
                };
                block.appendChild(btn);
            });
        }

        function appendBubble(role, htmlContent, id) {
            const container = document.getElementById('chat-container');
            const bubble = document.createElement('div');
            bubble.id = id;
            bubble.className = `msg msg-${role}`;
            bubble.innerHTML = htmlContent;
            container.appendChild(bubble);
            
            enhanceCodeBlocks();
            window.scrollTo(0, document.body.scrollHeight);
        }

        function updateBubble(id, htmlContent) {
            const bubble = document.getElementById(id);
            if (bubble) {
                bubble.innerHTML = htmlContent;
                enhanceCodeBlocks();
                window.scrollTo(0, document.body.scrollHeight);
            }
        }

        function updateLastBubble(htmlContent) {
            var aiBubbles = document.getElementsByClassName('msg-ai');
            if (aiBubbles.length > 0) {
                var lastBubble = aiBubbles[aiBubbles.length - 1];
                lastBubble.innerHTML = htmlContent;
           
                // 为新生成的代码块添加高亮和插入按钮
                hljs.highlightAll();
                var blocks = lastBubble.getElementsByTagName('pre');
                for (var i = 0; i < blocks.length; i++) {
                    if (blocks[i].getAttribute('data-btn-added') === 'true') continue;
                    blocks[i].setAttribute('data-btn-added', 'true');
                    blocks[i].style.position = 'relative';
                    var btn = document.createElement('button');
                    btn.innerText = '插入光标处';
                    btn.className = 'insert-btn';
                    (function(btn, block) {
                        btn.onclick = function() {
                            var codeText = block.querySelector('code').innerText;
                            window.chrome.webview.postMessage(JSON.stringify({ action: 'insert', code: codeText }));
                            btn.innerText = '已触发!';
                            setTimeout(function(){ btn.innerText = '插入光标处'; }, 2000);
                        };
                    })(btn, blocks[i]);
                    blocks[i].appendChild(btn);
                }
                window.scrollTo(0, document.body.scrollHeight);
            }
        }
    </script>
</head>
<body>
    <div id='chat-container'></div>
</body>
</html>";

        public AiChatWebViewWidget()
        {
            InitializeComponent();
            _chatWebView.EnsureCoreWebView2Async(null);
        }

        private void InitializeComponent()
        {
            _chatWebView = new WebView2();
            _inputTextBox = new TextBox();
            _sendButton = new Button();
            _clearButton = new Button();
            _inputAreaPanel = new Panel();
            _topBorderPanel = new Panel();

            _inputAreaPanel.SuspendLayout();
            this.SuspendLayout();

            // Input Area Panel (reused from AiChatHtmlWidget)
            _inputAreaPanel.Dock = DockStyle.Bottom;
            _inputAreaPanel.Height = 70;
            _inputAreaPanel.Padding = new Padding(5);
            _inputAreaPanel.BackColor = Color.White;
            _inputAreaPanel.Controls.Add(_topBorderPanel);
            _inputAreaPanel.Controls.Add(_inputTextBox);
            _inputAreaPanel.Controls.Add(_sendButton);
            _inputAreaPanel.Controls.Add(_clearButton);

            // Top Border
            _topBorderPanel.Dock = DockStyle.Top;
            _topBorderPanel.Height = 1;
            _topBorderPanel.BackColor = Color.LightGray;

            // Clear Button
            _clearButton.Dock = DockStyle.Left;
            _clearButton.Width = 50;
            _clearButton.Text = "清空";
            _clearButton.FlatStyle = FlatStyle.Flat;
            _clearButton.FlatAppearance.BorderSize = 0;
            _clearButton.BackColor = Color.FromArgb(224, 224, 224);
            _clearButton.ForeColor = Color.Black;
            _clearButton.Click += (s, e) => ClearHistory();

            // Send Button
            _sendButton.Dock = DockStyle.Right;
            _sendButton.Width = 70;
            _sendButton.Text = "发送";
            _sendButton.FlatStyle = FlatStyle.Flat;
            _sendButton.FlatAppearance.BorderSize = 0;
            _sendButton.BackColor = Color.FromArgb(0, 120, 215);
            _sendButton.ForeColor = Color.White;
            _sendButton.Click += async (s, e) => await SendMessageAsync();

            // Input TextBox
            _inputTextBox.Multiline = true;
            _inputTextBox.BorderStyle = BorderStyle.None;
            _inputTextBox.Dock = DockStyle.Fill;
            _inputTextBox.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(134)));
            _inputTextBox.Text = PlaceholderText;
            _inputTextBox.ForeColor = Color.Gray;
            _inputTextBox.Enter += (s, e) => { if (_inputTextBox.Text == PlaceholderText) { _inputTextBox.Text = ""; _inputTextBox.ForeColor = Color.Black; } };
            _inputTextBox.Leave += (s, e) => { if (string.IsNullOrWhiteSpace(_inputTextBox.Text)) { _inputTextBox.Text = PlaceholderText; _inputTextBox.ForeColor = Color.Gray; } };
            _inputTextBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter && e.Modifiers == Keys.None) { e.SuppressKeyPress = true; _sendButton.PerformClick(); } };

            // WebView2
            _chatWebView.Dock = DockStyle.Fill;
            _chatWebView.CoreWebView2InitializationCompleted += OnWebViewReady;

            // Main Control
            this.Controls.Add(_chatWebView);
            this.Controls.Add(_inputAreaPanel);
            _inputAreaPanel.ResumeLayout(false);
            _inputAreaPanel.PerformLayout();
            this.ResumeLayout(false);
        }

        private void OnWebViewReady(object sender, Microsoft.Web.WebView2.Core.CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                _isWebViewReady = true;
                _chatWebView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
                _chatWebView.CoreWebView2.Settings.IsScriptEnabled = true;
                _chatWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                _chatWebView.NavigateToString(_htmlTemplate);
                
                // It's better to append the welcome message after the navigation is complete.
                _chatWebView.NavigationCompleted += (s, args) => {
                    if (args.IsSuccess)
                    {
                        AppendMessage("ai", "你好！我能为你做些什么？");
                    }
                };
            }
        }

        private void OnWebMessageReceived(object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string jsonMessage = e.TryGetWebMessageAsString();
                if (string.IsNullOrEmpty(jsonMessage)) return;

                dynamic payload = JsonConvert.DeserializeObject<dynamic>(jsonMessage);
                if (payload.action == "insert")
                {
                    string codeToInsert = payload.code;
                    // Trigger the public event to notify the host form
                    OnInsertCodeRequested?.Invoke(this, new InsertCodeEventArgs { Code = codeToInsert, ReplaceSelection = false });
                }
            }
            catch (Exception)
            {
                // Ignore malformed messages or logging
            }
        }

        private async Task SendMessageAsync()
        {
            var message = _inputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(message) || message == PlaceholderText || !_isWebViewReady) return;

            _sendButton.Enabled = false;
            _inputTextBox.Enabled = false;

            AppendMessage("user", message);
            _inputTextBox.Clear();

            string thinkingMessageId = null;

            try
            {
                if (ConnectionMode == AiConnectionMode.LocalServer)
                {
                    thinkingMessageId = AppendMessage("ai", "思考中...");
                    var request = new { message = message };
                    var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync(ServerApiUrl, content);
                    response.EnsureSuccessStatusCode();
                    string responseText = await response.Content.ReadAsStringAsync();
                    UpdateMessage(thinkingMessageId, responseText);
                }
                else // DirectOpenAI with streaming and history
                {
                    thinkingMessageId = AppendMessage("ai", "..."); // Create an empty bubble first

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
                        messages = messages,
                        stream = true
                    };

                    _messageHistory.Add(new { role = "user", content = message });

                    var request = new HttpRequestMessage(HttpMethod.Post, DirectApiBaseUrl.TrimEnd('/') + "/chat/completions");
                    request.Headers.Add("Authorization", $"Bearer {DirectApiKey}");
                    request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                    string fullAiResponse = "";

                    using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var reader = new System.IO.StreamReader(stream))
                        {
                            while (!reader.EndOfStream)
                            {
                                var line = await reader.ReadLineAsync();
                                if (string.IsNullOrWhiteSpace(line)) continue;

                                if (line.StartsWith("data: "))
                                {
                                    var data = line.Substring(6);
                                    if (data.Trim() == "[DONE]") break;

                                    try
                                    {
                                        dynamic json = JsonConvert.DeserializeObject(data);
                                        string delta = json.choices[0].delta.content;
                                        if (!string.IsNullOrEmpty(delta))
                                        {
                                            fullAiResponse += delta;
                                            string html = MarkdownRenderHelper.ConvertToHtml(fullAiResponse);
                                            var script = $"updateLastBubble('{HttpUtility.JavaScriptStringEncode(html)}')";

                                            if (this.InvokeRequired)
                                            {
                                                this.Invoke(new Action(async () => await _chatWebView.ExecuteScriptAsync(script)));
                                            }
                                            else
                                            {
                                                await _chatWebView.ExecuteScriptAsync(script);
                                            }
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        /* Ignore JSON parsing errors for incomplete chunks */
                                    }
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(fullAiResponse))
                    {
                        _messageHistory.Add(new { role = "assistant", content = fullAiResponse });
                    }
                }
            }
            catch (Exception ex)
            {
                if (thinkingMessageId != null)
                {
                    UpdateMessage(thinkingMessageId, $"出现错误: {ex.Message}");
                }
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
            if (!_isWebViewReady) return null;
            var messageId = "msg-" + Guid.NewGuid().ToString("N");
            var htmlContent = MarkdownRenderHelper.ConvertToHtml(markdownText);
            var script = $"appendBubble('{role}', '{HttpUtility.JavaScriptStringEncode(htmlContent)}', '{messageId}');";
            
            // Execute script on UI thread
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(async () => await _chatWebView.ExecuteScriptAsync(script)));
            }
            else
            {
                _chatWebView.ExecuteScriptAsync(script);
            }

            return messageId;
        }

        private void UpdateMessage(string messageId, string newMarkdownText)
        {
            if (string.IsNullOrEmpty(messageId) || !_isWebViewReady) return;
            var htmlContent = MarkdownRenderHelper.ConvertToHtml(newMarkdownText);
            var script = $"updateBubble('{messageId}', '{HttpUtility.JavaScriptStringEncode(htmlContent)}');";

            // Execute script on UI thread
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(async () => await _chatWebView.ExecuteScriptAsync(script)));
            }
            else
            {
                _chatWebView.ExecuteScriptAsync(script);
            }
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
                _inputAreaPanel?.Dispose();
                _topBorderPanel?.Dispose();
                _chatWebView?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
