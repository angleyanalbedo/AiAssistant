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
        private Panel _inputAreaPanel;
        private Panel _topBorderPanel;
        private const string PlaceholderText = "输入消息...";

        private static readonly HttpClient _httpClient = new HttpClient();
        private bool _isWebViewReady = false;

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
    <meta charset='UTF-8'>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, 'Open Sans', 'Helvetica Neue', sans-serif; margin: 0; padding: 10px; background-color: #F0F0F0; overflow-y: scroll; }
        #chat-container { display: flex; flex-direction: column; gap: 10px; }
        .msg { max-width: 80%; padding: 10px 15px; border-radius: 18px; line-height: 1.5; word-wrap: break-word; box-shadow: 0 1px 2px rgba(0,0,0,0.1); }
        .msg-user { align-self: flex-end; background-color: #DCF8C6; color: #000; }
        .msg-ai { align-self: flex-start; background-color: #FFF; color: #000; border: 1px solid #EAEAEA; }
        pre { position: relative; background-color: #F4F4F4; border: 1px solid #DDD; border-radius: 5px; padding: 10px; font-family: Consolas, 'Courier New', monospace; white-space: pre-wrap; word-wrap: break-word; }
        .code-actions { position: absolute; top: 5px; right: 5px; display: none; background-color: #F4F4F4; padding: 2px;}
        pre:hover .code-actions { display: block; }
        .code-actions button { background-color: #888; color: white; border: none; border-radius: 3px; padding: 2px 6px; cursor: pointer; font-size: 12px; margin-left: 4px; }
        .code-actions button:hover { background-color: #666; }
        code { font-family: Consolas, 'Courier New', monospace; }
        p { margin: 0 0 10px 0; }
        p:last-child { margin-bottom: 0; }
        table { border-collapse: collapse; width: 100%; margin: 10px 0; background-color: #fff; box-shadow: 0 1px 2px rgba(0,0,0,0.1); }
        th, td { border: 1px solid #ccc; padding: 8px 12px; text-align: left; }
        th { background-color: #f0f0f0; font-weight: bold; }
        hr { height: 1px; border: none; background-color: #E5E7EB; margin: 16px 0; }
    </style>
    <script>
        function addCodeActionButtons(bubble) {
            const pres = bubble.querySelectorAll('pre');
            pres.forEach(pre => {
                const code = pre.querySelector('code');
                if (!code) return;

                const actionsDiv = document.createElement('div');
                actionsDiv.className = 'code-actions';

                const insertButton = document.createElement('button');
                insertButton.innerText = '插入';
                insertButton.onclick = () => {
                    window.chrome.webview.postMessage(JSON.stringify({
                        type: 'insertCode',
                        replace: false,
                        code: code.innerText
                    }));
                };
                
                const replaceButton = document.createElement('button');
                replaceButton.innerText = '替换';
                replaceButton.onclick = () => {
                    window.chrome.webview.postMessage(JSON.stringify({
                        type: 'insertCode',
                        replace: true,
                        code: code.innerText
                    }));
                };

                actionsDiv.appendChild(insertButton);
                actionsDiv.appendChild(replaceButton);
                pre.appendChild(actionsDiv);
            });
        }

        function appendBubble(role, htmlContent, id) {
            const container = document.getElementById('chat-container');
            const bubble = document.createElement('div');
            bubble.id = id;
            bubble.className = `msg msg-${role}`;
            bubble.innerHTML = htmlContent;
            addCodeActionButtons(bubble);
            container.appendChild(bubble);
            window.scrollTo(0, document.body.scrollHeight);
        }
        function updateBubble(id, htmlContent) {
            const bubble = document.getElementById(id);
            if (bubble) {
                bubble.innerHTML = htmlContent;
                addCodeActionButtons(bubble);
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

            // Top Border
            _topBorderPanel.Dock = DockStyle.Top;
            _topBorderPanel.Height = 1;
            _topBorderPanel.BackColor = Color.LightGray;

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
                var message = JsonConvert.DeserializeObject<dynamic>(e.WebMessageAsJson);
                string type = message.type;

                if (type == "insertCode")
                {
                    string code = message.code;
                    bool replace = message.replace;
                    OnInsertCodeRequested?.Invoke(this, new InsertCodeEventArgs { Code = code, ReplaceSelection = replace });
                }
            }
            catch
            {
                // Ignore malformed messages
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
