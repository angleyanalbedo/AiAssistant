using AiAssistant.Server.Engines;
using AiAssistant.Server.Interfaces;
using AiAssistant.Server.Models;
using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// 添加服务到容器
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

// 统一注入 StandardAiEngine
builder.Services.AddScoped<IAiEngine, StandardAiEngine>();

var app = builder.Build();

// 配置 HTTP 请求管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    // 将根 URL 重定向到 Swagger UI
    app.UseRewriter(new RewriteOptions().AddRedirect("^$", "swagger"));
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
