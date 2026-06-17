# Phase 12：非 UI 模块真实化与工程加固

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的非 Desktop 模块真实化目标、模块完成条件、禁止事项、验收规则和推进顺序
>
> 变更规则：实现阶段不得随意降低本文档定义的真实化标准；如需调整，必须同步更新 `docs/implementation/roadmap.md`、相关 architecture 文档和 modules 文档。

本文档承接 `docs/implementation/roadmap.md` 中 Phase 12。

Phase 0 到 Phase 11 的目标是建立可编译、依赖方向正确、主要链路可跑通的 MVP 骨架。Phase 12 的目标不是继续扩展 UI，而是把 `AtomSSH.Desktop` 之外的 Core、Application、Network、Session、Transfer、Infrastructure 从“最小闭环 / 骨架验证 / fake 默认实现”推进到“真实业务代码可作为 Desktop 基础能力使用”的状态。

## 1. 真实代码定义

本阶段所说“真实代码”指：

- 代码真实执行产品功能，不返回固定成功结果或模拟数据。
- 代码通过 Core 端口完成跨模块协作，不绕过架构边界。
- 代码能处理成功、失败、取消、资源释放和错误脱敏。
- 代码具备可验证测试，不只测试 DI 注册。
- 代码不把协议库对象、secret material、UI 类型或实现细节泄漏到错误边界之外。

以下代码不算真实完备：

- `Fake*` 默认注册作为生产路径。
- 只为了让测试通过而返回固定结果。
- 只实现 Direct route，而产品边界要求 JumpHost / ProxyJump。
- 保存 metadata 但完全无法保存或解析必要 secret。
- 只写任务状态，不真正执行任务。
- 只映射 happy path，不处理取消、失败、资源释放和脱敏。

## 2. 总体完成条件

Phase 12 完成时，除 `AtomSSH.Desktop` 外，所有非 UI 模块必须满足：

- 默认生产注册不再指向 fake runtime。
- fake 实现仅用于测试、开发演示或显式 fake composition。
- build / test 通过。
- 架构边界扫描通过：
  - SSH.NET 只能出现在 `AtomSSH.Session`。
  - Avalonia / AtomUI / ViewModel 不能出现在非 Desktop 模块。
  - `BuildServiceProvider()` 不能出现在内层模块。
- secret scan 通过：
  - 普通 JSON 配置不保存 password、private key body、passphrase、token。
  - 错误详情和日志路径经过脱敏。
- 真实路径测试覆盖：
  - 密码认证参数构造。
  - 私钥和 passphrase 参数构造。
  - Host key 未知和变更拦截。
  - Direct / JumpHost route 消费。
  - SFTP 列目录、上传、下载、删除。
  - Local / Remote / Dynamic SOCKS 端口转发生命周期。
  - LocalRelay 远端复制。
  - 网络清单保存、查询和路径规划。

## 3. 模块真实化清单

### 3.1 Core

目标：

- Core 继续保持纯净，只定义产品语言、端口、值对象和错误模型。
- 补齐真实业务需要的端口，不为了实现方便暴露 SSH.NET 或 UI 类型。

必须完成：

- 明确 SFTP stream lease、终端 channel、port forward instance、transfer progress 等跨模块契约。
- 明确错误脱敏工具或错误创建规范。
- 明确凭据 secret material 的运行时生命周期。

禁止：

- 引入 SSH.NET。
- 引入 Avalonia / AtomUI。
- 引入 JSON、本地文件格式或系统凭据库实现。

### 3.2 Session

目标：

- Session 成为真实 SSH 能力实现模块，基于 SSH.NET 完成 SSH、SFTP、端口转发和跳板机连接。

必须完成：

- 用户名 + 密码认证。
- 私钥认证。
- 私钥 + passphrase 认证。
- keyboard-interactive 的明确支持策略；未完整支持时必须返回结构化错误。
- agent 认证的明确支持策略；未完整支持时必须返回结构化错误。
- Host key 首次信任请求和 host key 变更拦截。
- Direct route 连接。
- JumpHost route 连接。
- ProxyJumpChain 至少返回清晰的未实现结构化错误，不能静默当作 Direct。
- 真实 PTY / terminal channel 输入、输出和 resize。
- 真实 SFTP list / delete / upload / download / open stream。
- Local / Remote / Dynamic SOCKS port forwarding 启停和释放。
- SSH.NET 异常统一映射到 `SshError`，错误详情必须脱敏。
- 所有 SSH client、SFTP client、shell stream、forwarded port 都必须有明确释放路径。

禁止：

- 把 SSH.NET 类型暴露到 Core、Application、Transfer、Network 或 Desktop ViewModel。
- 在 Session 中读取网络清单或自行规划 route。
- 在 Session 中保存凭据明文。

### 3.3 Transfer

目标：

- Transfer 执行真实上传、下载和远端到远端 LocalRelay 任务，不再只是写成功状态。

必须完成：

- 默认真实 scheduler。
- SFTP 上传任务执行。
- SFTP 下载任务执行。
- LocalRelay 远端复制。
- Running / Succeeded / Failed / Cancelled / Interrupted 状态落盘。
- 取消令牌贯穿执行链路。
- 失败时保存脱敏错误。
- 队列和历史通过 `ITransferStateStore` 查询。
- Transfer 不调用 `IConnectionRoutePlanner`，只消费 Application 生成的 `TransferExecutionPlan`。

