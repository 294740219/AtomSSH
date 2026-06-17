# AtomSSH.Application 模块设计

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的 Application 模块边界、目录规划、首批用例和实现约束
>
> 变更规则：实现阶段不得随意修改本文件；如需调整 Application 边界，必须同步更新 architecture 和 modules 文档。

## 1. 模块定位

`AtomSSH.Application` 是用户用例编排层。它把 Desktop 传入的用户动作转换为明确应用流程，例如保存会话、连接测试、打开终端、浏览 SFTP、创建端口转发、规划连接路径、发送命令片段、导入导出和提交传输任务。

Application 不是 UI 层，也不是 SSH 协议实现层。

## 2. 边界规则

Application 允许包含：

- 用户用例服务。
- 用例请求、结果、查询条件和摘要 DTO。
- 应用级流程编排和校验。
- 面向 DI 的注册扩展。

Application 禁止包含：

- Avalonia、AtomUI、ViewModel、View。
- 具体 SSH 协议库、终端控件、SFTP client。
- JSON 配置读写、凭据加密、known hosts 文件格式。
- Transfer worker、SSH connection、PTY channel、port forward listener。
- 跨模块稳定端口定义。

## 3. 推荐目录

```text
src/AtomSSH.Application/
  Profiles/
  Sessions/
  Sftp/
  Transfers/
  Network/
  PortForwarding/
  CommandSnippets/
  Settings/
  ImportExport/
  DependencyInjection/
```

## 4. 首批用例服务

- `ProfileAppService`：会话配置增删改查、分组、导入导出入口。
- `SessionAppService`：连接测试、打开终端、关闭终端、重连、查询活动会话。
- `SftpAppService`：列目录、刷新、创建目录、删除、重命名。
- `TransferAppService`：创建上传下载任务、取消、重试、查询队列和历史。
- `NetworkInventoryAppService`：列出网络空间、登记内网节点、规划连接路径、查询路径诊断。
- `PortForwardAppService`：保存转发配置、启动、停止、查询运行状态。
- `CommandSnippetAppService`：新增、编辑、删除、查询命令片段，并发送到当前活动终端。
- `SettingsAppService`：读取、保存、重置设置。
- `ImportExportAppService`：导入导出 profile、settings、port forward profiles、network inventory、command snippets 和非敏感配置。

## 5. 设计约束

- Application 只依赖 Core。
- Application 不直接 new 具体 SSH client。
- Application 可以请求 Core 端口执行短流程，但不持有长期会话。
- 打开终端用例只能返回会话实例 ID、状态快照和受控终端 I/O 句柄；不得把可直接操作底层协议的 channel、stream 或 client 对象暴露给 ViewModel。
- 连接路径相关用例只能消费 Core 的 route planner 和 diagnostics 端口；Application 不依赖 `AtomSSH.Network` 项目。
- ViewModel 只能调用 Application 服务，不直接进入 Network、Session、Transfer、Infrastructure 或 Core 底层端口。
- Application 返回结果必须适合 UI 展示，但不能包含 UI 类型。
