# AtomSSH.Infrastructure 凭据与安全

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的凭据保存、解析、脱敏和安全边界
>
> 变更规则：调整凭据策略时必须同步更新本文档和 lifecycle 文档。

## 1. 凭据存储

凭据存储实现 Core 的 `ICredentialStore`。第一版可以使用系统安全存储或本地加密文件，但必须保证 secret 不进入普通配置。

## 2. 凭据解析

凭据解析结果只在 Session 或 Transfer 执行链路中短期存在。解析结果必须有明确释放语义。

## 3. 脱敏

以下内容必须脱敏：

- password
- passphrase
- private key body
- token
- 完整连接串
- 可能包含用户名和密码的代理 URL

日志、错误详情和测试输出都必须遵守脱敏规则。

## 4. Host Key

known hosts 存储必须能区分首次信任和变更风险。Host key 变化不得自动覆盖。
