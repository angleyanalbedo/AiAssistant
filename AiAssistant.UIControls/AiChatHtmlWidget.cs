using Markdig;
using Newtonsoft.Json;
using System;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AiAssistant.UIControls
{
    public class AiChatHtmlWidget : UserControl
    {
        private WebBrowser _webBrowser;
        private TextBox _inputTextBox;
        private Button _sendButton;
        private Panel _inputAreaPanel;
        private Panel _topBorderPanel;
        private const string PlaceholderText = "输入消息...";

        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

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
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, 'Open Sans', 'Helvetica Neue', sans-serif; margin: 0; padding: 10px; background-color: #F0F0F0; overflow-y: scroll; }
        #chat-container { display: flex; flex-direction: column; }
        .msg { max-width: 80%; padding: 10px 15px; margin-bottom: 10px; border-radius: 15px; line-height: 1.5; word-wrap: break-word; }
        .msg-user { align-self: flex-end; background-color: #DCF8C6; color: #000; }
        .msg-ai { align-self: flex-start; background-color: #FFF; color: #000; border: 1px solid #EAEAEA; }
        pre { background-color: #F4F4F4; border: 1px solid #DDD; border-radius: 5px; padding: 10px; font-family: Consolas, 'Courier New', monospace; white-space: pre-wrap; word-wrap: break-word; }
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
            _sendButton = new Button();
            _inputAreaPanel = new Panel();
            _topBorderPanel = new Panel();

            _inputAreaPanel.SuspendLayout();
            this.SuspendLayout();

            // Input Area Panel
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
            _inputTextBox.Enter += (s, e) =>
            {
                if (_inputTextBox.Text == PlaceholderText)
                {
                    _inputTextBox.Text = "";
                    _inputTextBox.ForeColor = Color.Black;
                }
            };
            _inputTextBox.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_inputTextBox.Text))
                {
                    _inputTextBox.Text = PlaceholderText;
                    _inputTextBox.ForeColor = Color.Gray;
                }
            };
            _inputTextBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && e.Modifiers == Keys.None)
                {
                    e.SuppressKeyPress = true;
                    _sendButton.PerformClick();
                }
            };

            // WebBrowser
            _webBrowser.Dock = DockStyle.Fill;
            _webBrowser.IsWebBrowserContextMenuEnabled = false;
            _webBrowser.AllowWebBrowserDrop = false;
            _webBrowser.ScriptErrorsSuppressed = true;

            // Main Control
            this.Controls.Add(_webBrowser);
            this.Controls.Add(_inputAreaPanel);
            _inputAreaPanel.ResumeLayout(false);
            _inputAreaPanel.PerformLayout();
            this.ResumeLayout(false);
        }

        private async Task SendMessageAsync()
        {
            var message = _inputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(message) || message == PlaceholderText) return;

            _sendButton.Enabled = false;
            _inputTextBox.Enabled = false;

            AppendMessage("user", message);
            _inputTextBox.Clear();

            var thinkingMessageDiv = AppendMessage("ai", "思考中...");

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

                UpdateMessage(thinkingMessageDiv, responseText);
            }
            catch (Exception ex)
            {
                UpdateMessage(thinkingMessageDiv, $"出现错误: {ex.Message}");
            }
            finally
            {
                _sendButton.Enabled = true;
                _inputTextBox.Enabled = true;
                _inputTextBox.Focus();
            }
        }

        public HtmlElement AppendMessage(string role, string markdownText)
        {
            if (_webBrowser.InvokeRequired)
            {
                return (HtmlElement)_webBrowser.Invoke(new Func<string, string, HtmlElement>(AppendMessage), role, markdownText);
            }

            var container = _webBrowser.Document?.GetElementById("chat-container");
            if (container == null) return null;

            var htmlContent = Markdown.ToHtml(markdownText, _markdownPipeline);
            var cssClass = role == "user" ? "msg-user" : "msg-ai";
            
            var newBubble = _webBrowser.Document.CreateElement("div");
            newBubble.SetAttribute("className", $"msg {cssClass}");
            newBubble.InnerHtml = htmlContent;
            
            container.AppendChild(newBubble);
            _webBrowser.Document.Window.ScrollTo(0, container.ScrollRectangle.Height);

            return newBubble;
        }

        private void UpdateMessage(HtmlElement elementToUpdate, string newMarkdownText)
        {
            if (elementToUpdate == null) return;

            if (_webBrowser.InvokeRequired)
            {
                _webBrowser.Invoke(new Action(() => UpdateMessage(elementToUpdate, newMarkdownText)));
                return;
            }
            
            var htmlContent = Markdown.ToHtml(newMarkdownText, _markdownPipeline);
            elementToUpdate.InnerHtml = htmlContent;

            var container = _webBrowser.Document?.GetElementById("chat-container");
            if (container != null)
            {
                _webBrowser.Document.Window.ScrollTo(0, container.ScrollRectangle.Height);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inputTextBox?.Dispose();
                _sendButton?.Dispose();
                _inputAreaPanel?.Dispose();
                _topBorderPanel?.Dispose();
                _webBrowser?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
