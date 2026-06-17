# AtomSSH AtomUI 集成

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的 AtomUI 使用边界
>
> 变更规则：调整 AtomUI 集成方式时必须同步更新本文档。

AtomSSH UI 层优先使用 AtomUI 控件和主题能力。

## 1. 使用原则

- 主界面、菜单、按钮、弹窗、列表、设置页优先使用 AtomUI。
- 终端渲染控件可以使用专门终端控件，但必须封装在 Presentation。
- 不为了快速实现绕过 AtomUI 风格体系。

## 2. 边界

AtomUI 类型只能出现在 Desktop / Presentation 项目中。Core、Application、Session、Transfer、Infrastructure 均不得引用 AtomUI。
