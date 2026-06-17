# AtomSSH 实现路线图

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的工程实现顺序、阶段边界、禁止跳步规则和阶段完成条件
>
> 变更规则：实现阶段不得随意调整本文档定义的实现顺序；如需调整，必须先说明原因，再同步更新相关 architecture、modules、ui 文档。

本文档定义 AtomSSH 从设计文档进入代码实现时的落地顺序。

## 1. 总原则

- 先工程骨架，后业务功能。
- 先 Core 契约，后真实 SSH 协议库。
- 先组合根和 DI 边界，后具体服务实现。
- 先 Desktop 空 Shell，后复杂终端工作区。
- 先 fake session / fake SFTP，后真实连接。
- 先可编译、可启动、依赖方向正确，再追求功能完整。
- 每次实现具体功能前，必须读取 `docs/product/features.md`，确认该功能的产品边界、MVP 范围和产品模块到工程模块的映射。

## 2. Phase 0：工程骨架

目标：建立可编译的解决方案结构和项目依赖方向。

允许做：

- 创建 `AtomSSH.slnx`。
- 创建 `src/` 和 `tests/`。
- 创建 7 个生产项目：Core、Application、Network、Session、Transfer、Infrastructure、Desktop。
- 创建测试项目。
- 建立项目引用关系。

禁止做：

- 写真实 SSH 连接。
- 写复杂终端 UI。
- 保存真实凭据。

完成条件：

- 解决方案可以 restore / build。
- 项目引用方向符合架构文档。

## 3. Phase 1：Core 最小契约

目标：落地跨模块稳定模型、值对象、错误和端口。

允许做：

- 定义 `OperationResult<T>`。
- 定义错误模型。
- 定义 SSH profile、凭据引用、host key、terminal profile、SFTP item、transfer task、remote copy task、transfer execution plan、port forward profile、network route、command snippet。
- 定义仓储、凭据、Session、Network、Transfer、Settings、ImportExport 端口。

禁止做：

- 在 Core 中引用 Avalonia、AtomUI、SSH 协议库、终端控件、日志、文件 IO。

完成条件：

- Core 可以独立 build。
- 后续模块可以只通过 Core 契约协作。

## 4. Phase 2：DI 与组合根边界

目标：建立各模块注册扩展和 Desktop 组合根骨架。

允许做：

- Application 暴露 `AddAtomSSHApplication()`。
- Network 暴露 `AddAtomSSHNetwork()`。
- Session 暴露 `AddAtomSSHSession()`。
- Transfer 暴露 `AddAtomSSHTransfer()`。
- Infrastructure 暴露 `AddAtomSSHInfrastructure()`。
- Desktop 创建组合根代码。

禁止做：

- 在内层模块调用 `BuildServiceProvider()`。
- 把 SSH client、SFTP client、terminal channel、port forward listener 注册成全局 Singleton。

完成条件：

- Desktop 是唯一生产组合根。
- DI 生命周期符合 lifecycle 文档。

## 5. Phase 3：Desktop 空 Shell

目标：建立 Avalonia / AtomUI 可启动桌面壳。

允许做：

- 创建 Avalonia 启动文件。
- 接入 AtomUI 基础资源。
- 创建 Shell、TopBar、Navigation、Content Area、StatusBar。
- 创建空页面占位。

禁止做：

- ViewModel 直接访问 Network planner、Session Runtime、Transfer Runtime、Infrastructure store、Core 底层端口或具体 SSH client。
- 直接写真实 SSH 终端。

完成条件：

- Desktop 可以启动空 Shell。
- 页面切换路径可用。

## 6. Phase 4：Infrastructure 最小本地实现

目标：实现最小配置、profile、凭据、known hosts 和传输状态存储。

允许做：

- 配置目录解析。
- settings repository。
- profile repository。
- credential store 骨架。
- known hosts store。
- transfer task/state store。
- 日志初始化和脱敏骨架。

禁止做：

- 明文保存 secret。
- Infrastructure 弹 UI。
- Infrastructure 调用 Application 用例。

