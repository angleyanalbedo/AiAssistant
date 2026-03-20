using System;
using System.Windows.Forms;

namespace AiAssistant.WinFormsClient
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            this.Text = "AI Assistant Client";
            this.Size = new System.Drawing.Size(600, 500);
            this.StartPosition = FormStartPosition.CenterScreen;

            var chatWidget = new AiChatWidget
            {
                Dock = DockStyle.Fill
            };

            this.Controls.Add(chatWidget);
        }
    }
}
