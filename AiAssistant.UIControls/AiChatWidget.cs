using Newtonsoft.Json;
using System;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AiAssistant.UIControls
{
    public class AiChatWidget : UserControl
    {
        private RichTextBox _chatLog;
        private TextBox _userInput;
        private Button _sendButton;
        private static readonly HttpClient _httpClient = new HttpClient();

        public string ServerApiUrl { get; set; } = "http://localhost:5000/api/chat";
        public AiConnectionMode ConnectionMode { get; set; } = AiConnectionMode.LocalServer;
        public string DirectApiBaseUrl { get; set; } = "https://api.openai.com/v1";
        public string DirectApiKey { get; set; } = "";
        public string DirectApiModel { get; set; } = "gpt-3.5-turbo";

        public AiChatWidget()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            _chatLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            _userInput = new TextBox
            {
                Dock = DockStyle.Fill,
            };
            _userInput.KeyDown += async (sender, e) =>
            {
                if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    e.SuppressKeyPress = true;
                    await SendMessageAsync();
                }
            };

            _sendButton = new Button
            {
                Dock = DockStyle.Right,
                Text = "Send",
                Width = 75
            };
            _sendButton.Click += async (sender, e) => await SendMessageAsync();

            var inputPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = _userInput.Height + 10,
                Padding = new Padding(0, 5, 0, 5)
            };

            inputPanel.Controls.Add(_userInput);
            inputPanel.Controls.Add(_sendButton);

            this.Controls.Add(_chatLog);
            this.Controls.Add(inputPanel);
        }

        private async Task SendMessageAsync()
        {
            var message = _userInput.Text.Trim();
            if (string.IsNullOrEmpty(message)) return;

            AppendMessage("You: ", message, Color.Blue);
            _userInput.Clear();
            _sendButton.Enabled = false;

            try
            {
                if (ConnectionMode == AiConnectionMode.LocalServer)
                {
                    var request = new { message };
                    var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync(ServerApiUrl, content);
                    response.EnsureSuccessStatusCode();
                    var responseString = await response.Content.ReadAsStringAsync();
                    var chatResponse = JsonConvert.DeserializeObject<ChatResponse>(responseString);
                    AppendMessage("AI: ", chatResponse.Reply, Color.Green);
                }
                else // DirectOpenAI
                {
                    if (DirectApiBaseUrl.Contains("googleapis.com"))
                    {
                        // Google Gemini API logic
                        var geminiPayload = new
                        {
                            contents = new[] { new { parts = new[] { new { text = message } } } }
                        };
                        var requestUrl = $"{DirectApiBaseUrl.TrimEnd('/')}/models/{DirectApiModel}:generateContent?key={DirectApiKey}";
                        var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                        request.Content = new StringContent(JsonConvert.SerializeObject(geminiPayload), Encoding.UTF8, "application/json");

                        var response = await _httpClient.SendAsync(request);
                        var responseString = await response.Content.ReadAsStringAsync();
                        response.EnsureSuccessStatusCode();

                        dynamic result = JsonConvert.DeserializeObject(responseString);
                        string reply = result.candidates[0].content.parts[0].text;
                        AppendMessage("AI: ", reply, Color.Green);
                    }
                    else
                    {
                        // Standard OpenAI API logic
                        var openAiPayload = new
                        {
                            model = DirectApiModel,
                            messages = new[] { new { role = "user", content = message } }
                        };
                        var requestUrl = DirectApiBaseUrl.TrimEnd('/') + "/chat/completions";
                        var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                        request.Headers.Add("Authorization", $"Bearer {DirectApiKey}");
                        request.Content = new StringContent(JsonConvert.SerializeObject(openAiPayload), Encoding.UTF8, "application/json");

                        var response = await _httpClient.SendAsync(request);
                        var responseString = await response.Content.ReadAsStringAsync();
                        response.EnsureSuccessStatusCode();

                        dynamic result = JsonConvert.DeserializeObject(responseString);
                        string reply = result.choices[0].message.content;
                        AppendMessage("AI: ", reply, Color.Green);
                    }
                }
            }
            catch (Exception ex)
            {
                AppendMessage("Error: ", ex.Message, Color.Red);
            }
            finally
            {
                _sendButton.Enabled = true;
            }
        }

        private void AppendMessage(string prefix, string message, Color color)
        {
            if (_chatLog.InvokeRequired)
            {
                _chatLog.Invoke(new Action(() => AppendMessage(prefix, message, color)));
                return;
            }
            _chatLog.SelectionStart = _chatLog.TextLength;
            _chatLog.SelectionLength = 0;
            _chatLog.SelectionColor = color;
            _chatLog.AppendText(prefix + message + Environment.NewLine + Environment.NewLine);
            _chatLog.ScrollToCaret();
        }

        private class ChatResponse
        {
            public string Reply { get; set; }
        }
    }
}