完成条件：

- Infrastructure 只依赖 Core。
- 启动失败返回结构化错误。

## 7. Phase 5：Application 用例骨架

目标：建立用户用例编排服务。

允许做：

- profile 管理用例。
- session 打开 / 关闭 / 连接测试用例。
- network inventory / route planning / diagnostics 用例。
- SFTP 浏览用例。
- transfer 创建和查询用例。
- transfer 提交、重试和恢复前生成 `TransferExecutionPlan`。
- port forwarding 管理用例。
- command snippets 用例。
- settings 用例。
- import/export 用例。

禁止做：

- Application 直接引用 Avalonia、AtomUI 或具体 SSH 协议库。
- Application 持有长期 SSH client。

完成条件：

- Presentation 可以通过 Application 服务调用用例。
- 用例结果可展示且脱敏。

## 8. Phase 6：Fake Session 与 Fake SFTP 链路

目标：先打通抽象链路，再接真实 SSH。

允许做：

- 实现 fake session runtime。
- 实现 fake route planner 和 network diagnostics。
- 实现 fake terminal output。
- 实现 fake SFTP browser。
- 实现 fake transfer worker。

禁止做：

- 一上来接真实服务器。
- 让 fake 实现污染 Core 模型。

完成条件：

- Desktop 可以打开模拟终端标签。
- SFTP 页面可以展示模拟文件。
- Transfer 队列可以显示模拟任务状态。
- Network 清单可以展示模拟内网节点和跳板路径诊断。

## 9. Phase 7：真实 SSH 终端最小闭环

目标：接入真实 SSH 协议库，打通连接、认证、host key 和 PTY。

允许做：

- 引入 SSH 协议库。
- 实现密码和私钥认证。
- 实现 host key 首次信任和变更拦截。
- 实现 terminal channel 输入输出。
- 映射常见协议异常。

禁止做：

- 支持所有高级认证。
- 写完整脚本引擎。
- 实现多会话广播命令。

完成条件：

- 使用真实 SSH 服务器可以打开终端。
- 断开、关闭、重连有可解释状态。
- 错误详情不泄漏 secret。

## 10. Phase 8：SFTP 与传输最小闭环

目标：打通真实 SFTP 浏览和上传下载任务。

允许做：

- 列目录。
- 上传小文件。
- 下载小文件。
- 删除和刷新。
- 传输进度和历史。

禁止做：

- 断点续传。
- 复杂并发。
- 文件夹递归传输。

完成条件：

- 真实服务器 SFTP 浏览可用。
- 上传下载小文件可完成。
- 队列和历史通过 Application 查询。

## 11. Phase 9：端口转发与跳板机最小闭环

目标：实现本地、远程、动态 SOCKS 转发，以及 SSH 跳板机 / ProxyJump 的基础配置和运行。

允许做：

- 保存转发配置。
- 启动和停止转发。
- 保存跳板机 profile。
- 通过跳板机连接目标 profile。
- 展示端口占用、远端拒绝等错误。

禁止做：

- 复杂流量统计。
- 企业策略管理。

完成条件：

- 本地转发和动态 SOCKS 至少各有一条手工验证路径。
- 通过跳板机连接 VPC 内网主机有一条手工验证路径。
- 停止和应用关闭能释放 listener。

## 12. Phase 10：VPC 网络清单与远端复制

目标：在 SSH 管理工具主线不变的前提下，引入 VPC 内网目标、跳板机和连接路径清单，并支持云内网远端到远端文件传递。

允许做：

- 手工录入 VPC 机器、跳板机和网络空间。
- 导入内网机器清单。
- 规划 Direct、Jump Host、ProxyJump 连接路径。
- 创建远端到远端复制任务。
- 实现 LocalRelay 模式。
- 为 RemoteCommand 模式只保留模型预留，不实现执行路径。

禁止做：

- 把 AtomSSH 做成 VPN 客户端。
- 把 AtomSSH 做成云网络管理平台。
- 修改云厂商 VPC、路由表、安全组或 VPN 配置。

