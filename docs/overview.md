# AtomSSH 文档总览

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的产品定位、文档结构、阅读顺序和实现前置规则
>
> 变更规则：实现阶段不得绕过本文档定义的架构阅读顺序；如需调整文档体系，必须同步更新本文件。

AtomSSH 是一款基于 Avalonia 和 AtomUI 的 SSH 终端管理应用。本文档目录用于在正式编码前冻结软件架构、模块边界、依赖方向、生命周期和第一版 UI 结构。

AtomSSH 的设计参考成熟 SSH 管理软件的常见能力：

- MobaXterm：会话管理、标签终端、图形 SFTP、SSH gateway、SSH tunnels、多服务器执行、宏、密码管理。
- SecureCRT：SSH2 认证、凭据管理、端口转发、动态 SOCKS、Host Key 管理、终端仿真、滚屏、日志、命令窗口、批量会话编辑、导入导出、标签会话。

AtomSSH 的产品主线是 SSH 管理工具。它必须优先具备成熟 SSH 客户端的核心能力：会话管理、终端、SFTP、跳板机、端口转发、凭据与安全、日志、导入导出、批量管理等。所有特色能力都必须服务于 SSH 运维效率，不能把产品方向带成网络控制面、云管平台或 VPN 客户端。

在这个前提下，AtomSSH 需要加入少量有辨识度的运维增强能力：云 VPC 内网、跳板机、连接路径规划和多主机文件中转。AtomSSH 不做云网络控制台；它只把这些网络入口转化为更方便的 SSH 连接、SFTP 传输和路径诊断能力。

## 1. 文档结构

```text
docs/
  context.md
  overview.md
  product/
    features.md
  architecture/
    overview.md
    lifecycle.md
    technology-selection.md
  modules/
    overview.md
    core/
    application/
    network/
    session/
    transfer/
    infrastructure/
    presentation/
  ui/
    home.md
    menu.md
    session-dialog.md
    session-management.md
    terminal-workspace.md
    sftp-browser.md
    port-forwarding.md
    network-inventory.md
    remote-copy.md
    command-snippets.md
    settings.md
    security.md
    import-export.md
    transfer-queue.md
    transfer-history.md
  implementation/
    roadmap.md
```

## 2. 推荐阅读顺序

实现任何代码前，按以下顺序读取文档：

1. `docs/implementation/roadmap.md`
2. `docs/product/features.md`
3. `docs/architecture/overview.md`
4. `docs/architecture/lifecycle.md`
5. `docs/architecture/technology-selection.md`
6. `docs/modules/overview.md`
7. 目标模块下的具体设计文档
8. 如果涉及桌面界面，再读取 `docs/ui/` 下对应 UI 文档

## 3. 第一版产品边界

第一版必须覆盖：

- SSH 会话配置、分组、连接测试和打开终端。
- 密码、私钥、键盘交互等基础认证建模。
- Host Key 首次信任、变更拦截和 known hosts 存储边界。
- 多标签终端、断开、关闭、重连、基础终端主题和滚屏设置。
- SFTP 远程浏览、上传、下载、删除、刷新。
- 本地端口转发、远程端口转发、动态 SOCKS 转发的配置模型和基础运行实例。
- 跳板机 / ProxyJump 建模，支持通过堡垒机或内网入口连接目标机器。
- 云 VPC 内两台远程机器之间通过点选完成文件传递的用例建模。
- 凭据加密存储、日志脱敏、错误详情脱敏。

第一版明确延后：

- RDP、VNC、Telnet、Serial 等非 SSH 协议。
- 内置 X server 和完整 X11 桌面体验。
- 完整脚本引擎、宏录制回放、多会话广播命令。
- 云同步账号库、团队策略下发、插件市场。
- 把网络清单做成独立云网络管理平台。
- 偏离 SSH/SFTP 场景，做成通用文件分发平台。
- 高级终端仿真兼容矩阵和企业合规策略。

## 4. 实现前置规则

- 先写设计文档，再写工程骨架。
- 先建立 Core 契约，再接 SSH 协议库或终端控件。
- 有 dotnet 官方标准库时优先使用官方标准库；官方库缺失关键能力时，再通过模块边界引入社区库。
- 先打通 fake session / fake SFTP 链路，再连接真实 SSH。
- ViewModel 只能调用 Application 用例，不能直接使用 Network planner、Session Runtime、Transfer Runtime、Core 底层端口、SSH client、SFTP client、文件存储或凭据存储。
- Core 不能依赖 Avalonia、AtomUI、SSH.NET、Terminal 控件、日志库、文件系统或配置实现。
- 凭据、私钥内容、passphrase、连接密码不得进入普通配置、日志、错误详情或测试输出。
