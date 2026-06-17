# AtomSSH.Presentation 模块设计

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的 Presentation 模块边界、目录规划和实现约束
>
> 变更规则：实现阶段不得随意修改本文件；如需调整 Presentation 边界，必须同步更新 architecture 和 modules 文档。

## 1. 模块定位

`AtomSSH.Presentation` 是桌面端展示与交互层。物理项目为 `AtomSSH.Desktop`，负责 Avalonia / AtomUI 启动、窗口、页面、ViewModel、命令绑定、主题资源和桌面交互体验。

Presentation 不是业务编排层，也不是 SSH 运行时层。

## 2. 边界规则

Presentation 允许包含：

- View、Window、UserControl、Style、Resource。
- ViewModel、UI 状态、命令绑定。
- UI 专用行模型和弹窗状态。
- Desktop 组合根。

Presentation 禁止包含：

- 具体 SSH client、SFTP client、terminal channel 实现。
- 凭据加密实现。
- Transfer worker。
- 仓储文件格式。
- 把 Avalonia / AtomUI 类型泄漏给 Core、Application、Session、Transfer 或 Infrastructure。

## 3. 推荐目录

```text
src/AtomSSH.Desktop/
  App.axaml
  App.axaml.cs
  Program.cs
  Assets/
  Composition/
  Shell/
  Navigation/
  ViewFactory/
  Dialogs/
  Services/
  Views/
  ViewModels/
  Resources/
```

## 4. 设计约束

- ViewModel 只调用 Application 用例服务。
- ViewModel 不能保存 secret material。
- 终端控件只属于 Presentation。
- 错误展示消费 Application 返回的脱敏错误结果。
- Desktop 是唯一生产组合根。
