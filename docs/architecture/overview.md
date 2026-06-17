# AtomSSH 架构设计文档

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的架构、模块边界、依赖关系、目录规划或实现约束
>
> 变更规则：实现阶段不得随意修改本文件；如需调整本文档定义的设计边界，必须先说明原因，再同步更新相关设计文档。

本目录维护 AtomSSH 软件的正式架构、工程规范和模块文档。

## 1. 产品说明

AtomSSH 是一款跨平台 SSH 终端管理应用，用途如下：

- 管理 SSH 服务器连接配置。
- 打开多标签终端会话。
- 通过 SFTP 浏览、上传、下载远程文件。
- 管理本地、远程和动态 SOCKS 端口转发。
- 管理跳板机、ProxyJump 等 SSH 连接路径。
- 把云内网入口、跳板机和内网目标转化为可连接的 SSH 目标。
- 在云 VPC 内多台远程主机之间通过 SSH/SFTP 编排文件传递。
- 保存会话分组、凭据引用、known hosts、终端主题和连接偏好。

## 2. 核心设计原则

AtomSSH 遵循以下原则：

- dotnet 标准库优先：底层能力优先使用现代 .NET 库和清晰端口。
- AtomUI 优先：UI 层优先使用 AtomUI 提供的控件和视觉能力。
- AOT 友好：默认尽量避免运行时反射扫描和动态发现。
- 安全优先：凭据、私钥、passphrase、host key 决策、日志脱敏是第一等设计对象。
- 会话短生命周期优先：SSH client、SFTP client、channel、PTY、port forward listener 不能被错误注册成全局单例。
- SSH 主线优先：Network 和 VPC 能力只能作为 SSH 连接和 SFTP 传输的增强入口，不能反客为主。
- 网络拓扑显式建模：跳板机、代理链和 VPC 内网不是普通字符串备注，必须用明确模型表达可达性。

## 3. 禁止行为列表

- 不使用 ReactiveUI。
- 不把 IObservable 作为状态、命令、路由和事件系统的主公共 API。
- 不把运行时反射扫描作为默认模块发现机制。
- 不把 SSH 协议库对象、终端控件对象、SDK exception 泄漏到 Core 或 Application。
- 不把凭据明文保存到普通配置文件。
- 不在 ViewModel 中直接创建 SSH client、SFTP client、端口转发 listener 或文件仓储。
- 不在 Core 中放 UI、配置读写、日志、网络 IO 或具体协议库。

## 4. 执行流程

从运行流程看，AtomSSH 的整体执行结构分为以下层次：

| 顺序 | 层 / 模块 | 职责 |
| --- | --- | --- |
| 0 | Core | 定义会话、主机、凭据引用、终端配置、SFTP、端口转发、错误和值对象等产品内核。 |
| 1 | Presentation / Desktop UI | 基于 Avalonia 和 AtomUI 展示界面，接收用户操作，维护 UI 状态。 |
| 2 | Application | 编排用户用例，例如保存会话、连接测试、打开终端、创建 SFTP 传输任务。 |
| 3 | Network Routing | 基于 VPC 网段、内网目标、跳板机和代理链规划 SSH 连接路径，并提供可达性诊断。 |
| 4 | Session Runtime | 管理 SSH 连接、PTY、terminal channel、SFTP channel、端口转发运行实例和会话状态。 |
| 5 | Transfer Engine | 管理 SFTP 上传下载、远端到远端中转任务、队列、进度、取消、失败和历史。 |
| 6 | Infrastructure | 提供配置、凭据、known hosts、日志、本地持久化、导入导出等技术实现。 |
| 7 | External Systems | SSH 服务器、SFTP 子系统、代理服务器、本地文件系统、系统凭据库。 |

典型终端打开流程：

```text
TerminalWorkspaceViewModel
  -> SessionAppService
    -> IConnectionRoutePlanner
      -> ConnectionRoute
    -> ISshSessionRuntime
      -> ICredentialResolver
      -> IHostKeyTrustStore
      -> SSH protocol library
        -> Remote SSH server
```

典型 SFTP 下载流程：

```text
SftpBrowserViewModel
  -> TransferAppService
    -> ITransferTaskScheduler
      -> Transfer Runtime
        -> ISshSessionFactory
        -> controlled SFTP operation boundary
          -> Remote SSH server
```

典型云内网目标连接流程：

```text
NetworkInventoryViewModel
  -> NetworkInventoryAppService
    -> INetworkInventoryStore
    -> IConnectionRoutePlanner
      -> Direct / JumpHost / ProxyJump route

TerminalWorkspaceViewModel
  -> SessionAppService
    -> ISshSessionRuntime
      -> consume previously planned ConnectionRoute
      -> Remote SSH server
```

典型远端到远端文件传递流程：

```text
RemoteCopyViewModel
  -> TransferAppService
    -> IConnectionRoutePlanner
      -> source/target ConnectionRoute
    -> TransferExecutionPlan
    -> ITransferTaskScheduler
      -> Transfer Runtime
        -> Source SFTP session
        -> Target SFTP session
```

会话配置、运行时连接和传输任务必须分开理解：

- Application 保存的是会话配置和任务描述。
- Application 负责在打开终端、SFTP、端口转发或传输任务前完成必要的连接路径规划。
- Session Runtime 创建和持有短生命周期 SSH 连接与 channel。
- Transfer Engine 执行任务时消费 Application 生成的 `TransferExecutionPlan`，不自行规划连接路径，不把 SSH client 放进任务对象。
- Network Routing 只表达节点、网段和 SSH 连接路径，不替代云网络管理平台。
- `RemoteCopyTask` 保存源 profile、目标 profile、源路径、目标路径和复制模式，不保存两个 SFTP client。
- Desktop 只展示状态，不持有协议库对象。

