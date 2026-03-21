using AiAssistant.UIControls;
using Newtonsoft.Json;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AiAssistant.UITester
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            this.Text = "AI Assistant Test Host";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            string apiKey = "";
            string configPath = "config.json";

            if (File.Exists(configPath))
            {
                var configJson = File.ReadAllText(configPath);
                dynamic config = JsonConvert.DeserializeObject(configJson);
                apiKey = config.ApiKey;
            }
            else
            {
                MessageBox.Show(
                    "未找到 config.json 配置文件！\n请在程序运行目录下复制 config.example.json 并将其重命名为 config.json，填入你的真实 API Key 后重试。",
                    "配置错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            this.Size = new Size(1200, 700);
            this.StartPosition = FormStartPosition.CenterScreen;

            string apiKey = "";
            string configPath = "config.json";

            if (File.Exists(configPath))
            {
                var configJson = File.ReadAllText(configPath);
                dynamic config = JsonConvert.DeserializeObject(configJson);
                apiKey = config.ApiKey;
            }
            else
            {
                MessageBox.Show(
                    "未找到 config.json 配置文件！\n请在程序运行目录下复制 config.example.json 并将其重命名为 config.json，填入你的真实 API Key 后重试。",
                    "配置错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Right,
                Width = 800,
                SplitterDistance = 400
            };

            var ieChatWidget = new AiChatHtmlWidget
            {
                Dock = DockStyle.Fill,
                ConnectionMode = AiConnectionMode.DirectOpenAI,
                DirectApiBaseUrl = "https://openrouter.ai/api/v1",
                DirectApiKey = apiKey,
                DirectApiModel = "stepfun/step-3.5-flash:free"
            };

            var webViewChatWidget = new AiChatWebViewWidget
            {
                Dock = DockStyle.Fill,
                ConnectionMode = AiConnectionMode.DirectOpenAI,
                DirectApiBaseUrl = "https://openrouter.ai/api/v1",
                DirectApiKey = apiKey,
                DirectApiModel = "stepfun/step-3.5-flash:free"
            };

            splitContainer.Panel1.Controls.Add(ieChatWidget);
            splitContainer.Panel2.Controls.Add(webViewChatWidget);

            var autoCompleteEditor = new AiAutoCompleteEditor
            {
                Dock = DockStyle.Fill,
                ConnectionMode = AiConnectionMode.DirectOpenAI,
                DirectApiBaseUrl = "https://openrouter.ai/api/v1",
                DirectApiKey = apiKey,
                DirectApiModel = "stepfun/step-3.5-flash:free"
            };

            if (string.IsNullOrEmpty(apiKey))
            {
                ieChatWidget.Enabled = false;
                webViewChatWidget.Enabled = false;
                autoCompleteEditor.Enabled = false;
            }

            this.Controls.Add(autoCompleteEditor);
            this.Controls.Add(splitContainer);
        }
    }
}
