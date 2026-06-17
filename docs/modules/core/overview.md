# AtomSSH.Core 模块设计

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的 Core 模块边界、目录规划、首批核心对象和实现约束
>
> 变更规则：实现阶段不得随意修改本文件；如需调整 Core 边界，必须同步更新 architecture 和 modules 文档。

## 1. 模块定位

`AtomSSH.Core` 是 AtomSSH 的产品内核。它定义 SSH 管理领域的统一语言、核心模型、值对象、跨模块稳定端口、错误模型和纯领域规则。

Core 不是公共工具库。不能把 UI 绑定对象、协议库 DTO、文件系统工具或日志实现塞进 Core。

## 2. 边界规则

Core 允许包含：

- SSH profile、credential ref、terminal profile、port forward profile、network route、SFTP item、transfer task、command snippet 等核心模型。
- Host、端口、路径、ID、指纹等值对象。
- runtime、store、scheduler、route planner、diagnostics 等跨模块稳定端口定义。
- 结果模型、错误模型、能力模型。
- 无外部依赖的领域规则。

Core 禁止包含：

- Avalonia、AtomUI、View、ViewModel、Command、Binding。
- SSH.NET、终端控件、SFTP client、系统凭据库具体实现。
- JSON 配置读写、SQLite、文件系统持久化、日志实现。
- DI 注册、应用启动和模块初始化。

## 3. 推荐目录

```text
src/AtomSSH.Core/
  Profiles/
  Credentials/
  Hosts/
  Sessions/
  Terminal/
  Sftp/
  PortForwarding/
  Network/
  Transfers/
  CommandSnippets/
  ImportExport/
  Results/
  Errors/
  Settings/
  ValueObjects/
```

## 4. 首批核心对象范围

会话与主机：

- `SshProfile`
- `SshProfileId`
- `SshEndpoint`
- `SshAuthMethod`
- `SshProfileGroup`

凭据与安全：

- `CredentialRef`
- `CredentialKind`
- `CredentialLease`
- `KnownHostEntry`
- `HostKeyFingerprint`
- `HostKeyTrustDecision`

终端：

- `TerminalProfile`
- `TerminalSize`
- `TerminalThemeRef`
- `TerminalScrollbackSettings`

SFTP 与传输：

- `RemotePath`
- `LocalPath`
- `SftpItem`
- `SftpItemKind`
- `SftpTransferTask`
- `RemoteCopyTask`
- `RemoteCopyMode`
- `TransferExecutionPlan`
- `TransferTaskId`
- `TransferStatus`
- `TransferProgress`

端口转发：

- `PortForwardProfile`
- `PortForwardKind`
- `PortForwardEndpoint`
- `PortForwardStatus`

网络与连接路径：

- `NetworkSpace`
- `NetworkNode`
- `NetworkNodeId`
- `NetworkAddress`
- `ConnectionRoute`
- `ConnectionRouteKind`
- `JumpHostRoute`
- `ProxyJumpChain`
- `NetworkReachabilitySnapshot`
- `NetworkDiagnosticResult`
- `RoutePlanningError`

命令片段与导入导出：

- `CommandSnippet`
- `CommandSnippetId`
- `ImportExportPackage`
- `ImportConflict`

端口：

- `ISshProfileRepository`
- `ICredentialStore`
- `ICredentialResolver`
- `IHostKeyTrustStore`
- `ISshSessionRuntime`
- `ISshSessionFactory`
- `ISftpBrowser`
- `IPortForwardRuntime`
- `IPortForwardProfileRepository`
- `ITransferTaskStore`
- `ITransferStateStore`
- `ITransferTaskScheduler`
- `INetworkInventoryStore`
- `IConnectionRoutePlanner`
- `INetworkDiagnosticsService`
- `ICommandSnippetRepository`
- `IImportExportService`
- `IApplicationSettingsRepository`

## 5. 设计约束

- 远程路径使用 `RemotePath`，本地路径使用 `LocalPath`。
- `CredentialRef` 只能表达引用，不能保存 secret material。
- `SshProfile` 保存连接配置，不保存运行中 SSH client。
- `SftpTransferTask` 保存任务描述，不保存 SFTP client 或完整 profile 快照。
- `RemoteCopyTask` 保存源/目标 profile 和路径，不保存两个 SFTP client。
- `ConnectionRoute` 是连接路径描述，不是活动连接。
- Core 错误模型不持有原始 exception。
- 跨模块稳定端口的方法签名只能使用 Core 类型和 .NET BCL 类型。
- Core 不做 DI 注册。
- Core 端口只定义契约，不提供存储、SSH 协议、传输 worker、网络诊断或日志实现。
- Core 端口不能成为外层模块互相绕行的后门；Desktop 不能直接消费这些端口，Application 也不能依赖具体实现项目。
