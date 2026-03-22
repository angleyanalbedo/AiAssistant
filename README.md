

# AiAssistant.WinForms

**面向工业与测绘软件的 .NET 4.5 高性能 AI UI 控件库**

[](https://dotnet.microsoft.com/)
[](https://www.google.com/search?q=LICENSE)

## 🎯 项目愿景

在工业自动化等传统桌面软件领域，升级 .NET 架构往往面临巨大的兼容性风险。本库提供了一套**零侵入、高度解耦**的 WinForms 控件，使老旧系统能够通过简单的组件拖拽，瞬间获得类似 Cursor 或 GitHub Copilot 的现代 AI 辅助能力。

-----

## ✨ 核心控件

### 1\. `AiAutoCompleteTextBox` (智能代码编辑器)

基于 ScintillaNET 深度定制，专为代码补全优化的编辑器。

  * **Ghost Text (幽灵文本)**：手动按下 `Alt + \` 唤醒 AI，建议以灰色斜体预览，`Tab` 键一键采纳。
  * **多语言语义高亮**：内置 **ST (Structured Text)**、C\#、Java、Pascal 等工业语言解析器。
  * **智能触发器**：内置 `CancellationToken` 机制，彻底解决网络请求竞争导致的补全闪烁。
  * **半手动模式**：拒绝自动补全带来的打扰，仅在用户需要时（快捷键）显灵，稳定可靠。

### 2\. `AiChatWebViewWidget` (现代聊天对话框)

基于 WebView2 的现代聊天窗口，具备 Markdown 渲染与流式输出能力。

  * **现代 Web 体验**：采用 **WebView2** 内核，支持最新的 HTML5/CSS3 标准，性能优异。
  * **Material Design 风格**：扁平化设计，集成 highlight.js 语法高亮，彻底摆脱 WinForms 的陈旧感。
  * **多轮对话记忆**：内置 Context 队列，支持联系上下文进行连续追问。
  * **流式打字机效果**：支持 SSE 数据流渲染，代码逐字弹出，交互感极佳。
  * **代码一键插入**：聊天结果中的代码块可一键插入到编辑器光标处。

-----

## 🚀 快速集成指南

### 0\. 安装与引用

1.  **安装 NuGet 包**: 在 Visual Studio 的 NuGet 包管理器中搜索 `AiAssistant.UIControls` 并安装。
2.  **拖拽控件**: 安装后，`AiAutoCompleteTextBox` 和 `AiChatWebViewWidget` 控件将自动出现在工具箱中。将它们拖拽到你的 WinForms 窗体上即可开始使用。

### 1\. 配置控件 AI 属性

```csharp
// 1.1 配置 AiAutoCompleteEditor
// 切换为工业 ST 语言模式
aiEditor.CurrentLanguage = CodeLanguage.ST; 

// 配置 API 节点（支持 DeepSeek, OpenAI, Gemini 等）
aiEditor.DirectApiBaseUrl = "https://api.deepseek.com/v1";
aiEditor.DirectApiKey = "sk-xxxxxxxxxxxx";
aiEditor.DirectApiModel = "deepseek-coder";

// 注入专属人设
aiEditor.SystemPrompt = "你是一个 PLC 编程专家，请只输出严谨的 ST 补全代码。";

// 1.2 配置 AiChatWebViewWidget
// 配置 API 节点
aiChat.ConnectionMode = AiConnectionMode.DirectOpenAI; // 或 LocalServer
aiChat.DirectApiBaseUrl = "https://api.deepseek.com/v1";
aiChat.DirectApiKey = "sk-xxxxxxxxxxxx";
aiChat.DirectApiModel = "deepseek-chat";

// 注入专属人设
aiChat.SystemPrompt = "你是一个全能的编程助手，请提供详细的中文代码解释。";
```

### 2\. 实现控件联动 (解耦设计)

通过主窗体（Mediator）连接两个控件，实现无缝交互：

```csharp
// 1. 编辑器快捷键触发 AI 聊天
aiEditor.OnAiActionRequested += (s, e) => {
    aiChat.SendExternalMessage(e.Prompt);
};

// 2. 聊天框结果一键回填编辑器
aiChat.OnInsertCodeRequested += (s, e) => {
    if (e.ReplaceSelection) 
        aiEditor.ReplaceSelectedText(e.Code);
    else 
        aiEditor.InsertTextAtCursor(e.Code);
};

// 3. 焦点无缝切换 (快捷键 Ctrl+J / Esc)
aiEditor.OnFocusChatRequested += (s, e) => aiChat.FocusInput();
aiChat.OnFocusEditorRequested += (s, e) => aiEditor.Focus();
```

-----

## 🏗️ 项目架构

```text
.
├── AiAssistant.UIControls/     # 核心控件库 (Target: .NET 4.5)
│   ├── AiAutoCompleteEditor              # Scintilla 逻辑与幽灵文本实现
│   ├── AiChatWebViewWidget                   # 基于 WebView2 的现代聊天控件
│   └── Utils/                  # Markdown 渲染器与事件定义
├── AiAssistant.Server/         # 后端网关 (Target: .NET 8.0)
│   └── Engines/                # 多模型转发引擎 (StandardAi / ClaudeCLI)
└── AiAssistant.UITester/       # 样例演示程序
```

-----

## 🛠️ 技术特性

  - **异步非阻塞**：所有 AI 请求均在后台线程完成，UI 永远保持 60fps 响应。
  - **内存优化**：针对 IE11 内核进行了 DOM 回收优化，长时间运行不卡顿。
  - **零安装依赖**：支持 ILRepack 合并 DLL，方便随宿主程序直接发布。

-----

## 📄 授权

本项目采用 MIT 协议开源。

-----
