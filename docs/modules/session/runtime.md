# AtomSSH.Session Runtime 设计

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的 SSH 运行时实例、连接和关闭边界
>
> 变更规则：调整 SSH runtime 生命周期时必须同步更新本文档和 lifecycle 文档。

## 1. 活动会话

活动会话由 `SshSessionInstanceId` 标识。它代表一个运行中的 SSH 连接上下文，可以拥有：

- 一个主 terminal channel。
- 零个或多个 SFTP channel。
- 零个或多个 port forwarding instance。

活动会话不是持久化对象。

## 2. 连接流程

连接流程建议拆成：

1. 接收 Application 传入的 profile 摘要和已规划 `ConnectionRoute`。
2. 解析凭据。
3. 按 `ConnectionRoute` 建立 TCP / proxy / jump host 链路。
4. 校验 host key。
5. 执行认证。
6. 创建 PTY 和 terminal channel。
7. 返回会话状态快照。

每一步失败都必须映射为结构化错误。

Session Runtime 不读取网络清单，不调用 `IConnectionRoutePlanner`，不自行选择跳板机或代理链。

## 3. 关闭流程

关闭流程必须按顺序处理：

1. 停止接收新的 channel / port forward 请求。
2. 停止 port forward listener。
3. 关闭 SFTP channel。
4. 关闭 terminal channel。
5. 断开 SSH connection。
6. 释放凭据 lease。
7. 发布最终状态快照。

## 4. 重连

第一版重连可以实现为关闭旧会话后创建新会话。不得尝试复用已失败的协议库对象。
