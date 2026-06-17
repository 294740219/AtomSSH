# AtomSSH 生命周期设计

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的业务对象、运行时服务、凭据、会话、端口转发、传输任务和模块启动停止生命周期
>
> 变更规则：实现阶段不得随意修改本文档定义的生命周期边界；如需调整，必须同步更新 architecture 和 modules 文档。

生命周期文档回答一个问题：某个对象应该活多久，由谁创建，由谁释放，能不能持久化。

## 1. 生命周期分类

| 类型 | 例子 | 生命周期 |
| --- | --- | --- |
| 持久化业务对象 | `SshProfile`、`CredentialRef`、`PortForwardProfile`、`TerminalProfile` | 保存到本地仓储，跨应用重启存在。 |
| 值对象 | `SshProfileId`、`RemotePath`、`LocalPath`、`HostName` | 不由 DI 管理，不可变。 |
| 用例请求 / 结果 | `OpenTerminalRequest`、`ListSftpItemsResult` | 单次调用内存在，不持有外部资源。 |
| 运行时会话 | SSH connection、PTY channel、SFTP channel | 从连接开始到断开结束，必须显式释放。 |
| 传输任务 | `SftpTransferTask`、`RemoteCopyTask` | 任务描述可持久化，执行 worker 不持久化。 |
| UI 状态 | 当前标签、选中项、加载状态 | 跟随页面或窗口生命周期。 |
| 技术服务 | 仓储、凭据存储、日志服务 | 由 DI 管理，生命周期按职责单独决定。 |

## 2. SSH 会话生命周期

- `SshProfile` 是持久化配置，不代表一个活动连接。
- 活动 SSH 连接由 Session Runtime 创建，不能保存到 Application 或 ViewModel。
- 一个终端标签通常对应一个 `SshSessionInstance` 和一个 PTY channel。
- SFTP 浏览可以使用独立 SFTP channel；不得假定它和终端 PTY 必须共享同一个 channel。
- 连接断开、认证失败、host key 变化必须转换为 Core 错误模型后向外传播。
- 应用关闭时，Desktop 先停止接受新操作，再请求 Session Runtime 关闭活动连接。

## 3. 凭据生命周期

- `CredentialRef` 是引用，不是凭据明文。
- 密码、私钥 passphrase、私钥内容不得进入普通配置、日志、错误详情或测试输出。
- 凭据解析能力由 Core 端口表达，具体实现属于 Infrastructure 或专门安全适配实现；Session 和 Transfer 只在执行链路中短期消费解析结果。
- `CredentialLease` 只表达运行时占用，不进入持久化。
- 凭据删除时必须检查是否仍有活动会话或运行中传输引用。

## 4. Host Key 生命周期

- `KnownHostEntry` 是持久化安全决策。
- 首次连接未知 host key 时，Session Runtime 返回需要信任决策的结构化结果。
- Host key 变更必须被拦截，不允许自动覆盖。
- UI 负责展示指纹和风险提示；Application 和 Session 不知道弹窗存在。
- 用户确认信任后，通过 Application 用例写入 known hosts。

## 5. 端口转发生命周期

- `PortForwardProfile` 是持久化配置。
- `PortForwardInstance` 是运行时 listener 或 remote forward handle，不能持久化。
- 本地端口占用、远程转发拒绝、SOCKS 初始化失败必须返回结构化错误。
- 应用关闭时必须先停止 listener，再断开关联 SSH 会话。
- 端口转发实例不能注册成全局 Singleton。

## 6. SFTP 传输生命周期

- `SftpTransferTask` 是上传 / 下载任务描述，保存 profile ID、本地路径、远程路径、方向、覆盖策略和状态。
- `RemoteCopyTask` 是远端到远端复制任务描述，保存源 profile、目标 profile、源路径、目标路径、复制模式、覆盖策略和状态。
- 传输任务不能保存 SSH client、SFTP client、凭据明文或完整 profile 快照。
- Transfer Worker 执行任务时消费 Application 提交的运行时执行计划，按需创建短生命周期连接或 channel。
- 运行时执行计划可以包含已规划 `ConnectionRoute`，但不能包含 SSH client、SFTP client、凭据明文或完整 profile 快照。
- 运行中任务在应用关闭时必须落为可解释状态，例如 `Interrupted`。
- 队列和历史展示只能通过 Application 查询快照进入 UI。

## 7. DI 生命周期约束

- Repository、Settings、KnownHosts store 可以是长期服务，但必须线程安全或明确串行访问。
- Application service 可以是无状态长期服务。
- SSH connection、SFTP channel、terminal channel、port forward listener、Transfer worker 不得作为全局 Singleton。
- 模块可以暴露 `IServiceCollection` 注册扩展，但不能在模块内部 `BuildServiceProvider()`。
- 普通业务对象不得直接注入 `IServiceProvider` 后自行解析依赖。

## 8. Presentation 生命周期

- Desktop 是唯一生产组合根。
- ViewModel 生命周期跟随页面、窗口、标签或工作区。
- ViewModel 只能保存 UI 状态，不保存 secret material 或协议库对象。
- 终端标签关闭时必须通过 Application 请求关闭对应会话。
- 关闭应用时，Presentation 负责触发关闭流程，但不直接操作底层连接对象。
