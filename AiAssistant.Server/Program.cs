using AiAssistant.Server.Engines;
using AiAssistant.Server.Interfaces;
using AiAssistant.Server.Models;
using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// 添加服务到容器
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

// 根据配置动态注入 AI 引擎
var activeProvider = builder.Configuration["AiProviders:Active"];
if (activeProvider == "ClaudeCLI")
{
    Console.WriteLine("Active provider is ClaudeCLI. Using ClaudeCodeProcessEngine.");
    builder.Services.AddScoped<IAiEngine, ClaudeCodeProcessEngine>();
}
else
{
    Console.WriteLine($"Active provider is '{activeProvider}'. Using StandardAiEngine.");
    builder.Services.AddScoped<IAiEngine, StandardAiEngine>();
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
