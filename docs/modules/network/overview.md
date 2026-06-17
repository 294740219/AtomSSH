# AtomSSH.Network 模块设计

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的 Network 模块边界、目录规划、云 VPC 内网场景和连接路径规划约束
>
> 变更规则：实现阶段不得随意修改本文件；如需调整 Network 边界，必须同步更新 architecture 和 modules 文档。

## 1. 模块定位

`AtomSSH.Network` 是 SSH 运维场景下的连接路径规划和网络诊断实现模块。它解决的问题不是“如何执行 SSH 协议”，也不是“如何管理云网络”，而是基于 Core 中的网络模型判断“目标机器经由什么入口可达、应该用哪条 SSH 路径连接”。

该模块覆盖以下运维场景：

- 云 VPC 内网地址和网段。
- 跳板机、堡垒机、ProxyJump、代理链。
- 从一台远程主机向另一台远程主机传递文件时的路径规划。

AtomSSH 不替代云网络管理平台，不负责 VPC、路由表、安全组或 VPN 控制面。产品层可以维护 SSH 运维所需的网络清单、可达性信息和 SSH 入口，让用户可以更快打开终端、SFTP 和远端复制任务；但在工程实现上，`AtomSSH.Network` 只消费这些快照并输出路径规划或诊断结果。

从工程边界看，`AtomSSH.Network` 只提供 route planner 和 diagnostics 实现；Network Inventory 是产品和 UI 概念，稳定模型定义在 Core，持久化由 Infrastructure 实现，用例编排由 Application 的 `NetworkInventoryAppService` 实现。Desktop 不能绕过 Application 直接调用 Network 实现。

## 2. 边界规则

Network 允许包含：

- 连接路径规划实现，例如直连、ProxyJump、跳板机链路选择。
- 网络诊断实现，例如节点地址冲突、目标不可达、路由缺失、目标 SSH 端口不可达。
- 规划和诊断所需的内部策略对象。

Network 禁止包含：

- Avalonia、AtomUI、ViewModel、弹窗。
- SSH client、SFTP client、terminal channel、Transfer worker。
- Application 用例服务。
- Core 稳定模型定义。
- Network inventory 仓储实现。
- 本地持久化文件格式和凭据加密实现。
- 独立云网络管理平台能力。
- 修改云厂商 VPC、路由表、安全组或 VPN 配置。
- 保存凭据明文。

## 3. 推荐目录

```text
src/AtomSSH.Network/
  Routes/
  JumpHosts/
  Diagnostics/
  DependencyInjection/
```

## 4. 消费的 Core 对象和端口

Network 消费以下 Core 网络模型：

- `NetworkSpace`
- `NetworkNode`
- `NetworkNodeId`
- `NetworkAddress`
- `SubnetRoute`
- `ConnectionRoute`
- `ConnectionRouteKind`
- `JumpHostRoute`
- `ProxyJumpChain`
- `NetworkReachabilitySnapshot`
- `NetworkDiagnosticResult`
- `RoutePlanningError`

Network 实现以下 Core 端口：

- `IConnectionRoutePlanner`
- `INetworkDiagnosticsService`

Network 不实现 `INetworkInventoryStore`。该 store 的稳定端口定义在 Core，持久化实现属于 Infrastructure。

## 5. 设计约束

- Network 只输出“应该如何通过 SSH 连接”的路径描述，不创建 SSH 会话。
- Session 根据 `ConnectionRoute` 创建真实连接。
- Application 编排用户用例，例如“打开某个 VPC 内网节点的终端”。
- Infrastructure 保存本地网络清单和路径配置；Network 只消费这些快照进行规划和诊断。
- Desktop 展示节点、网段、路由状态和诊断结果。
- Network 第一版优先手工录入和导入，不默认修改任何外部网络控制面。
