using AiAssistant.Server.Interfaces;
using AiAssistant.Server.Utils;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AiAssistant.Server.Engines
{
    public class ClaudeCodeProcessEngine : IAiEngine
    {
        public async Task<string> ChatAsync(string message)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "claude", // 确保 'claude' 在系统的 PATH 环境变量中
                // 或者使用: FileName = "cmd.exe", Arguments = "/c claude",
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = new Process { StartInfo = processStartInfo };
            if (!process.Start())
            {
                return "错误: 无法启动 claude 进程。";
            }

            await process.StandardInput.WriteLineAsync(message);
            process.StandardInput.Close(); // 表示输入结束

            string result = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return AnsiCleaner.Clean(result);
        }
    }
}
