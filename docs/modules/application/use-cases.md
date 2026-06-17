# AtomSSH.Application 用例设计

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的首批用户用例和边界
>
> 变更规则：新增用户流程时必须同步更新本文档。

## 1. 会话配置用例

- 新增 SSH profile。
- 编辑 SSH profile。
- 删除 SSH profile。
- 复制 SSH profile。
- 移动到分组。
- 列出 profile 和分组。
- 导入 / 导出 profile。

删除 profile 前必须检查是否仍有活动会话、端口转发或未完成传输任务引用。

## 2. 连接与终端用例

- 测试连接。
- 打开终端。
- 关闭终端。
- 重连终端。
- 查询活动会话。
- 发送窗口尺寸变化。

连接测试必须覆盖认证、host key、网络连通性和端口可达性，但不创建长期终端 channel。

打开终端、连接测试和重连必须先通过 `IConnectionRoutePlanner` 获得 `ConnectionRoute`，再调用 `ISshSessionRuntime`。Session Runtime 只消费规划结果，不读取网络清单，不自行决定跳板机或代理链。

## 3. SFTP 浏览用例

- 打开远程目录。
- 刷新目录。
- 返回上级目录。
- 创建目录。
- 删除远程文件或目录。
- 重命名远程文件或目录。
- 创建上传 / 下载任务。

SFTP 浏览返回远程快照，不返回活动 SFTP client。

从 profile 打开 SFTP 时，Application 必须复用连接路径规划流程，向 Session 传入已规划的 `ConnectionRoute`，不能让 SFTP 用例绕过 Network planner。

## 4. 传输用例

- 创建上传任务。
- 创建下载任务。
- 创建远端到远端复制任务。
- 查询队列。
- 查询历史。
- 取消任务。
- 重试失败任务。
- 清理历史。

任务创建只生成描述并提交调度，不直接执行传输。

远端到远端复制用例必须明确源 profile、目标 profile、源路径、目标路径、传递模式和覆盖策略。

上传、下载、远端复制在提交给 `ITransferTaskScheduler` 前，Application 必须先完成所需 profile 的路径规划，并生成 `TransferExecutionPlan`。Transfer worker 只消费执行计划，不负责规划 route，不读取 network inventory。

## 5. 端口转发用例

- 创建本地端口转发配置。
- 创建远程端口转发配置。
- 创建动态 SOCKS 配置。
- 启动转发实例。
- 停止转发实例。
- 查询转发状态。

端口占用和远端拒绝必须返回结构化错误。

启动端口转发前必须完成路径规划。Port Forward 用例保存和读取的是 `PortForwardProfile`，运行时启动消费的是已规划 `ConnectionRoute` 和转发配置。

## 6. 网络清单和路径用例

- 列出网络空间。
- 手工登记 VPC 内网机器。
- 规划目标 profile 的连接路径。
- 诊断直连、跳板机、代理链和目标 SSH 端口是否可达。

网络用例只返回节点和路径规划结果，不创建 SSH client。

## 7. 命令片段用例

- 新增命令片段。
- 编辑命令片段。
- 删除命令片段。
- 按分组列出命令片段。
- 发送命令到当前活动终端。

命令片段用例只发送文本到 Application 暴露的终端输入边界，不直接访问协议库 channel。

## 8. 导入导出用例

- 导出 profile、settings、network inventory、port forward profiles 和 command snippets。
- 导入 profile、settings、network inventory、port forward profiles 和 command snippets。
- 导入前预览冲突。
- 处理冲突后提交导入。

导入导出默认不包含 secret material，不能静默覆盖已有 profile。