禁止：

- 引用 SSH.NET。
- 自行读取 network inventory。
- 把完整文件无必要地加载进内存；大文件后续要走流式复制。
- 在第一版实现 rsync、scp 或 RemoteCommand 执行路径。

### 3.4 Network

目标：

- Network 真实表达 SSH 连接路径规划，而不是固定返回 Direct。

必须完成：

- Direct route 规划。
- JumpHost route 规划。
- ProxyJumpChain 模型校验和结构化未实现错误，或完成基础链路规划。
- 基于 `SshProfile.JumpHostProfileId` 和 network inventory 的路径选择。
- 网络诊断至少真实检查目标 host/port TCP 可达性。
- 诊断失败返回结构化、可展示、可重试错误。

禁止：

- 创建 SSH client。
- 引用 Session 或 Transfer。
- 修改云厂商 VPC、路由表、安全组或 VPN 配置。

### 3.5 Infrastructure

目标：

- Infrastructure 提供真实本地持久化、安全凭据存储、known hosts、导入导出和诊断基础。

必须完成：

- profile/settings/known hosts/transfer state/network inventory/command snippets/port forward profile 的 JSON 持久化。
- 凭据 metadata 和 secret material 分离保存。
- Windows 第一版必须使用系统保护能力或等价方案保存 password/private key/passphrase。
- macOS / Linux 如未实现系统凭据库，必须返回清晰配置错误，不得明文落盘。
- 导入导出默认不包含 secret material。
- JSON 损坏返回结构化配置错误，不静默覆盖。
- 配置目录创建失败返回结构化错误。

禁止：

- 明文保存 password、private key body、passphrase、token。
- 弹 UI。
- 调用 Application 用例。

### 3.6 Application

目标：

- Application 编排真实用例，不只转发端口调用。

必须完成：

- profile 保存、查询、删除基础校验。
- 打开终端前完成 route planning。
- SFTP 浏览前完成 route planning。
- Transfer 提交前生成完整 `TransferExecutionPlan`。
- RemoteCopy 提交前生成 source/target routes。
- PortForward 启动前完成 route planning。
- Network inventory 保存、查询、诊断用例。
- Command snippet 发送必须通过 terminal channel 边界。
- 所有返回给 UI 的错误必须脱敏。

禁止：

- 引用 SSH.NET。
- 引用 Avalonia / AtomUI。
- 持有长期 SSH client、SFTP client、port forward listener 或 transfer worker。

## 4. 默认 DI 真实化规则

Phase 12 完成前允许：

- `AddAtomSSHFakeSession()`、`FakeTransferTaskScheduler`、`FakeNetworkDiagnosticsService` 继续存在。
- 测试使用 fake 实现。

Phase 12 完成后必须：

- 生产默认注册指向真实实现。
- fake 注册必须改名为显式开发/测试入口，例如 `AddAtomSSHFakeSession()`、`AddAtomSSHFakeNetwork()`、`AddAtomSSHFakeTransfer()`。
- `AddAtomSSHSession()`、`AddAtomSSHNetwork()`、`AddAtomSSHTransfer()` 语义必须清晰：要么明确是生产真实注册，要么文档和命名不能让 Desktop 误用。

## 5. 推荐推进顺序

Phase 12 应按以下顺序推进：

1. Infrastructure 凭据 secret store。
2. Session 认证完整化和 host key 决策流。
3. Session route connector 完整化，包括 JumpHost / ProxyJumpChain 边界。
4. Session SFTP 和 port forwarding 真实能力补齐。
5. Transfer 真实 scheduler、取消、失败、LocalRelay 流式复制。
6. Network 真实 route planning 和 TCP diagnostics。
7. Application 用例校验和错误脱敏。
8. DI 默认注册真实化。
9. secret scan、边界扫描、集成测试和手工验证文档。

## 6. Phase 12 验收命令

至少执行：

```text
dotnet build AtomSSH.slnx --no-restore -m:1
dotnet test AtomSSH.slnx --no-build
rg -n "Renci|SSH\.NET|SshClient|SftpClient|ForwardedPort|ShellStream|ConnectionInfo" src\AtomSSH.Core src\AtomSSH.Application src\AtomSSH.Network src\AtomSSH.Transfer src\AtomSSH.Infrastructure
rg -n "Avalonia|AtomUI|ReactiveUI|ViewModel|BuildServiceProvider" src\AtomSSH.Core src\AtomSSH.Application src\AtomSSH.Network src\AtomSSH.Session src\AtomSSH.Transfer src\AtomSSH.Infrastructure
rg -n "password|passphrase|private key|token|secret" src tests docs
```

验收时不能只看命令是否为 0，还必须人工确认命中项是否合理。例如测试代码可以出现 `PasswordCredentialMaterial("secret")`，但生产普通配置文件写入路径不能出现 secret material。

## 7. 与 Desktop 的关系

Phase 12 不实现 Desktop UI。

Phase 12 的交付目标是让 Desktop 后续接入时面对的 Application 服务和底层端口已经足够真实，不需要 UI 层再补协议、存储、传输或诊断逻辑。

Desktop 仍然只负责：

- 展示状态。
- 收集用户输入。
- 调用 Application 用例。
- 呈现错误和确认弹窗。
- 触发关闭流程。

Desktop 不得因为 Phase 12 未完成而直接调用 Session、Transfer、Network、Infrastructure 的底层实现。
