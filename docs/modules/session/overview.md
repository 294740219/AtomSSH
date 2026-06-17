# AtomSSH.Session 模块设计

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的 Session 模块边界、运行时职责和实现约束
>
> 变更规则：实现阶段不得随意修改本文件；如需调整 Session 边界，必须同步更新 architecture 和 modules 文档。

## 1. 模块定位

`AtomSSH.Session` 是 SSH 运行时模块，负责真实 SSH 连接、认证、Host Key 校验、PTY、terminal channel、SFTP channel 和端口转发运行实例。

Session 不负责用户用例编排，也不负责 UI 展示。

## 2. 边界规则

Session 允许包含：

- SSH client wrapper。
- 认证流程适配。
- Host key verification workflow。
- PTY / terminal channel 管理。
- SFTP channel 创建和短操作。
- port forwarding runtime。
- 消费 Core 的 `ConnectionRoute` 连接路径描述。
- 协议库异常到 Core 错误的转换。

Session 禁止包含：

- Avalonia、AtomUI、ViewModel。
- Application 用例服务。
- 本地配置文件格式。
- UI 弹窗和消息。
- 长期保存凭据明文。

## 3. 推荐目录

```text
src/AtomSSH.Session/
  Connections/
  Authentication/
  HostKeys/
  Terminal/
  Sftp/
  PortForwarding/
  Errors/
  DependencyInjection/
```

## 4. 设计约束

- Session 可以依赖具体 SSH 协议库，但不能让协议库类型进入 Core 或 Application。
- Session 可以消费 `ConnectionRoute`，但不能依赖 `AtomSSH.Network` 项目或自行读取网络清单。
- 每个活动连接必须可关闭、可查询状态、可映射错误。
- Host key 变化必须阻断连接并返回信任决策需求。
- Terminal channel 的输入输出必须通过抽象边界进入 UI。
- Port forward listener 必须明确绑定到活动 SSH 会话或短生命周期连接。
