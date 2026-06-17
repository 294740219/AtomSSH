# AtomSSH.Application 依赖注入设计

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的 Application DI 边界
>
> 变更规则：调整 Application 注册方式时必须同步更新本文档。

Application 可以暴露：

```csharp
IServiceCollection AddAtomSSHApplication(this IServiceCollection services)
```

允许注册：

- 用例服务。
- 用例级 validator。
- 轻量 mapper 或 factory。

禁止：

- 在 Application 内部调用 `BuildServiceProvider()`。
- 注入或持有 `IServiceProvider`。
- 注册具体 SSH client、SFTP client、terminal channel。
- 注册 Infrastructure store 的具体实现。
- 注册 Network planner、Session runtime、Transfer scheduler 的具体实现。
- 注册 ViewModel 或 View。

Application 服务默认可以按无状态长期服务注册，但不能持有运行时资源。
