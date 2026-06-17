# AtomSSH.Network 依赖注入设计

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的 Network DI 边界
>
> 变更规则：调整 Network 注册方式时必须同步更新本文档。

Network 可以暴露：

```csharp
IServiceCollection AddAtomSSHNetwork(this IServiceCollection services)
```

允许注册：

- `IConnectionRoutePlanner` 的实现。
- `INetworkDiagnosticsService` 的实现。
- 轻量路径规则、诊断规则和策略对象。

禁止：

- 注册 ViewModel 或 View。
- 注册 SSH client、SFTP client、terminal channel、Transfer worker。
- 注册 network inventory 的持久化 store 具体实现；该实现属于 Infrastructure。
- 定义或注册 Core 网络模型。
- 在模块内部 `BuildServiceProvider()`。
- 注入 `IServiceProvider` 后动态解析普通业务依赖。
