

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

### 2\. `AiChatHtmlWidget` (双引擎对话框)

具备 Markdown 渲染与流式输出能力的聊天窗口，支持自动降级。

  * **Ghost Text (幽灵文本)**：手动按下 `Alt + \` 唤醒 AI，建议以灰色斜体预览，`Tab` 键一键采纳。
  * **自适应双引擎**：优先使用 **WebView2**，在无环境的旧机器上自动降级至 **IE11 WebBrowser**，保证 100% 可用。
  * **Material Design 风格**：扁平化设计，集成 FontAwesome 图标，彻底摆脱 WinForms 的陈旧感。
  * **多轮对话记忆**：内置 Context 队列，支持联系上下文进行连续追问。
  * **流式打字机效果**：支持 SSE 数据流渲染，代码逐字弹出，交互感极佳。

-----

## 🚀 快速集成指南

### 1\. 配置编辑器语言与 AI 属性

```csharp
// 切换为工业 ST 语言模式
aiEditor.CurrentLanguage = CodeLanguage.ST; 

// 配置 API 节点（支持 DeepSeek, OpenAI, Gemini 等）
aiEditor.DirectApiBaseUrl = "https://api.deepseek.com/v1";
aiEditor.DirectApiKey = "sk-xxxxxxxxxxxx";
aiEditor.DirectApiModel = "deepseek-coder";

// 注入专属人设
aiEditor.SystemPrompt = "你是一个 PLC 编程专家，请只输出严谨的 ST 补全代码。";
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
│   ├── AiChatWebViewWidget                   # WebView2/IE 双引擎渲染逻辑
│   └── Utils/                  # Markdown 渲染器与 LSP 协议定义
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
