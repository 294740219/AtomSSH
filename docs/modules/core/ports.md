# AtomSSH.Core 端口设计

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的跨模块稳定端口归属和职责
>
> 变更规则：新增跨模块端口时必须优先更新本文档。

Core 端口按能力族组织，不按外层消费者组织。

## 0. 端口归属原则

Core 定义端口是为了稳定跨模块契约，不代表 Core 承担存储、网络、SSH 运行时或后台任务职责。

- 端口只能表达“外层模块需要提供什么能力”，不能表达“由 Core 执行什么技术实现”。
- 端口方法签名只能使用 Core 类型和 .NET BCL 类型，不能出现 Avalonia、AtomUI、SSH 协议库、数据库、日志框架或具体文件格式类型。
- Repository / Store 端口由 Infrastructure 实现。
- SSH session、SFTP、PTY 和 port forwarding runtime 端口由 Session 实现。
- Transfer scheduler 和 worker 端口由 Transfer 实现。
- Route planner 和 diagnostics 端口由 Network 实现。
- Application 只编排端口，不实现持久化、协议连接或后台 worker。
- Desktop 只通过 Application 服务间接使用端口，不能直接解析或调用底层端口实现。

## 1. 仓储端口

- `ISshProfileRepository`：保存、更新、删除、查询 SSH profile 和分组。
- `IApplicationSettingsRepository`：保存和读取应用设置。
- `IHostKeyTrustStore`：保存 known hosts 和 host key 信任决策。
- `ITransferTaskStore`：保存上传、下载和远端复制等传输任务描述。
- `ITransferStateStore`：保存和查询传输状态快照。
- `INetworkInventoryStore`：保存网络空间、内网节点、跳板机引用和连接路径元数据。
- `ICommandSnippetRepository`：保存命令片段和分组。

仓储端口只能使用 Core 类型和 BCL 类型。

## 2. 凭据端口

- `ICredentialStore`：保存、更新、删除凭据密文和元数据。
- `ICredentialResolver`：按 `CredentialRef` 解析运行时凭据材料。

运行时凭据材料必须有明确释放语义，不能长期缓存到 Application 或 ViewModel。

## 3. 会话端口

- `ISshSessionRuntime`：创建、查询、关闭活动 SSH 会话。
- `ISshSessionFactory`：基于 profile 和凭据创建短生命周期连接。
- `ITerminalChannel`：表达受控终端输入输出边界，不暴露协议库对象，不作为 ViewModel 可长期持有的底层运行时对象。
- `ISftpBrowser`：列目录、创建目录、删除、重命名等短 SFTP 操作。

Session 端口返回会话实例 ID、状态快照或受控 I/O 句柄，不能泄漏具体 SSH client、shell stream、PTY channel 或 SFTP client 类型。

## 4. 端口转发端口

- `IPortForwardRuntime`：启动、停止、查询端口转发实例。
- `IPortForwardProfileRepository`：保存端口转发配置。实现阶段可以将端口转发作为独立仓储，也可以作为 profile 的子配置持久化，但 Core 端口语义必须保持独立。

## 5. 网络路径端口

- `IConnectionRoutePlanner`：根据目标 profile、网络清单、跳板机和用户选择生成 `ConnectionRoute`。
- `INetworkDiagnosticsService`：诊断直连、跳板机、代理链和目标 SSH 端口的可达性。

Network 端口只返回路径描述和诊断结果，不创建 SSH client、SFTP channel 或 Transfer worker。

## 6. 传输端口

- `ITransferTaskScheduler`：提交、取消、重试传输任务。提交执行时必须接收任务描述和 `TransferExecutionPlan` 或等价运行时执行计划。

队列和历史快照查询复用仓储端口中的 `ITransferStateStore`，不得另行定义第二套传输状态读取端口。

Transfer 运行时对象不通过端口暴露给 UI。Transfer 端口不得包含 `IConnectionRoutePlanner` 调用语义；路径规划由 Application 在提交、重试或恢复任务前完成。`RemoteCopyMode.RemoteCommand`、scp 和 rsync 相关能力只允许作为后续增强的模型预留，第一版端口不得要求 Transfer 实现远端命令执行路径。

## 7. 导入导出端口

- `IImportExportService`：导入导出 profile、settings、port forward profiles、network inventory、command snippets 和其他非敏感配置。

导出默认不得包含 secret material。如未来支持敏感数据导出，必须使用显式加密和用户确认。

## 8. 错误与结果

所有跨模块调用优先返回 `OperationResult<T>`。错误使用统一 `SshError` 或等价模型表达：

- 类别：认证、网络、HostKey、权限、路径、端口占用、协议、取消、内部错误。
- 摘要：可显示给用户。
- 详情：脱敏后用于诊断。
- 可重试性：供 UI 决定重试入口。
