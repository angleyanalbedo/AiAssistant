using Newtonsoft.Json;
using System;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AiAssistant.WinFormsClient
{
    public class AiChatWidget : UserControl
    {
        private readonly RichTextBox _chatHistoryBox;
        private readonly TextBox _inputTextBox;
        private readonly Button _sendButton;
        private readonly HttpClient _httpClient;

        public string ServerApiUrl { get; set; } = "http://localhost:5000/api/chat";

        public AiChatWidget()
        {
            _httpClient = new HttpClient();
            InitializeComponent();
            AppendMessage("系统", "客户端已启动，等待输入...", Color.Gray);
        }

        private void InitializeComponent()
        {
            this.BackColor = Color.FromArgb(245, 245, 245); // #F5F5F5

            // 聊天记录区
            _chatHistoryBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = this.BackColor,
                ReadOnly = true,
                Font = new Font("微软雅黑", 10F),
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Padding = new Padding(10)
            };

            // 底部输入区
            var inputPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.FromArgb(230, 230, 230), // 稍深的灰色
                Padding = new Padding(10)
            };

            // 发送按钮
            _sendButton = new Button
            {
                Dock = DockStyle.Right,
                Text = "发送",
                Width = 75,
                Height = 40,
                BackColor = Color.FromArgb(0, 123, 255), // #007BFF
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("微软雅黑", 9F, FontStyle.Bold)
            };
            _sendButton.FlatAppearance.BorderSize = 0;
            _sendButton.Click += async (s, e) => await SendMessageAsync();

            // 用户输入框
            _inputTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                Font = new Font("微软雅黑", 10F),
                BorderStyle = BorderStyle.FixedSingle,
                Height = 40
            };
            _inputTextBox.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    e.SuppressKeyPress = true;
                    await SendMessageAsync();
                }
            };

            inputPanel.Controls.Add(_inputTextBox);
            inputPanel.Controls.Add(_sendButton);

            this.Controls.Add(_chatHistoryBox);
            this.Controls.Add(inputPanel);
        }

        private async Task SendMessageAsync()
        {
            var message = _inputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(message)) return;

            _sendButton.Enabled = false;
            _inputTextBox.Enabled = false;
            AppendMessage("你", message, Color.DarkBlue);
            _inputTextBox.Clear();

            try
            {
                var requestPayload = new { message };
                var jsonPayload = JsonConvert.SerializeObject(requestPayload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(ServerApiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var chatResponse = JsonConvert.DeserializeObject<ChatResponse>(jsonResponse);
                    AppendMessage("AI", chatResponse.Reply, Color.DarkGreen);
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    AppendMessage("错误", $"{response.StatusCode}\r\n{error}", Color.Red);
                }
            }
            catch (HttpRequestException ex)
            {
                var errorMsg = $"无法连接到服务端。请确保 AiAssistant.Server 正在运行。\r\n{ex.Message}";
                AppendMessage("错误", errorMsg, Color.Red);
                MessageBox.Show(errorMsg, "连接错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                var errorMsg = $"发生未知错误: {ex.Message}";
                AppendMessage("错误", errorMsg, Color.Red);
                MessageBox.Show(errorMsg, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _sendButton.Enabled = true;
                _inputTextBox.Enabled = true;
                _inputTextBox.Focus();
            }
        }

        private void AppendMessage(string prefix, string message, Color color)
        {
            if (_chatHistoryBox.InvokeRequired)
            {
                _chatHistoryBox.Invoke(new Action(() => AppendMessage(prefix, message, color)));
            }
            else
            {
                _chatHistoryBox.SelectionStart = _chatHistoryBox.TextLength;
                _chatHistoryBox.SelectionLength = 0;

                _chatHistoryBox.SelectionFont = new Font("微软雅黑", 10F, FontStyle.Bold);
                _chatHistoryBox.SelectionColor = color;
                _chatHistoryBox.AppendText($"{prefix}: ");

                _chatHistoryBox.SelectionFont = new Font("微软雅黑", 10F, FontStyle.Regular);
                _chatHistoryBox.SelectionColor = Color.Black;
                _chatHistoryBox.AppendText(message + Environment.NewLine + Environment.NewLine);

                _chatHistoryBox.ScrollToCaret();
            }
        }

        // 用于反序列化响应的内部类
        private class ChatResponse
        {
            public string Reply { get; set; }
        }
    }
}
