# AtomSSH 产品功能设计

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的产品功能模块、功能边界、版本范围和产品模块到工程模块的映射
>
> 变更规则：新增、删除或调整产品功能模块时必须同步更新本文档，并检查 architecture、modules、ui 和 roadmap 文档是否需要同步调整。

本文档是 AtomSSH 的产品功能总账。它只回答“产品要做什么、边界在哪里、和工程模块如何对应”，不替代架构文档、模块文档或 UI 文档。

AtomSSH 的产品主线是 SSH 终端管理工具。所有能力都必须服务于 SSH 连接、终端操作、文件传输、端口转发、凭据安全和运维效率。

## 1. 产品定位

AtomSSH 是一款基于 Avalonia 和 AtomUI 的跨平台 SSH 终端管理应用。

产品核心能力：

- 管理 SSH 会话。
- 打开多标签终端。
- 管理凭据、私钥和 Host Key。
- 通过 SFTP 浏览和传输文件。
- 支持跳板机、ProxyJump、代理链和连接路径诊断。
- 支持本地、远程和动态 SOCKS 端口转发。
- 支持远端到远端文件复制。
- 提供命令片段、日志、设置和诊断能力。

产品非目标：

- 不做云管平台。
- 不做 VPN 客户端。
- 不做堡垒机审计平台。
- 不做 RDP / VNC / Telnet / Serial 全协议远程工具。
- 不做第三方网络控制面管理。
- 不做企业级集中策略下发和团队资产平台。

## 2. 产品功能地图

| 产品模块 | 核心职责 | MVP 范围 | 延后范围 |
| --- | --- | --- | --- |
| 会话管理 | 保存、组织、搜索和打开 SSH profile | 新增、编辑、删除、复制、分组、连接测试 | 批量编辑、云同步、团队共享 |
| 终端工作区 | 多标签 SSH 终端和会话状态 | 打开、关闭、断开、重连、复制粘贴、滚屏、主题 | 复杂终端仿真矩阵、会话录像 |
| 凭据与安全 | 凭据、私钥、Host Key、known hosts | 密码、私钥、passphrase、首次信任、变更拦截、脱敏 | 硬件密钥、企业 KMS、集中密钥策略 |
| SFTP 文件管理 | 远程文件浏览和基础操作 | 列目录、上传、下载、删除、重命名、创建目录 | 递归目录传输、预览、搜索、压缩 |
| 传输队列 | 文件任务调度和历史 | 上传、下载、进度、取消、失败、历史 | 断点续传、复杂并发、全局限速 |
| 远端到远端复制 | 两台远程机器之间传文件 | LocalRelay 模式、任务进度、错误详情 | 高级 rsync 编排、自动选择最优路径 |
| 连接路径 | 跳板机、ProxyJump、代理链和诊断 | 直连、跳板机、ProxyJump、可达性诊断 | 复杂策略路由、自动拓扑发现 |
| 端口转发 | SSH tunnel 管理 | Local、Remote、Dynamic SOCKS 启停和状态 | 流量统计、规则模板、批量转发 |
| 命令片段 | 常用命令保存和发送 | 分组、编辑、发送到当前终端 | 宏录制、多会话广播、脚本引擎 |
| 设置与诊断 | 应用偏好、日志和故障定位 | 终端默认值、日志目录、配置路径、错误详情 | 插件、完整遥测、远程诊断平台 |
| 导入导出 | 数据迁移和备份 | profile、settings、port forward profiles、network inventory、command snippets 导入导出 | 第三方 SSH 工具完整迁移适配、敏感数据加密导出 |

## 3. 会话管理

### 产品目标

会话管理负责把服务器连接信息保存成可重复使用的 SSH profile。用户不应该反复输入 host、port、username、认证方式、跳板路径和终端偏好。

### MVP 功能

- 新增 SSH profile。
- 编辑 SSH profile。
- 删除 SSH profile。
- 复制 SSH profile。
- 按分组管理。
- 按名称、host、标签搜索。
- 连接测试。
- 从 profile 一键打开终端。
- 从 profile 打开 SFTP。

