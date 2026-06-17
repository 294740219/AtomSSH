# AtomSSH.Session 终端状态设计

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的终端通道状态和 UI 边界
>
> 变更规则：调整终端状态传播方式时必须同步更新本文档。

## 1. 终端通道

终端通道表达：

- 标准输入写入。
- 标准输出读取。
- 窗口尺寸变化。
- 退出状态。
- 连接状态。

终端通道不能暴露具体 SSH 协议库 channel 类型。

## 2. UI 边界

Desktop 可以使用终端控件渲染字节流或字符流，但终端控件对象不能进入 Session 或 Core。

ViewModel 只持有：

- 会话实例 ID。
- 连接状态快照。
- 当前标题。
- 错误展示状态。

## 3. 状态快照

终端状态至少包含：

- Connecting
- Authenticating
- VerifyingHostKey
- Connected
- Disconnecting
- Disconnected
- Failed

状态变化必须可供 Application 查询或订阅，但第一版不把 IObservable 作为主公共 API。
