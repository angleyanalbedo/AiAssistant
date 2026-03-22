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
    /// <summary>
    /// 一个基于 WebView2 的现代 WinForms 聊天控件，具备 Markdown 渲染和流式响应能力。
    /// </summary>
    public class AiChatWebViewWidget : UserControl
    {
        /// <summary>
        /// 当用户点击代码块中的“插入”按钮时触发。
        /// </summary>
        public event EventHandler<InsertCodeEventArgs> OnInsertCodeRequested;
        /// <summary>
        /// 当用户在输入框中按下 Escape 键时触发，请求将焦点切换回主编辑器。
        /// </summary>
        public event EventHandler OnFocusEditorRequested;

        private WebView2 _chatWebView;
        private TextBox _inputTextBox;
        private Button _sendButton;
        private Button _clearButton;
        private Panel _inputAreaPanel;
        private Panel _topBorderPanel;

        private static readonly HttpClient _httpClient = new HttpClient();
        private bool _isWebViewReady = false;
        private System.Collections.Generic.List<object> _messageHistory = new System.Collections.Generic.List<object>();

        // API Properties
        /// <summary>
        /// 获取或设置发送给 AI 模型的系统提示，用于定义其角色和行为。
        /// </summary>
        public string SystemPrompt { get; set; } = "你是一个专业的AI编程助手。请提供准确、简洁的代码和解释。";
        /// <summary>
        /// 获取或设置连接模式，可以是连接本地服务器或直连 OpenAI 兼容的 API。
        /// </summary>
        public AiConnectionMode ConnectionMode { get; set; } = AiConnectionMode.LocalServer;
        /// <summary>
        /// 获取或设置本地服务器的 API 地址。当 ConnectionMode 为 LocalServer 时使用。
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

        /// <summary>
        /// 清空聊天历史记录，并显示一条确认消息。
        /// </summary>
        public void ClearHistory() { _messageHistory.Clear(); AppendMessage("ai", "上下文已清空，我们可以开始新的对话了。"); }

        private string _htmlTemplate = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>__CSS_PLACEHOLDER__</style>
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
</head>
<body class=""markdown-body"">
    <div id='chat-container'></div>
    <script>__JS_PLACEHOLDER__</script>
    <script>
        document.addEventListener('DOMContentLoaded', () => { hljs.highlightAll(); });

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
</body>
</html>";

        /// <summary>
        /// 初始化 AiChatWebViewWidget 控件的新实例。
        /// </summary>
        public AiChatWebViewWidget()
        {
            InitializeComponent();

            var css = GetEmbeddedResource("github.min.css");
            var js = GetEmbeddedResource("highlight.min.js");
            _htmlTemplate = _htmlTemplate
                .Replace("__CSS_PLACEHOLDER__", css)
                .Replace("__JS_PLACEHOLDER__", js);

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
            _clearButton.Click += new System.EventHandler(this._clearButton_Click);

            // Send Button
            _sendButton.Dock = DockStyle.Right;
            _sendButton.Width = 70;
            _sendButton.Text = "发送";
            _sendButton.FlatStyle = FlatStyle.Flat;
            _sendButton.FlatAppearance.BorderSize = 0;
            _sendButton.BackColor = Color.FromArgb(0, 120, 215);
            _sendButton.ForeColor = Color.White;
            _sendButton.Click += new System.EventHandler(this._sendButton_Click);

            // Input TextBox
            _inputTextBox.Multiline = true;
            _inputTextBox.BorderStyle = BorderStyle.None;
            _inputTextBox.Dock = DockStyle.Fill;
            _inputTextBox.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(134)));
            _inputTextBox.Text = "输入消息...";
            _inputTextBox.ForeColor = Color.Gray;
            _inputTextBox.Enter += new System.EventHandler(this._inputTextBox_Enter);
            _inputTextBox.Leave += new System.EventHandler(this._inputTextBox_Leave);
            _inputTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this._inputTextBox_KeyDown);

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

        private void _clearButton_Click(object sender, EventArgs e)
        {
            ClearHistory();
        }

        private async void _sendButton_Click(object sender, EventArgs e)
        {
            await SendMessageAsync();
        }

        private void _inputTextBox_Enter(object sender, EventArgs e)
        {
            if (_inputTextBox.Text == "输入消息...")
            {
                _inputTextBox.Text = "";
                _inputTextBox.ForeColor = Color.Black;
            }
        }

        private void _inputTextBox_Leave(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_inputTextBox.Text))
            {
                _inputTextBox.Text = "输入消息...";
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
            if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                OnFocusEditorRequested?.Invoke(this, EventArgs.Empty);
            }
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
            if (string.IsNullOrEmpty(message) || message == "输入消息..." || !_isWebViewReady) return;

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

        /// <summary>
        /// 在聊天视图中追加一个新的消息气泡。
        /// </summary>
        /// <param name="role">发送者角色，'user' 或 'ai'。</param>
        /// <param name="markdownText">Markdown 格式的消息内容。</param>
        /// <returns>新消息气泡的唯一 ID。</returns>
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

        /// <summary>
        /// 将焦点设置到消息输入框。
        /// </summary>
        public void FocusInput() { _inputTextBox.Focus(); }

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

        private string GetEmbeddedResource(string fileName)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            // 注意：资源路径通常是 [命名空间].[文件夹].[文件名]
            string resourcePath = "AiAssistant.UIControls.Resources." + fileName;
            using (var stream = assembly.GetManifestResourceStream(resourcePath))
            {
                if (stream == null) return string.Empty;
                using (var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
