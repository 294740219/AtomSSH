# AtomSSH.Transfer Runtime 设计

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的传输运行时、worker 和调度边界
>
> 变更规则：调整传输运行时生命周期时必须同步更新本文档。

## 1. 调度器

`ITransferTaskScheduler` 负责提交、取消、重试任务。提交执行时必须接收任务描述和 Application 生成的运行时执行计划，不能在 scheduler 或 worker 内部规划连接路径。

第一版只需要单 worker 或有限并发，不做复杂限速和断点续传。

## 2. Worker

Worker 是短生命周期执行对象：

- 读取任务描述。
- 解析 profile 和凭据。
- 创建 SFTP channel。
- 执行上传或下载。
- 更新进度和最终状态。
- 释放通道和凭据 lease。

Worker 不应注册为全局 Singleton。

远端到远端复制第一版只实现 `LocalRelay` worker：

- 为源 profile 创建读取用 SFTP channel。
- 为目标 profile 创建写入用 SFTP channel。
- 数据经本机内存流或受控临时文件中转。
- 进度统一写入 `ITransferStateStore`。
- 任务结束、失败或取消时释放两个 SFTP channel、凭据 lease 和临时文件。

第一版不得实现 `RemoteCommand`、scp 或 rsync 执行路径，避免 Transfer 模块同时承担远端命令编排、shell 语义和跨主机策略路由职责。

## 3. 进度

进度快照至少包含：

- 已传输字节数。
- 总字节数。
- 当前速度。
- 状态。
- 最近错误摘要。

进度不得包含 secret material 或原始 exception。
