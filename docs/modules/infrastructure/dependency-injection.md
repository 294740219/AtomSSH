# AtomSSH.Infrastructure 依赖注入设计

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的 Infrastructure DI 边界
>
> 变更规则：调整 Infrastructure 注册方式时必须同步更新本文档。

Infrastructure 可以暴露：

```csharp
IServiceCollection AddAtomSSHInfrastructure(this IServiceCollection services)
```

允许注册：

- profile repository。
- port forward profile repository。
- network inventory repository。
- command snippet repository。
- settings repository。
- credential store。
- known hosts store。
- transfer task/state store。
- import/export service。
- logging services。

禁止：

- 注册 ViewModel 或 View。
- 注册 SSH connection、terminal channel、SFTP channel。
- 在模块内部 `BuildServiceProvider()`。
- 注入 `IServiceProvider` 后动态解析普通业务依赖。
