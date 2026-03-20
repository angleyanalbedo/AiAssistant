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
        private RichTextBox _chatHistoryRichTextBox;
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
            this.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(134)));
            this.BackColor = Color.FromArgb(243, 243, 243);
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            _chatHistoryRichTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = this.BackColor,
                BorderStyle = BorderStyle.None
            };

            _userInput = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                BorderStyle = BorderStyle.None
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
                Width = 75,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White
            };
            _sendButton.FlatAppearance.BorderSize = 0;
            _sendButton.Click += async (sender, e) => await SendMessageAsync();

            var inputPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = _userInput.Height + 20,
                Padding = new Padding(5),
                BackColor = Color.White
            };

            inputPanel.Controls.Add(_userInput);
            inputPanel.Controls.Add(_sendButton);

            this.Controls.Add(_chatHistoryRichTextBox);
            this.Controls.Add(inputPanel);
        }

        private async Task SendMessageAsync()
        {
            var message = _userInput.Text.Trim();
            if (string.IsNullOrEmpty(message)) return;

            AppendMessage("You: ", message, Color.DarkBlue);
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
                    AppendMessage("AI: ", chatResponse.Reply, Color.Black);
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
                        AppendMessage("AI: ", reply, Color.Black);
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
                        AppendMessage("AI: ", reply, Color.Black);
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
            if (_chatHistoryRichTextBox.InvokeRequired)
            {
                _chatHistoryRichTextBox.Invoke(new Action(() => AppendMessage(prefix, message, color)));
                return;
            }

            _chatHistoryRichTextBox.AppendText(Environment.NewLine);
            int selectionStart = _chatHistoryRichTextBox.TextLength;

            // Set alignment
            bool isUser = prefix.Contains("You");
            _chatHistoryRichTextBox.SelectionAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;

            // Append prefix (bold)
            _chatHistoryRichTextBox.SelectionFont = new Font(this.Font, FontStyle.Bold);
            _chatHistoryRichTextBox.SelectionColor = color;
            _chatHistoryRichTextBox.AppendText(isUser ? "👨 你: " : "🤖 AI: ");

            // Append message (regular)
            _chatHistoryRichTextBox.SelectionFont = new Font(this.Font, FontStyle.Regular);
            _chatHistoryRichTextBox.AppendText(message);

            // Set background color for the bubble
            _chatHistoryRichTextBox.Select(selectionStart, _chatHistoryRichTextBox.TextLength - selectionStart);
            _chatHistoryRichTextBox.SelectionBackColor = isUser ? Color.FromArgb(190, 228, 255) : Color.White;
            _chatHistoryRichTextBox.Select(_chatHistoryRichTextBox.TextLength, 0);
            _chatHistoryRichTextBox.SelectionBackColor = _chatHistoryRichTextBox.BackColor; // Reset for next line

            _chatHistoryRichTextBox.AppendText(Environment.NewLine + Environment.NewLine);
            _chatHistoryRichTextBox.ScrollToCaret();
        }

        private class ChatResponse
        {
            public string Reply { get; set; }
        }
    }
}
