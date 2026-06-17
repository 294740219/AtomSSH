# AtomSSH.Presentation ViewFactory 设计

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的 View / ViewModel 创建边界
>
> 变更规则：调整 ViewFactory 时必须同步更新本文档。

ViewFactory 负责 ViewModel 到 View 的显式映射。

禁止：

- 运行时反射扫描所有 View。
- 在 ViewModel 中直接 new View。
- 在 View 中直接解析业务服务。

允许：

- Desktop 组合根注册 ViewModel。
- ViewFactory 显式创建 View。
- View 通过 DataContext 绑定 ViewModel。