### 产品边界

- Profile 是连接配置，不是活动连接。
- 删除 profile 前必须检查活动会话、端口转发和未完成传输任务引用。
- Profile 可以引用凭据，但不能保存凭据明文。
- Profile 可以引用跳板机或连接路径，但不保存运行时 SSH client。

### 工程映射

| 产品概念 | Core | Application | 实现模块 / 持久化 | Presentation |
| --- | --- | --- | --- | --- |
| SSH profile | `SshProfile`、`SshProfileId` | `ProfileAppService` | `ISshProfileRepository` | 会话树、会话管理页、会话弹窗 |
| 分组 | `SshProfileGroup` | profile list / move use cases | `ISshProfileRepository` | Navigation Tree |
| 连接测试 | `SshEndpoint`、`OperationResult` | `SessionAppService.TestConnection` | Session Runtime | 测试连接按钮、错误详情 |
| 删除保护 | profile/task/session reference models | delete profile use case | `ITransferStateStore`、Session state query | 删除确认弹窗 |

## 4. 终端工作区

### 产品目标

终端工作区是 AtomSSH 的核心工作区，负责承载多个 SSH 终端标签，并展示连接状态、断开、重连和关闭流程。

### MVP 功能

- 多标签终端。
- 打开 profile 创建终端标签。
- 关闭终端标签。
- 断开和重连。
- 显示连接中、认证中、已连接、断开、失败等状态。
- 基础复制粘贴。
- 滚屏设置。
- 字体、字号、主题。
- 窗口尺寸变化同步到远端 PTY。

### 产品边界

- 终端标签不是 SSH 连接对象本身。
- ViewModel 不保存 SSH client、shell stream 或 channel。
- 终端控件只属于 Presentation。
- 重连可以先实现为关闭旧会话再创建新会话。

### 工程映射

| 产品概念 | Core | Application | 实现模块 / 持久化 | Presentation |
| --- | --- | --- | --- | --- |
| 终端标签 | `SshSessionInstanceId`、session snapshot | `SessionAppService.OpenTerminal` | `ISshSessionRuntime` | Terminal Workspace、Tab ViewModel |
| 终端配置 | `TerminalProfile`、`TerminalSize` | settings/profile use cases | `IApplicationSettingsRepository` | 终端设置页、终端控件适配 |
| 输入输出 | terminal input/output handles | session use cases | Session terminal channel | terminal view adapter |
| 状态 | `SessionStateSnapshot` | query session state | Session Runtime | 标签状态、状态栏 |

## 5. 凭据与安全

### 产品目标

凭据与安全模块负责管理 SSH 登录所需的敏感信息和远端身份校验。它是产品可信度的基础。

### MVP 功能

- 密码凭据。
- 私钥凭据。
- 私钥 passphrase。
- 凭据引用。
- Host Key 首次信任。
- Host Key 变更拦截。
- known hosts 管理。
- 错误详情和日志脱敏。

### 产品边界

- `CredentialRef` 不是密码。
- 密码、私钥内容、passphrase 不能进入普通配置文件。
- Host Key 变化不能自动覆盖。
- 错误详情不能包含 secret material。

### 工程映射

| 产品概念 | Core | Application | 实现模块 / 持久化 | Presentation |
| --- | --- | --- | --- | --- |
| 凭据引用 | `CredentialRef`、`CredentialKind` | profile / credential use cases | `ICredentialStore` | 凭据输入控件 |
| 凭据解析 | `CredentialLease` | session open orchestration | `ICredentialResolver` | 不直接接触 secret |
| Host Key | `KnownHostEntry`、`HostKeyFingerprint` | trust decision use cases | `IHostKeyTrustStore`、Session HostKey verifier | 首次信任/变更弹窗 |
| 脱敏错误 | `SshError` | error result shaping | Infrastructure logging redactor | 错误详情弹窗 |

## 6. SFTP 文件管理

### 产品目标

SFTP 文件管理让用户在 SSH 会话上下文中浏览远程文件，并创建上传、下载和删除等操作。

