# AtomSSH 模块文档索引

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的模块边界、依赖关系、目录规划或实现约束
>
> 变更规则：实现阶段不得随意修改本文件；如需调整模块边界，必须同步更新对应模块文档。

本文件是 AtomSSH 模块文档导航页，也是模块边界速查表。修改代码前必须先判断改动属于哪个模块。

## 1. 模块总览

| 模块 | 文档 | 物理项目 | 核心职责 |
| --- | --- | --- | --- |
| Core | `core/overview.md` | `AtomSSH.Core` | 产品内核，定义模型、值对象、端口、错误和能力。 |
| Application | `application/overview.md` | `AtomSSH.Application` | 用户用例编排。 |
| Network | `network/overview.md` | `AtomSSH.Network` | 基于 Core 网络模型提供连接路径规划和诊断实现。 |
| Session | `session/overview.md` | `AtomSSH.Session` | SSH 连接、认证、PTY、channel、端口转发运行时。 |
| Transfer | `transfer/overview.md` | `AtomSSH.Transfer` | SFTP 传输队列、任务、进度和历史。 |
| Infrastructure | `infrastructure/overview.md` | `AtomSSH.Infrastructure` | 配置、凭据、known hosts、日志、本地持久化。 |
| Presentation | `presentation/overview.md` | `AtomSSH.Desktop` | Avalonia / AtomUI UI、ViewModel、导航、弹窗和组合根。 |

## 2. 依赖摘要

```text
AtomSSH.Desktop
  -> AtomSSH.Application
  -> AtomSSH.Network
  -> AtomSSH.Session
  -> AtomSSH.Transfer
  -> AtomSSH.Infrastructure
  -> AtomSSH.Core

AtomSSH.Application -> AtomSSH.Core
AtomSSH.Network -> AtomSSH.Core
AtomSSH.Session -> AtomSSH.Core
AtomSSH.Transfer -> AtomSSH.Core
AtomSSH.Infrastructure -> AtomSSH.Core
```

## 3. AI 修改代码强约束

- 不得在 Core 中加入 Avalonia、AtomUI、SSH 协议库、终端控件、日志、配置读写或文件 IO。
- 不得在 ViewModel 中直接调用 Network planner、Session Runtime、Transfer Runtime、Infrastructure store、Core 底层端口或具体 SSH client。
- 不得让 SSH 协议库对象、SFTP DTO、原始 exception 穿透到 Core / Application / Presentation。
- 不得把凭据明文写入普通配置、日志、错误详情或测试输出。
- 不得在 Application 或 Transfer 中定义跨模块稳定端口；优先放入 Core。
- 不得让 Network 直接创建 SSH 会话、SFTP channel 或 Transfer worker。
- 不得让 Transfer 依赖 Application。
- 不得让 Session 依赖 Application 或 Desktop。
- 不得把所有服务注册成 Singleton。

## 4. 常见任务路由

| 任务 | 应修改模块 |
| --- | --- |
| 新增会话、主机、路径、错误、能力、凭据引用模型 | Core |
| 新增保存会话、打开终端、连接测试、SFTP 浏览、创建转发等用户流程 | Application |
| 新增 VPC 网段、内网目标、跳板机、代理链等稳定模型 | Core |
| 新增连接路径规划、网络可达性诊断规则 | Network |
| 新增 SSH 连接、认证、PTY、channel、host key、port forward runtime | Session |
| 新增 SFTP 上传下载、远端到远端中转、队列、进度、取消、重试、历史 | Transfer |
| 新增配置、凭据加密、known hosts、日志、导入导出 | Infrastructure |
| 新增窗口、页面、ViewModel、图标、样式、弹窗、导航 | Presentation |
| 调整依赖关系或项目拆分 | Architecture 文档和对应模块文档 |

## 5. 文档维护规则

- 新增模块时，必须新增对应模块文档，并更新本文件。
- 调整模块职责时，必须同步更新本文件和对应模块文档。
- 新增重要业务对象时，先判断是否属于 Core。
- 新增 UI 页面时，先更新 `docs/ui/` 对应文档，再进入实现。
- 文档和代码不一致时，先修正设计，再改代码。
