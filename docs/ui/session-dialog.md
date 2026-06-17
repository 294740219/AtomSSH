# AtomSSH 会话配置弹窗

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的新增和编辑 SSH profile 弹窗职责
>
> 变更规则：调整会话配置流程时必须同步更新本文档。

## 1. 用途

会话配置弹窗用于新增和编辑 SSH profile。

## 2. 信息分组

第一版表单分组：

- 基本信息：名称、分组、备注。
- 连接信息：host、port、username。
- 认证信息：密码、私钥、passphrase、凭据引用。
- 终端设置：编码、初始目录、启动命令、终端 profile。
- 高级设置：keepalive、代理或 jump host 引用。

## 3. 规则

- 密码和 passphrase 不显示在普通文本中。
- 保存时通过 Application 用例提交。
- 连接测试通过 Application 用例触发。
- 错误详情必须脱敏。
