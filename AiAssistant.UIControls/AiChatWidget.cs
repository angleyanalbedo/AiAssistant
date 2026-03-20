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
            _userInput.KeyDown += (sender, e) =>
            {
                if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    e.SuppressKeyPress = true;
                    SendMessageAsync();
                }
            };

            _sendButton = new Button
            {
                Dock = DockStyle.Right,
                Text = "Send",
                Width = 75
            };
            _sendButton.Click += (sender, e) => SendMessageAsync();

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
                var request = new { message };
                var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(ServerApiUrl, content);
                response.EnsureSuccessStatusCode();
                var responseString = await response.Content.ReadAsStringAsync();
                var chatResponse = JsonConvert.DeserializeObject<ChatResponse>(responseString);
                AppendMessage("AI: ", chatResponse.Reply, Color.Green);
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
