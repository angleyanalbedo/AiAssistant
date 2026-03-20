using AiAssistant.Server.Engines;
using AiAssistant.Server.Interfaces;
using AiAssistant.Server.Models;
using System;
using System.Diagnostics;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// 添加服务到容器
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 动态注入 AI 引擎
if (IsClaudeCliAvailable())
{
    Console.WriteLine("Claude CLI detected. Using ClaudeCodeProcessEngine.");
    builder.Services.AddSingleton<IAiEngine, ClaudeCodeProcessEngine>();
}
else
{
    Console.WriteLine("Claude CLI not found. Using BasicApiEngine.");
    builder.Services.AddSingleton<IAiEngine, BasicApiEngine>();
}

var app = builder.Build();

// 配置 HTTP 请求管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

// 定义根路径返回一个简单的 HTML UI 用于测试
app.MapGet("/", (HttpContext context) =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    return context.Response.WriteAsync(@"
<!DOCTYPE html>
<html>
<head>
    <title>AI Assistant Test UI</title>
    <style>
        body { font-family: sans-serif; max-width: 800px; margin: auto; padding: 1em; }
        textarea { width: 100%; height: 200px; box-sizing: border-box; margin-bottom: 1em; }
        #response { white-space: pre-wrap; background: #f4f4f4; padding: 1em; border: 1px solid #ddd; margin-top: 1em; min-height: 100px; }
        button { padding: 0.5em 1em; }
    </style>
</head>
<body>
    <h1>AI Assistant Test UI</h1>
    <form id=""chatForm"">
        <textarea id=""message"" name=""message"" placeholder=""Enter your message...""></textarea>
        <button type=""submit"">Send</button>
    </form>
    <h2>Response:</h2>
    <pre id=""response""></pre>

    <script>
        document.getElementById('chatForm').addEventListener('submit', async function (e) {
            e.preventDefault();
            const message = document.getElementById('message').value;
            const responseDiv = document.getElementById('response');
            const submitButton = this.querySelector('button[type=""submit""]');
            
            responseDiv.textContent = 'Waiting for response...';
            submitButton.disabled = true;

            try {
                const response = await fetch('/api/chat', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                    },
                    body: JSON.stringify({ message: message }),
                });

                if (!response.ok) {
                    const errorText = await response.text();
                    throw new Error(`HTTP error! status: ${response.status}. ${errorText}`);
                }

                const data = await response.json();
                responseDiv.textContent = data.reply;
            } catch (error) {
                responseDiv.textContent = 'Error: ' + error.message;
            } finally {
                submitButton.disabled = false;
            }
        });
    </script>
</body>
</html>
    ");
});

// 定义聊天 API 端点
app.MapPost("/api/chat", async (ChatRequest request, IAiEngine engine) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest("Message cannot be empty.");
    }

    var reply = await engine.ChatAsync(request.Message);
    return Results.Ok(new { reply });
});

app.Run();


static bool IsClaudeCliAvailable()
{
    // 优先检查环境变量
    var useClaudeEnv = Environment.GetEnvironmentVariable("USE_CLAUDE_CLI");
    if (bool.TryParse(useClaudeEnv, out var useClaude) && useClaude)
    {
        return true;
    }
    if (useClaudeEnv == "0" || (bool.TryParse(useClaudeEnv, out useClaude) && !useClaude))
    {
        return false;
    }

    // 在 Windows 上使用 'where' 命令检测 'claude' 是否存在于 PATH
    if (OperatingSystem.IsWindows())
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = "claude",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false; // 'where' 命令可能不存在于旧版 Windows
        }
    }

    // 在 Linux/macOS 上可以添加类似的 'which' 命令检测
    return false;
}