### MVP 功能

- 列目录。
- 返回上级目录。
- 刷新。
- 创建目录。
- 重命名。
- 删除文件或目录。
- 上传文件。
- 下载文件。
- 从当前终端会话打开 SFTP。

### 产品边界

- SFTP 页面展示远程文件快照，不持有 SFTP client。
- 上传和下载只创建传输任务，不由 UI 直接执行。
- 删除远程文件必须确认。
- SFTP 错误必须映射为统一错误结果。

### 工程映射

| 产品概念 | Core | Application | 实现模块 / 持久化 | Presentation |
| --- | --- | --- | --- | --- |
| 远程路径 | `RemotePath` | `SftpAppService` | Session SFTP channel | 路径栏 |
| 文件项 | `SftpItem`、`SftpItemKind` | list items use case | `ISftpBrowser` | 文件列表行 |
| 删除/重命名 | SFTP operation result | SFTP use cases | Session SFTP operations | 操作按钮、确认弹窗 |
| 上传/下载入口 | `SftpTransferTask` | transfer creation use cases | Transfer scheduler | 上传/下载按钮 |

## 7. 传输队列

### 产品目标

传输队列负责把上传、下载和远端复制变成可观察、可取消、可重试的任务。

### MVP 功能

- 创建上传任务。
- 创建下载任务。
- 队列展示。
- 进度展示。
- 取消任务。
- 失败原因展示。
- 历史记录。
- 重试失败任务。

### 产品边界

- Transfer task 是任务描述，不是 worker。
- Worker 不保存到持久化数据。
- UI 不能直接访问 Transfer 内部队列。
- 应用关闭时运行中任务必须落为可解释状态。

### 工程映射

| 产品概念 | Core | Application | 实现模块 / 持久化 | Presentation |
| --- | --- | --- | --- | --- |
| 任务 | `SftpTransferTask`、`TransferTaskId` | `TransferAppService` | `ITransferTaskStore` | 队列页、历史页 |
| 状态 | `TransferStatus`、`TransferProgress` | queue/history use cases | `ITransferStateStore` | 进度条、状态列 |
| 调度 | `ITransferTaskScheduler` | create/cancel/retry use cases | Transfer Runtime | 操作按钮 |
| 执行计划 | `TransferExecutionPlan` | route planning before submit/retry | planned route consumed by Transfer Runtime | 不直接展示 |
| 历史 | task/state snapshots | history query | `ITransferStateStore` | 历史记录页 |

## 8. 远端到远端复制

### 产品目标

远端到远端复制解决云 VPC 内两台机器之间传文件不方便的问题。用户通过选择源机器、源路径、目标机器和目标路径创建复制任务。

### MVP 功能

- 选择源 profile。
- 选择源文件或目录。
- 选择目标 profile。
- 选择目标路径。
- 选择覆盖策略。
- 使用 `LocalRelay` 模式传输。
- 展示队列、进度和失败原因。

### 延后功能

- `RemoteCommand` 模式。
- 自动选择最优传递路径。
- rsync 增量同步。
- 目录递归复制。

### 产品边界

- MVP 优先 `LocalRelay`：源服务器 SFTP 流经本机，再上传目标服务器。
- `LocalRelay` 不要求源和目标服务器彼此可达。
- 任务不能保存两个 SFTP client。
- 如需临时文件，必须定义清理规则。

### 工程映射

| 产品概念 | Core | Application | 实现模块 / 持久化 | Presentation |
| --- | --- | --- | --- | --- |
| 远端复制任务 | `RemoteCopyTask` | `TransferAppService.CreateRemoteCopy` | Transfer Runtime | 远端复制页 |
| 源/目标 | source/target profile IDs、paths | remote copy request | `ISshProfileRepository`、`ISshSessionFactory` | 源目标选择器 |
| 模式 | `RemoteCopyMode` | request validation | LocalRelay worker | 模式选择 |
| 执行计划 | `TransferExecutionPlan` | source/target route planning | source/target route consumed by LocalRelay worker | 不直接展示 |
| 进度 | `TransferProgress` | queue snapshot | `ITransferStateStore` | 队列/历史 |

