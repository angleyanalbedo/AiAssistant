using AiAssistant.UIControls;
using System.Drawing;
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

            var chatWidget = new AiChatWidget
            {
                Dock = DockStyle.Right,
                Width = 300,
                // To test DirectOpenAI mode, uncomment and fill in the following lines:
                ConnectionMode = AiConnectionMode.DirectOpenAI,
                DirectApiBaseUrl = "https://openrouter.ai/api/v1",
                DirectApiKey = "[REDACTED OPENROUTER API KEY]",
                DirectApiModel = "stepfun/step-3.5-flash:free"
            };

            var autoCompleteTextBox = new AiAutoCompleteTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10),
                ConnectionMode = AiConnectionMode.DirectOpenAI,
                DirectApiBaseUrl = "https://openrouter.ai/api/v1",
                DirectApiKey = "[REDACTED OPENROUTER API KEY]",
                DirectApiModel = "stepfun/step-3.5-flash:free"
            };

            this.Controls.Add(autoCompleteTextBox);
            this.Controls.Add(chatWidget);
        }
    }
}
