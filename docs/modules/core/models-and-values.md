# AtomSSH.Core 模型和值对象

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的 Core 首批模型和值对象语义
>
> 变更规则：新增或调整核心模型时必须同步更新本文档。

## 1. 会话模型

`SshProfile` 表达一条可保存的 SSH 会话配置。

建议字段族：

- 标识：`SshProfileId`
- 显示：名称、分组、标签、备注
- 连接：host、port、username、proxy/jump host 引用
- 认证：认证方式、`CredentialRef`
- 终端：`TerminalProfile` 引用或内联快照
- 行为：启动命令、工作目录、编码、保持连接设置

`SshProfile` 不表达活动连接，不持有 SSH client。

## 2. 凭据模型

`CredentialRef` 是凭据引用，指向 Infrastructure 管理的加密凭据。它不能包含密码、私钥内容或 passphrase。

`CredentialKind` 第一版包含：

- Password
- PrivateKey
- PrivateKeyWithPassphrase
- KeyboardInteractive
- Agent

Agent 只作为模型预留，第一版可不实现完整 agent 集成。

## 3. Host Key 模型

`KnownHostEntry` 表达用户对某个 host key 的持久化信任决策。

模型必须能表达：

- host、port
- key type
- fingerprint
- first trusted time
- last seen time
- user decision source

Host key 变更不能被普通更新覆盖，必须走显式信任决策流程。

## 4. 终端模型

`TerminalProfile` 表达终端显示和行为偏好：

- 字体族、字号
- 主题引用
- 滚屏行数
- 光标样式
- 复制粘贴策略
- 默认编码
- 初始行列

终端模型不包含 Avalonia 控件对象。

## 5. SFTP 模型

`SftpItem` 是远程文件快照，不是远程文件活动代理对象。

建议字段：

- `RemotePath`
- 名称
- 类型
- 大小
- 修改时间
- 权限文本

`RemotePath` 必须不可变，路径拼接、父路径、文件名提取等规则应集中在值对象中。

传输任务模型分成两层：

- `SftpTransferTask` / `RemoteCopyTask`：可持久化任务描述，只表达用户要传什么、从哪里到哪里、覆盖策略和当前状态。
- `TransferExecutionPlan`：运行时执行计划，表达本次执行需要使用的 `ConnectionRoute`、任务 ID 和必要执行参数。

`TransferExecutionPlan` 不是长期业务事实。任务提交、重试或应用恢复后重新执行时，Application 可以重新规划路径并生成新的执行计划。

## 6. 端口转发模型

`PortForwardProfile` 是可保存配置，`PortForwardStatus` 是运行时状态快照。

第一版转发类型：

- Local
- Remote
- DynamicSocks

运行时 listener、socket、forward handle 不进入 Core 持久化模型。

## 7. 网络路径模型

`NetworkSpace` 表达一个可理解的运维网络空间，例如生产 VPC、测试 VPC 或办公室内网。

`NetworkNode` 表达一个可被 SSH 访问的目标节点。它可以是公网服务器、VPC 内网机器或跳板机。

`ConnectionRoute` 表达如何到达目标节点：

- Direct：直接连接目标 host:port。
- JumpHost：通过一个跳板机连接目标。
- ProxyJumpChain：通过多个跳板机串联连接目标。

`ConnectionRoute` 不是活动连接，不持有 SSH client、socket 或 channel。

`NetworkReachabilitySnapshot` 和 `NetworkDiagnosticResult` 是诊断结果快照，只表达可达性事实、失败阶段和脱敏错误原因，不持有 socket、SSH client 或原始异常。

## 8. 命令片段模型

`CommandSnippet` 表达用户保存的常用命令文本、分组和说明。它不是脚本运行时，不表达宏录制，也不直接持有终端 channel。

## 9. 导入导出模型

`ImportExportPackage` 表达 profile、settings、port forward profiles、network inventory、command snippets 等非敏感配置的导入导出包。

导入导出模型默认不包含 secret material。冲突必须通过 `ImportConflict` 或等价模型显式表达，不能静默覆盖已有配置。