## 9. 连接路径

### 产品目标

连接路径模块负责把直连、跳板机、ProxyJump、代理链和 VPC 内网目标组织成可解释的 SSH 连接路径。

### MVP 功能

- 直连。
- 跳板机。
- ProxyJump。
- 代理链模型。
- VPC 内网目标手工登记。
- 连接路径诊断。
- 从网络清单打开 SSH。

### 产品边界

- Network 只规划路径，不创建 SSH client。
- 跳板机可以是普通 SSH profile 的一种角色。
- VPC 内网目标是 SSH 目标，不代表 AtomSSH 管理云网络。
- 路径诊断只诊断 SSH 可达性，不修改云安全组、路由表或 VPN 配置。

### 工程映射

| 产品概念 | Core | Application | 实现模块 / 持久化 | Presentation |
| --- | --- | --- | --- | --- |
| 网络节点 | `NetworkNode`、`NetworkSpace` | `NetworkInventoryAppService` | `INetworkInventoryStore` | 网络清单页 |
| 跳板机 | `JumpHostRoute`、profile role | route planning use cases | `INetworkInventoryStore`、Network planner | 路径配置 UI |
| 连接路径 | `ConnectionRoute` | route planner orchestration | Network planner / diagnostics | 路径诊断面板 |
| 诊断 | `NetworkDiagnosticResult` | diagnostics use case | network diagnostics service | 错误详情、建议 |

## 10. 端口转发

### 产品目标

端口转发模块负责通过 SSH tunnel 暴露远端服务、本地服务或动态 SOCKS 代理。

### MVP 功能

- Local forwarding。
- Remote forwarding。
- Dynamic SOCKS。
- 启动转发。
- 停止转发。
- 查看运行状态。
- 展示端口占用、认证失败、远端拒绝等错误。

### 产品边界

- `PortForwardProfile` 是配置。
- `PortForwardInstance` 是运行时。
- 端口转发 listener 不能注册成全局 Singleton。
- 应用关闭时必须释放 listener。

### 工程映射

| 产品概念 | Core | Application | 实现模块 / 持久化 | Presentation |
| --- | --- | --- | --- | --- |
| 转发配置 | `PortForwardProfile` | `PortForwardAppService` | Infrastructure port forward repository | 端口转发页 |
| 运行实例 | `PortForwardStatus` | start/stop use cases | `IPortForwardRuntime` | 状态列表 |
| 错误 | `SshError` | result shaping | Session Runtime | 错误详情 |

## 11. 命令片段

### 产品目标

命令片段用于保存常用命令，并快速发送到当前活动终端。

### MVP 功能

- 新增命令片段。
- 编辑命令片段。
- 删除命令片段。
- 分组。
- 发送到当前活动终端。

### 延后功能

- 宏录制。
- 多会话广播。
- 脚本引擎。
- 参数化命令模板。

### 产品边界

- 命令片段是文本配置，不是脚本运行时。
- MVP 只发送到当前活动终端。
- 发送命令必须通过终端会话边界，不直接访问协议库 channel。

### 工程映射

| 产品概念 | Core | Application | 实现模块 / 持久化 | Presentation |
| --- | --- | --- | --- | --- |
| 命令片段 | `CommandSnippet` | snippet use cases | `ICommandSnippetRepository` | 命令片段页 |
| 发送命令 | terminal input abstraction | session command use case | terminal channel | 发送按钮 |

## 12. 设置与诊断

### 产品目标

设置与诊断模块负责应用偏好、终端默认值、日志路径、配置位置和故障定位。

### MVP 功能

- 终端默认设置。
- 主题和外观设置。
- 配置目录展示。
- 日志目录展示。
- 错误详情。
- 启动失败诊断。
- 配置损坏提示。

### 产品边界

- Infrastructure 不弹 UI。
- 设置保存必须通过 Application 用例。
- 日志不得包含 secret material。
- 启动关键依赖失败时不能进入正常 Shell。

