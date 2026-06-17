# AtomSSH.Transfer 模块设计

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的 Transfer 模块边界、目录规划和实现约束
>
> 变更规则：实现阶段不得随意修改本文件；如需调整 Transfer 边界，必须同步更新 architecture 和 modules 文档。

## 1. 模块定位

`AtomSSH.Transfer` 管理 SFTP 上传下载任务生命周期，包括任务队列、执行 worker、进度、取消、失败、重试和历史。它还负责远端到远端文件传递任务的执行编排。

Transfer 不负责 UI 展示，不负责保存 profile，也不持有长期 SSH client。

## 2. 边界规则

Transfer 允许包含：

- 传输任务调度器。
- Worker 和执行批次。
- 进度聚合。
- 状态更新。
- 失败、取消、重试策略。
- 远端到远端传输执行模式；第一版只实现本机中转 `LocalRelay`。

Transfer 禁止包含：

- Avalonia、AtomUI、ViewModel。
- Application 用例服务。
- 具体 UI 列表状态。
- 凭据明文。
- 把 SFTP client 持久化到任务对象。

## 3. 推荐目录

```text
src/AtomSSH.Transfer/
  Queue/
  Workers/
  Scheduling/
  Progress/
  Policies/
  DependencyInjection/
```

## 4. 设计约束

- Transfer 只依赖 Core。
- Transfer 执行任务时通过 Core 端口创建运行时 SFTP 能力。
- Transfer 不调用 `IConnectionRoutePlanner`，不读取 network inventory。连接路径必须由 Application 在提交、重试或恢复任务前规划好，并作为运行时执行计划传入 Transfer。
- 远端到远端任务必须保存源 profile、目标 profile、源路径、目标路径和传递模式。
- 本机中转模式不得把完整文件不必要地落盘；如需临时文件，必须有清理和失败恢复规则。
- `RemoteCommand`、`scp` 和 `rsync` 编排只允许作为模型预留，不得在第一版 Transfer Runtime 中实现执行路径。
- Transfer 只能消费已经规划好的 `ConnectionRoute` 描述，不能依赖 `AtomSSH.Network` 项目，不能把路径规划作为 worker 的隐式副作用。
- UI 只能通过 Application 查询传输快照。
- 运行中任务关闭时必须落为 `Interrupted` 或等价状态。
