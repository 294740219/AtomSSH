# AtomSSH.Presentation 依赖注入设计

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的 Desktop 组合根和 DI 边界
>
> 变更规则：调整 Desktop 组合根时必须同步更新本文档。

Desktop 是唯一生产组合根。

允许：

- 调用 `AddAtomSSHApplication()`。
- 调用 `AddAtomSSHNetwork()`。
- 调用 `AddAtomSSHSession()`。
- 调用 `AddAtomSSHTransfer()`。
- 调用 `AddAtomSSHInfrastructure()`。
- 注册 ViewModel、ViewFactory、DialogService、NavigationService。

禁止：

- 在内层模块构建 ServiceProvider。
- 把 ViewModel 注入 Core、Application、Session、Transfer 或 Infrastructure。
- 把 SSH connection 注册成 Singleton。
