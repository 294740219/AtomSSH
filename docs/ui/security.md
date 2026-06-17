# AtomSSH 凭据与安全页

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的凭据与安全页职责和第一版边界
>
> 变更规则：调整凭据、私钥、Host Key 或 known hosts 展示流程时必须同步更新本文档。

凭据与安全页用于管理 SSH 登录所需的敏感配置引用和远端身份信任记录。

## 1. 第一版能力

- 查看凭据条目摘要，不显示 secret material。
- 新增、编辑、删除密码或私钥凭据引用。
- 管理 known hosts。
- 查看 Host Key 指纹。
- 删除或重新信任 Host Key 前显示确认。
- 查看安全相关错误详情，详情必须脱敏。

## 2. 边界

页面只通过 Application 用例读写凭据引用和 Host Key 信任决策，不能直接访问 `ICredentialStore`、`IHostKeyTrustStore` 或任何 secret material。

凭据输入控件可以接收用户输入，但保存后 ViewModel 不得长期持有密码、私钥内容或 passphrase。
