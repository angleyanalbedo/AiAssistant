using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AiAssistant.WinFormsClient
{
    public partial class Form1 : Form
    {
        private readonly TextBox _logTextBox;
        private readonly TextBox _inputTextBox;
        private readonly Button _sendButton;
        private readonly HttpClient _httpClient;

        public Form1()
        {
            // 初始化 HttpClient
            _httpClient = new HttpClient();

            // 动态构建 UI
            this.Text = "AI Assistant Client";
            this.Size = new System.Drawing.Size(600, 450);
            this.StartPosition = FormStartPosition.CenterScreen;

            _logTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)))
            };

            var inputPanel = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(5) };

            _sendButton = new Button
            {
                Dock = DockStyle.Right,
                Text = "发送",
                Width = 80
            };
            _sendButton.Click += async (s, e) => await SendMessageAsync();

            _inputTextBox = new TextBox { Dock = DockStyle.Fill };
            _inputTextBox.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    e.SuppressKeyPress = true; // 阻止回车换行
                    await SendMessageAsync();
                }
            };

            inputPanel.Controls.Add(_inputTextBox);
            inputPanel.Controls.Add(_sendButton);

            this.Controls.Add(_logTextBox);
            this.Controls.Add(inputPanel);

            Log("客户端已启动，等待输入...");
        }

        private async Task SendMessageAsync()
        {
            var message = _inputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(message)) return;

            _sendButton.Enabled = false;
            _inputTextBox.Enabled = false;
            Log($"\r\n> 你: {message}");
            _inputTextBox.Clear();

            try
            {
                var requestPayload = new { message };
                var jsonPayload = JsonConvert.SerializeObject(requestPayload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("http://localhost:5000/api/chat", content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var chatResponse = JsonConvert.DeserializeObject<ChatResponse>(jsonResponse);
                    Log($"< AI: {chatResponse.Reply}");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Log($"< 错误: {response.StatusCode}\r\n{error}");
                }
            }
            catch (HttpRequestException ex)
            {
                Log($"< 错误: 无法连接到服务端。请确保 AiAssistant.Server 正在运行。\r\n{ex.Message}");
                MessageBox.Show("无法连接到服务端。请确保 AiAssistant.Server 正在运行。", "连接错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                Log($"< 发生未知错误: {ex.Message}");
                MessageBox.Show($"发生未知错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _sendButton.Enabled = true;
                _inputTextBox.Enabled = true;
                _inputTextBox.Focus();
            }
        }

        private void Log(string text)
        {
            if (_logTextBox.InvokeRequired)
            {
                _logTextBox.Invoke(new Action(() => Log(text)));
            }
            else
            {
                _logTextBox.AppendText(text + Environment.NewLine);
            }
        }

        // 用于反序列化响应的内部类
        private class ChatResponse
        {
            public string Reply { get; set; }
        }
    }
}
