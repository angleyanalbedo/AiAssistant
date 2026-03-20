# AiAssistant

AiAssistant 是一个功能强大且可扩展的 AI 助手后端服务，旨在提供一个统一的接口来与全球主流的大型语言模型（LLM）进行交互。它既可以作为个人开发和测试的工具，也可以作为构建更复杂 AI 应用的坚实基础。

## ✨ 功能特性

- **多提供商支持**：通过单个配置文件即可无缝切换 OpenAI、Gemini、DeepSeek、Groq、OpenRouter、SiliconFlow 等多种 OpenAI 兼容的 API 服务。
- **本地模型支持**：集成了对本地部署模型（如 Ollama）和本地命令行工具（如 `claude` CLI）的支持，提供了离线和私有化使用的能力。
- **统一引擎架构**：采用微软最新的 `Microsoft.Extensions.AI` 标准，通过 `StandardAiEngine` 实现对所有兼容 API 的统一调用。
- **动态引擎切换**：应用启动时会根据配置自动选择使用云端 API 引擎还是本地进程引擎。
- **简单易用**：提供了一个简洁的 Web UI 用于快速进行聊天测试。
- **易于配置**：所有提供商的凭据和模型信息都集中在 `appsettings.json` 文件中管理。

## 🚀 快速开始

### 环境要求

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 或更高版本。
- (可选) 如果希望使用本地 `claude` CLI，请确保已安装并将其路径添加至系统环境变量 `PATH` 中。

### 安装与运行

1.  **克隆仓库**
    ```bash
    git clone <your-repository-url>
    cd <repository-folder>
    ```

2.  **配置应用**
    - 进入 `AiAssistant.Server` 目录。
    - 将 `appsettings.json.example` 文件复制并重命名为 `appsettings.json`。
    - 打开 `appsettings.json` 文件进行编辑。

3.  **编辑配置文件**
    - **选择 AI 提供商**：修改 `AiProviders.Active` 的值来指定您希望使用的服务。例如，将其设置为 `"DeepSeek"`、`"Gemini"` 或 `"ClaudeCLI"`。
    - **填写凭据**：在 `AiProviders.Endpoints` 下找到您选择的提供商，并填入正确的 `ApiKey`、`BaseUrl` 和 `Model`。对于本地 Ollama 或其他不需要密钥的服务，`ApiKey` 可以留空或为任意值。

    **示例 (使用 DeepSeek):**
    ```json
    "AiProviders": {
      "Active": "DeepSeek",
      "Endpoints": {
        "DeepSeek": { "BaseUrl": "https://api.deepseek.com/v1", "ApiKey": "YOUR_DEEPSEEK_API_KEY", "Model": "deepseek-chat" },
        // ... 其他配置
      }
    }
    ```

    **示例 (使用本地 Claude CLI):**
    ```json
    "AiProviders": {
      "Active": "ClaudeCLI",
      // ... 其他配置
    }
    ```

4.  **启动服务**
    在 `AiAssistant.Server` 目录下运行以下命令：
    ```bash
    dotnet run
    ```
    服务启动后，默认监听 `http://localhost:5000`。

5.  **开始聊天**
    打开浏览器并访问 `http://localhost:5000`，即可看到聊天界面。

## 🔧 引擎架构

本项目包含两种核心 AI 引擎，在程序启动时根据配置自动选择：

1.  **`StandardAiEngine`**:
    - 基于 `Microsoft.Extensions.AI` 和 `OpenAI` 官方 SDK。
    - 负责调用所有兼容 OpenAI API v1 规范的云端和本地（如 Ollama）HTTP 服务。
    - 当 `AiProviders.Active` 设置为 `OpenAI`, `Gemini`, `DeepSeek` 等时激活。

2.  **`ClaudeCodeProcessEngine`**:
    - 通过启动本地子进程来调用 `claude` 命令行工具。
    - 负责处理与本地 CLI 的交互、捕获输出并清理 ANSI 乱码。
    - 当 `AiProviders.Active` 设置为 `"ClaudeCLI"` 时激活。

## 📁 项目结构

```
.
├── AiAssistant.Server/         # 后端服务项目
│   ├── Engines/                # AI 引擎实现
│   │   ├── StandardAiEngine.cs
│   │   └── ClaudeCodeProcessEngine.cs
│   ├── Utils/                  # 工具类
│   │   └── AnsiStripper.cs
│   ├── wwwroot/                # Web UI 静态文件 (HTML, JS, CSS)
│   ├── appsettings.json        # 应用程序配置 (需自行创建)
│   ├── appsettings.json.example  # 配置模板
│   └── Program.cs              # 程序入口与服务配置
│
├── AiAssistant.WinFormsClient/ # (可选) Windows 窗体客户端
└── README.md                   # 就是你正在看的这个文件
```

## 🤝 贡献

欢迎提交 Issue 或 Pull Request 来改进此项目！
