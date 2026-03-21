using AiAssistant.Server.Interfaces;
using AiAssistant.Server.Models;
using AiAssistant.Server.Utils;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace AiAssistant.Server.Engines
{
    public class ClaudeCodeProcessEngine : IAiEngine
    {
        public async Task<string> ChatAsync(ChatRequest request)
        {
            try
            {
                var promptBuilder = new StringBuilder();
                foreach (var message in request.Messages)
                {
                    promptBuilder.AppendLine($"{message.Role}: {message.Content}");
                }
                var fullPrompt = promptBuilder.ToString();

                var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c claude \"{fullPrompt.Replace("\"", "\\\"")}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                var output = await outputTask;
                var error = await errorTask;

                var result = string.IsNullOrEmpty(output) ? error : output;

                return AnsiStripper.Clean(result);
            }
            catch (Exception ex)
            {
                return $"启动 'claude' 进程失败。请确保 claude CLI 已正确安装并位于系统的 PATH 中。错误: {ex.Message}";
            }
        }
    }
}
