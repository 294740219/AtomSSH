# AtomSSH 连接路径规划

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的连接路径规划和跳板机边界
>
> 变更规则：调整连接路径语义时必须同步更新本文档。

## 1. 路径类型

第一版路径类型：

- Direct：直接连接 host:port。
- JumpHost：通过 SSH 跳板机连接。
- ProxyJumpChain：多个跳板机串联。

## 2. 跳板机

跳板机是可保存 profile 的一种角色，不是普通备注字段。

跳板机配置必须能表达：

- 跳板机 profile。
- 目标 host 和 port。
- 认证方式。
- 是否允许 SFTP 和端口转发复用该路径。
- 失败诊断：跳板机不可达、目标不可达、认证失败、端口拒绝。

## 3. 路径规划职责

`IConnectionRoutePlanner` 输入：

- 目标 profile。
- 当前网络空间。
- 可用 jump host 和代理链。
- 用户选择或默认策略。

输出：

- `ConnectionRoute`。
- 诊断信息。
- 风险提示。

Session Runtime 只消费规划结果，不自行查找网络清单或跳板机。