## 5. 依赖关系

代码依赖必须围绕 Core 向内收敛，不能让 Core 反向依赖 UI、Session、Infrastructure 等外层实现。

| 模块 | 可以依赖 | 不应该依赖 | 依赖原则 |
| --- | --- | --- | --- |
| AtomSSH.Core | .NET BCL | Avalonia、AtomUI、SSH 协议库、终端控件、日志、数据库、配置实现 | Core 只定义产品语言、模型、端口、错误和纯领域规则。 |
| AtomSSH.Application | AtomSSH.Core、DI 抽象 | Avalonia、AtomUI、具体 SSH 库、ViewModel、Infrastructure 实现 | Application 编排用户用例，只消费 Core 端口。 |
| AtomSSH.Network | AtomSSH.Core、DI 抽象 | Avalonia、AtomUI、Application、Session Runtime、Transfer Runtime、Infrastructure 存储实现 | Network 实现 Core 中定义的连接路径规划和网络诊断端口，不拥有网络清单持久化，不创建 SSH 会话，不管理云网络控制面。 |
| AtomSSH.Session | AtomSSH.Core、SSH 协议库、DI 抽象 | Avalonia、AtomUI、Application、ViewModel | Session 实现 SSH 运行时和通道管理，不依赖 UI 或 Application。 |
| AtomSSH.Transfer | AtomSSH.Core、DI 抽象 | Avalonia、AtomUI、Application、具体 ViewModel | Transfer 管理 SFTP 任务调度，通过 Core 端口获取运行时能力。 |
| AtomSSH.Infrastructure | AtomSSH.Core、必要系统库、加密库、日志库 | Avalonia、AtomUI、Application、Session UI 状态 | Infrastructure 提供本地技术实现，不承载用户用例。 |
| AtomSSH.Desktop | 所有生产模块 | 不应被任何内层模块反向依赖 | Desktop 是 Avalonia/AtomUI 启动项目和唯一生产组合根。 |

推荐项目引用方向：

```text
AtomSSH.Desktop
  -> AtomSSH.Application
  -> AtomSSH.Network
  -> AtomSSH.Session
  -> AtomSSH.Transfer
  -> AtomSSH.Infrastructure
  -> AtomSSH.Core

AtomSSH.Application
  -> AtomSSH.Core

AtomSSH.Network
  -> AtomSSH.Core

AtomSSH.Session
  -> AtomSSH.Core

AtomSSH.Transfer
  -> AtomSSH.Core

AtomSSH.Infrastructure
  -> AtomSSH.Core
```

强约束：

- Core 不依赖任何外层模块。
- Application 不依赖 Avalonia、AtomUI 或具体 SSH 协议库。
- Network 不依赖 Application、Session、Transfer 或 Desktop；它只提供连接路径规划和网络诊断能力。
- Session 不依赖 Application，不依赖 Desktop，不接触 ViewModel。
- Transfer 不依赖 Application，不直接暴露内部队列给 UI。
- Infrastructure 不承载业务用例，不弹 UI。
- Desktop 可以引用所有实现模块，但只作为组合根和 UI 层。
- 跨模块稳定端口统一定义在 Core。
- ViewModel 不能绕过 Application 直接进入 Network、Session、Transfer、Infrastructure 或任何 Core 底层端口。

## 6. 项目目录规划

```text
AtomSSH/
  AtomSSH.slnx
  Directory.Build.props
  Directory.Packages.props

  docs/

  src/
    AtomSSH.Core/
    AtomSSH.Application/
    AtomSSH.Network/
    AtomSSH.Session/
    AtomSSH.Transfer/
    AtomSSH.Infrastructure/
    AtomSSH.Desktop/

  tests/
    AtomSSH.Core.Tests/
    AtomSSH.Application.Tests/
    AtomSSH.Network.Tests/
    AtomSSH.Session.Tests/
    AtomSSH.Transfer.Tests/
    AtomSSH.Infrastructure.Tests/
```

各项目职责：

| 项目 | 职责 |
| --- | --- |
| AtomSSH.Core | 产品内核，定义模型、值对象、端口、错误、能力和领域规则。 |
| AtomSSH.Application | 用户用例编排层，负责会话管理、连接测试、终端打开、SFTP 浏览、传输创建、端口转发配置。 |
| AtomSSH.Network | 网络路径实现层，负责基于 Core 网络模型执行连接路径规划和诊断。 |
| AtomSSH.Session | SSH 运行时，负责连接、认证、PTY、channel、host key、port forward runtime。 |
| AtomSSH.Transfer | SFTP 传输任务调度，负责队列、状态、进度、取消和历史。 |
| AtomSSH.Infrastructure | 配置、凭据、known hosts、日志、本地持久化和导入导出。 |
| AtomSSH.Desktop | Avalonia / AtomUI 启动项目、UI、ViewModel、导航、弹窗和组合根。 |

## 7. 生命周期文档

对象和服务“应该活多久”统一阅读 `docs/architecture/lifecycle.md`。涉及 SSH 连接、SFTP client、端口转发 listener、凭据 lease、known hosts、后台 worker、DI 注册和关闭保存时，必须同时遵守生命周期文档。

## 8. 技术选型文档

第三方库和官方库使用规则统一阅读 `docs/architecture/technology-selection.md`。实现阶段新增生产依赖、替换 SSH 库、选择终端控件或调整凭据安全存储方案时，必须先遵守该文档。
