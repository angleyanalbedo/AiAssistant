using System;
using System.Net;
using System.Windows.Forms;

namespace AiAssistant.WinFormsClient
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // 兼容老版本 .NET Framework 的网络请求
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
