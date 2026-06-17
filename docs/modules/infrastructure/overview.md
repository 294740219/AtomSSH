# AtomSSH.Infrastructure 模块设计

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的 Infrastructure 模块边界、目录规划和实现约束
>
> 变更规则：实现阶段不得随意修改本文件；如需调整 Infrastructure 边界，必须同步更新 architecture 和 modules 文档。

## 1. 模块定位

`AtomSSH.Infrastructure` 提供本地技术实现，包括配置、profile 仓储、端口转发配置仓储、网络清单仓储、命令片段仓储、传输状态仓储、凭据加密、known hosts、日志、本地缓存和导入导出。

Infrastructure 不承载用户用例，不弹 UI。

## 2. 边界规则

Infrastructure 允许包含：

- 本地文件、JSON、SQLite 或其他存储实现。
- Profile、port forward profiles、network inventory、command snippets、transfer tasks 和 settings 的仓储实现。
- 凭据加密和系统凭据库适配。
- known hosts 存储。
- 日志初始化和脱敏。
- 配置目录解析。

Infrastructure 禁止包含：

- Avalonia、AtomUI、ViewModel。
- Application 用例流程。
- SSH 终端 channel 和 UI 状态。
- 远程协议业务编排。

## 3. 推荐目录

```text
src/AtomSSH.Infrastructure/
  Configuration/
  Profiles/
  PortForwarding/
  NetworkInventory/
  CommandSnippets/
  Credentials/
  HostKeys/
  Logging/
  Storage/
  ImportExport/
  DependencyInjection/
```

## 4. 设计约束

- Infrastructure 只依赖 Core。
- 凭据必须加密保存。
- 日志和错误详情必须脱敏。
- 配置损坏必须返回结构化启动错误。
- Infrastructure 不能自己决定显示弹窗。
