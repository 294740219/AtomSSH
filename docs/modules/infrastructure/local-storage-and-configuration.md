# AtomSSH.Infrastructure 本地存储与配置

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的本地存储和配置边界
>
> 变更规则：调整本地持久化策略时必须同步更新本文档。

## 1. 配置目录

应用必须通过统一配置目录服务解析：

- profiles
- settings
- known hosts
- transfer tasks
- transfer state
- logs

路径解析失败属于启动诊断错误。

## 2. 配置文件

普通配置可以保存：

- profile 非敏感字段。
- UI 设置。
- terminal profile。
- port forward profile。
- known hosts 指纹。

普通配置禁止保存：

- 密码。
- 私钥内容。
- passphrase。
- token。
- 任何 secret material。

## 3. 迁移

第一版应预留 schema version。配置损坏时不应静默覆盖用户数据。
