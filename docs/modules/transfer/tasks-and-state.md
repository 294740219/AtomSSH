# AtomSSH.Transfer 任务与状态

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的传输任务和状态语义
>
> 变更规则：调整传输状态机时必须同步更新本文档。

## 1. 任务描述

第一版传输任务分为两类描述：

- `SftpTransferTask`：本机与远端之间的上传 / 下载任务。
- `RemoteCopyTask`：两台远端机器之间的复制任务。

`SftpTransferTask` 保存：

- task ID
- profile ID
- 方向：upload / download
- local path
- remote path
- overwrite policy
- created time
- current status

`RemoteCopyTask` 保存：

- task ID
- source profile ID
- target profile ID
- remote source path
- remote target path
- remote copy mode
- overwrite policy
- created time
- current status

任务不能保存 SSH client、SFTP client、凭据明文或完整 profile 快照。

持久化任务描述只保存 profile ID 和路径等业务意图，不保存运行时连接对象。执行任务时必须由 Application 先完成路径规划，并提交运行时执行计划：

- 上传 / 下载执行计划包含目标 profile 对应的 `ConnectionRoute`。
- 远端复制执行计划包含 source profile 和 target profile 各自的 `ConnectionRoute`。
- 执行计划可以随任务提交、重试或恢复时重新生成，不作为长期业务事实替代任务描述。
- Transfer worker 只消费执行计划，不调用 `IConnectionRoutePlanner`，不读取 network inventory。

## 2. 远端到远端传输

远端到远端传输第一版只执行一种模式：

- `LocalRelay`：源 SFTP 流经本机，再写入目标 SFTP。

`RemoteCommand`、`scp` 和 `rsync` 编排只作为模型预留和后续增强。任务必须记录模式，不能由 worker 隐式猜测。

## 3. 状态

第一版状态：

- Pending
- Running
- Succeeded
- Failed
- Cancelled
- Interrupted

`Interrupted` 表示应用关闭、进程退出或运行时中断，不等同于普通失败。

## 4. 队列和历史

队列和历史都通过不可变快照查询。UI 不直接访问 Transfer 内部队列。