### 工程映射

| 产品概念 | Core | Application | 实现模块 / 持久化 | Presentation |
| --- | --- | --- | --- | --- |
| 应用设置 | `ApplicationSettings` | `SettingsAppService` | `IApplicationSettingsRepository` | 设置页 |
| 日志 | error/log models | diagnostics use cases | logging service | 日志入口、诊断页 |
| 启动诊断 | startup error result | startup orchestration | config/storage services | 启动错误界面 |

## 13. 导入导出

### 产品目标

导入导出用于本地备份、迁移和恢复 profile、设置和非敏感配置。

### MVP 功能

- 导出 profile。
- 导入 profile。
- 导出应用设置、端口转发配置。
- 导入应用设置、端口转发配置。
- 导出和导入 network inventory。
- 导出和导入 command snippets。
- 导出时排除 secret material。

### 产品边界

- 凭据导出默认不包含 secret material。
- 如未来支持敏感数据导出，必须有显式加密和用户确认。
- 导入不能静默覆盖已有 profile。

### 工程映射

| 产品概念 | Core | Application | 实现模块 / 持久化 | Presentation |
| --- | --- | --- | --- | --- |
| 导入导出包 | export DTOs / validation models | import/export use cases | Infrastructure import/export service | 导入导出页 |
| 冲突处理 | conflict result | import preview use case | Infrastructure repositories | 导入冲突确认 UI |

## 14. MVP 收敛清单

MVP 完成时，AtomSSH 至少应具备：

- 保存一台 SSH 服务器并一键打开终端。
- 使用密码或私钥完成认证。
- 首次 Host Key 信任和 Host Key 变更拦截。
- 多标签终端、断开、关闭、重连。
- SFTP 列目录、上传、下载、删除。
- 传输队列和历史。
- 本地端口转发和动态 SOCKS。
- 跳板机连接 VPC 内网机器。
- 远端到远端 `LocalRelay` 文件复制。
- 错误详情、日志脱敏和设置页。

## 15. 产品到工程的总映射

| 产品模块 | Core | Application | Session | Transfer | Network | Infrastructure | Desktop |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 会话管理 | profile models | ProfileAppService | 连接测试消费 profile | 引用 profile | route planning input | `ISshProfileRepository` | 会话树、管理页 |
| 终端工作区 | session IDs / terminal I/O contracts | SessionAppService | SSH/PTY/channel runtime，消费已规划 route | 无 | planner/diagnostics | 无 | terminal views |
| 凭据与安全 | credential/host key models | trust/credential use cases | 认证和 Host Key 校验 | 凭据 lease 消费 | 无 | credential/known hosts store | 凭据 UI、信任弹窗 |
| SFTP 文件管理 | SFTP item/path models | SftpAppService | SFTP operations，消费已规划 route | 创建任务 | planner/diagnostics | 无 | SFTP browser |
| 传输队列 | task/progress/execution plan models | TransferAppService | SFTP channel provider | scheduler/worker/state，消费执行计划 | planner/diagnostics | `ITransferTaskStore`、`ITransferStateStore` | 队列/历史 |
| 远端到远端复制 | remote copy task/execution plan models | remote copy use case | source/target sessions | LocalRelay worker，消费执行计划 | source/target route planning | temp/cache if needed | remote copy page |
| 连接路径 | route/network models | NetworkInventoryAppService | route consumer | route consumer | planner/diagnostics | network inventory store | network inventory |
| 端口转发 | forwarding models | PortForwardAppService | tunnel runtime，消费已规划 route | 无 | planner/diagnostics | port forward repository | port forwarding page |
| 命令片段 | snippet model | snippet use cases | terminal input | 无 | 无 | `ICommandSnippetRepository` | command snippets page |
| 设置与诊断 | settings/error models | Settings/Diagnostics services | runtime errors | transfer errors | route errors | settings/logging/config | settings/diagnostics UI |
| 导入导出 | validation models | import/export use cases | 无 | 无 | 无运行时职责 | Infrastructure import/export service | import/export page/dialogs |
