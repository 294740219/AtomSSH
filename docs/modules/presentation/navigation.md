# AtomSSH.Presentation 导航设计

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的 Desktop 导航边界
>
> 变更规则：调整导航结构时必须同步更新本文档和 `docs/ui/menu.md`。

## 1. 导航目标

第一版导航目标：

- 连接
- 终端工作区
- SFTP
- 传输队列
- 传输历史
- 端口转发
- 网络清单
- 远端复制
- 命令片段
- 设置
- 凭据与安全
- 导入导出

## 2. 规则

- Navigation 只切换 UI 页面或工作区。
- Navigation 不直接执行 SSH 连接。
- 打开 profile 必须通过 Application 用例。
- 导航参数只能使用 ID、枚举、轻量值对象，不传递 ViewModel 或运行时连接。