完成条件：

- 用户可以从网络清单选择 VPC 内网节点并打开 SSH。
- 用户可以通过跳板机路径访问 VPC 内网机器。
- 用户可以在两台远程机器之间创建文件复制任务，并看到队列、进度和错误。

## 13. Phase 11：可试用性收口

目标：收敛成可试用 MVP。

允许做：

- 错误详情弹窗。
- 删除确认。
- 设置页和诊断入口。
- 导入导出基础能力。
- 日志脱敏检查。

禁止做：

- 新增非 SSH 协议。
- 做宏录制、脚本引擎、X11 server、云同步。

完成条件：

- 用户可以保存 profile、打开终端、浏览 SFTP、上传下载、创建端口转发。
- 用户可以通过跳板机或手工登记的内网入口打开 SSH。
- 常见失败都有可读、脱敏、可诊断的错误。
- build / test / desktop smoke / secret scan 均可执行并记录结果。

## 14. Phase 12：非 UI 模块真实化与工程加固

目标：在暂缓 `AtomSSH.Desktop` 的前提下，把 Core、Application、Network、Session、Transfer、Infrastructure 从“最小闭环 / fake 链路 / 骨架验证”推进到真实业务代码可承载 Desktop 接入的状态。

详细设计、模块完成条件、禁止事项、验收命令和推进顺序见：

- `docs/implementation/phase-12-realization-hardening.md`

允许做：

- 补齐 Infrastructure 凭据 secret store。
- 补齐 Session 认证、Host Key、JumpHost / ProxyJumpChain 边界、SFTP、端口转发真实能力。
- 补齐 Transfer 真实 scheduler、取消、失败和 LocalRelay 流式复制。
- 补齐 Network 真实路径规划和 TCP 诊断。
- 补齐 Application 用例校验、脱敏和真实执行计划编排。
- 将默认 DI 注册从 fake 路径调整为真实生产路径，并保留显式 fake 注册用于测试。

禁止做：

- 在 Desktop 之前把 UI 逻辑塞入 Application、Session、Transfer、Network 或 Infrastructure。
- 为了真实化而破坏模块依赖方向。
- 让 SSH.NET 类型泄漏到 Session 之外。
- 明文保存 secret material。
- 用固定成功结果冒充真实实现。

完成条件：

- 非 Desktop 模块默认生产注册不再指向 fake runtime。
- fake 实现仅用于测试、开发演示或显式 fake composition。
- product 功能文档中非 UI 主线能力均有真实实现或清晰结构化未实现错误。
- build / test / secret scan / 架构边界扫描均通过。

## 15. 跳步规则

以下跳步禁止发生：

- 未完成 Phase 0 就写业务功能。
- 未完成 Core 契约就接真实 SSH 库。
- 未完成组合根就让模块自己解析 DI。
- 未完成 fake session 链路就写复杂终端 UI。
- 未完成真实 SSH 最小闭环就做宏、多会话广播或脚本引擎。
- 未完成 SFTP 最小闭环就做断点续传和复杂并发。
- 未完成远端复制 LocalRelay 就做复杂 rsync 编排。
- 未完成 Phase 12 非 UI 模块真实化就把 Desktop UI 直接绑定到底层 fake 实现。
- 未完成凭据安全存储就把 password、private key body 或 passphrase 写入普通配置文件。

如果必须跳步，必须先更新本文档，并说明跳步原因、风险和回滚方式。

## 16. 当前推荐起点

当前推荐从 Phase 12 开始：

```text
Phase 12：非 UI 模块真实化与工程加固
  -> Infrastructure 凭据 secret store
  -> Session 认证、Host Key、连接路径和 SSH.NET adapter 加固
  -> Transfer 真实 scheduler、取消、失败和 LocalRelay 流式复制
  -> Network 真实路径规划和 TCP diagnostics
  -> Application 用例校验、脱敏和真实执行计划
  -> DI 默认注册真实化
  -> build / test / secret scan / 边界扫描
```
