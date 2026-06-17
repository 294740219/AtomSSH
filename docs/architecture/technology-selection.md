# AtomSSH 技术选型原则

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的第三方库引入原则、官方库优先策略和第一版关键技术选型
>
> 变更规则：新增生产依赖、替换核心库或调整官方库优先策略时，必须先更新本文档，并同步检查 architecture、modules 和 implementation 文档。

本文档回答一个问题：AtomSSH 在实现阶段应该优先使用哪些基础技术，以及什么时候允许引入社区库。

## 1. 总原则

AtomSSH 采用“dotnet 官方标准库优先，缺失能力再引入社区库”的技术选型策略。

优先级如下：

1. .NET BCL。
2. Microsoft.Extensions 官方扩展库。
3. Avalonia / AtomUI 已确定 UI 技术栈。
4. 功能缺口明确、维护状态可接受、协议能力成熟的社区库。
5. 自研复杂底层协议能力只作为最后选择。

引入社区库必须满足：

- 官方 .NET 库无法直接提供该能力，或自行实现风险明显更高。
- 该库只出现在具体实现模块，不泄漏到 Core 或 Application 的公开契约。
- 必须由 Core 端口隔离，后续可以替换实现。
- 必须确认许可证、维护状态、目标框架和安全风险。

## 2. 第一版技术选型表

| 能力 | 首选技术 | 是否允许社区库 | 归属模块 | 说明 |
| --- | --- | --- | --- | --- |
| Core 模型、值对象、结果类型 | C# / .NET BCL | 否 | Core | Core 必须保持纯净，不依赖外层实现库。 |
| 依赖注入 | `Microsoft.Extensions.DependencyInjection` | 暂不需要 | Desktop 组合根、各模块 DI 扩展 | 内层模块只暴露 `IServiceCollection` 注册扩展，不自行 `BuildServiceProvider()`。 |
| 配置与设置 | `System.Text.Json`、`System.IO` | 暂不需要 | Infrastructure | 第一版使用 JSON 文件即可；格式由 Infrastructure 隔离。 |
| 导入导出 | `System.Text.Json`、`System.IO.Compression` | 暂不需要 | Infrastructure | 导出包不包含 secret material。 |
| 文件与流 | `System.IO` | 暂不需要 | Infrastructure、Transfer | 本地文件、临时文件、LocalRelay 流中转优先使用官方 API。 |
| 日志抽象 | `Microsoft.Extensions.Logging` | 可后续评估 sink | Infrastructure | 第一版先使用官方日志抽象，文件落盘实现可保持简单。 |
| 后台任务和取消 | `Task`、`CancellationToken`、`Channel<T>`、`PeriodicTimer` | 暂不需要 | Transfer、Session | 队列、取消、状态轮询优先使用 BCL 并发原语。 |
| 网络路径规划 | C# 业务逻辑 | 否 | Network | 路径规划是 AtomSSH 业务规则，不依赖外部库。 |
| SSH / SFTP / SCP / 端口转发 | SSH.NET | 是，第一版选 SSH.NET | Session | .NET 无官方 SSH 客户端标准库，SSH.NET 覆盖交互 shell、SFTP、SCP 和 tunnel。 |
| 终端 ANSI / VT 渲染 | 待选型 | 是 | Desktop | 终端渲染不是普通文本框，不能假设 BCL 足够。 |
| 跨平台系统凭据库 | 待选型或分平台适配 | 是 | Infrastructure | Windows 可使用系统保护能力；macOS / Linux 需要单独适配。 |
| UI | Avalonia + AtomUI | 已确定 | Desktop | UI 类型不得进入 Core、Application、Session、Transfer、Infrastructure。 |

## 3. SSH.NET 选型边界

第一版 SSH 能力优先使用 SSH.NET。

允许 SSH.NET 承担：

- SSH 连接。
- 密码、私钥、keyboard-interactive 等认证适配。
- Host Key 校验事件或等价流程适配。
- 交互式 shell / terminal channel。
- SFTP 浏览和短操作。
- SFTP 上传、下载执行链路。
- Local、Remote、Dynamic SOCKS 端口转发。
- SCP 能力的后续预留。

SSH.NET 禁止泄漏到：

- `AtomSSH.Core`
- `AtomSSH.Application`
- `AtomSSH.Network`
- `AtomSSH.Transfer` 的公开契约
- `AtomSSH.Desktop` ViewModel

SSH.NET 类型只能出现在 `AtomSSH.Session` 的实现内部，或测试项目中针对 Session 实现的测试辅助代码。

## 4. 官方库优先落点

### Core

Core 只使用 .NET BCL。

禁止：

- Avalonia / AtomUI。
- SSH.NET。
- 终端控件。
- 数据库、日志、配置、文件 IO 实现。

### Application

Application 只依赖 Core 和必要的 DI 抽象。

允许：

- C# / BCL。
- 请求、结果、校验和用例编排代码。

禁止：

- SSH.NET。
- Avalonia / AtomUI。
- JSON 文件读写。
- 具体凭据加密实现。
- Transfer worker 或 Session runtime 具体类。

### Session

Session 是 SSH.NET 的主要落点。

允许：

- SSH.NET。
- 必要的 `Microsoft.Extensions.Logging.Abstractions`。
- BCL 网络、流和并发类型。

禁止：

- UI 类型。
- Application 用例服务。
- 本地配置文件格式。

### Transfer

Transfer 优先使用 BCL 并发和流能力。

允许：

- `Task`
- `CancellationToken`
- `Channel<T>`
- `Stream`
- `FileStream`
- `MemoryStream`

Transfer 不直接引用 SSH.NET。需要 SFTP 能力时通过 Core 端口消费 Session 暴露的运行时能力。

### Infrastructure

Infrastructure 优先使用官方库：

- `System.IO`
- `System.Text.Json`
- `System.IO.Compression`
- `System.Security.Cryptography`
- `Microsoft.Extensions.Logging`

跨平台凭据安全存储如果 BCL 不足，可以在该模块中引入分平台适配库，但必须由 `ICredentialStore` 和 `ICredentialResolver` 隔离。

### Desktop

Desktop 使用 Avalonia 和 AtomUI。UI 相关社区库只能进入 Desktop。

终端控件、图标、主题和桌面集成库不得进入 Core、Application、Session、Transfer 或 Infrastructure。

## 5. 暂缓选型项

以下能力第一版不急于确定最终库：

- 终端控件 / ANSI VT parser。
- 跨平台系统凭据库。
- 文件日志 sink。
- SQLite 或嵌入式数据库。
- OpenSSH agent / Pageant / 1Password / Bitwarden agent 集成。

暂缓不代表可以随意实现。进入代码前必须补充对应模块文档，并确认不会破坏现有依赖边界。

## 6. 禁止事项

- 不为了少写代码而让社区库类型进入 Core 模型或端口。
- 不在多个模块分别引用 SSH.NET。
- 不用 `ssh.exe` / `sftp.exe` 进程调用作为第一版核心 SSH 实现。
- 不在 ViewModel 中直接调用 SSH.NET、终端控件底层 channel、文件仓储或凭据仓储。
- 不在 Transfer worker 中直接规划连接路径或读取 network inventory。
- 不在 Infrastructure 中承载用户用例。

