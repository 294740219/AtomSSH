# AtomSSH.Presentation 弹窗与消息

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的弹窗职责和边界
>
> 变更规则：新增重要弹窗流程时必须同步更新本文档和对应 UI 文档。

第一版必须通过弹窗处理：

- 新增 / 编辑 SSH profile。
- 首次信任 host key。
- Host key 变更拦截。
- 删除 profile 确认。
- 删除远程文件确认。
- 错误详情。
- 端口转发启动失败详情。

弹窗只属于 Presentation。Application、Core、Session、Transfer、Infrastructure 不知道弹窗存在。
