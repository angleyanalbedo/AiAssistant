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
builder.Services.AddHttpClient();

// 动态注入 AI 引擎
if (IsClaudeCliAvailable())
{
    Console.WriteLine("Claude CLI detected. Using ClaudeCodeProcessEngine.");
    builder.Services.AddSingleton<IAiEngine, ClaudeCodeProcessEngine>();
}
else
{
    Console.WriteLine("Claude CLI not found. Using BasicApiEngine.");
    // 使用 AddHttpClient 注册 BasicApiEngine，以便正确注入和管理 HttpClient 实例
    builder.Services.AddHttpClient<IAiEngine, BasicApiEngine>();
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

    // 在 Linux/macOS 上使用 'which' 命令检测 'claude' 是否存在于 PATH
    if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "which",
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
            return false; // 'which' 命令可能不存在
        }
    }

    return false;
}
