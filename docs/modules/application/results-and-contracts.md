# AtomSSH.Application 结果与契约

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的用例输入输出结果约束
>
> 变更规则：新增公开用例结果对象时必须同步更新本文档。

## 1. 结果模型

Application 用例优先返回 Core 的 `OperationResult<T>`。

结果对象可以包含：

- 数据快照。
- 分页信息。
- 能力标志。
- 脱敏错误详情。
- 下一步建议文案 key 或结构化提示。

结果对象不能包含：

- Avalonia / AtomUI 类型。
- SSH 协议库对象。
- SFTP client。
- secret material。
- 原始 exception。

## 2. 契约稳定性

用例请求和结果是 Presentation 调用 Application 的稳定边界。ViewModel 可以把结果转换成 UI 行模型，但不能要求 Application 返回 UI 专用控件状态。

## 3. 错误展示

错误结果必须包含：

- 用户可读摘要。
- 脱敏详情。
- 错误类别。
- 是否可重试。
- 关联对象 ID，例如 profile ID 或 task ID。

错误详情不得包含密码、私钥、passphrase、完整连接串或敏感路径。
