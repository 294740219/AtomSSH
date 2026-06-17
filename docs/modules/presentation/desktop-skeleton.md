# AtomSSH.Desktop 桌面骨架

> 文档状态：阶段性冻结
>
> 冻结时间：2026-06-16
>
> 冻结范围：当前文档定义的 Desktop 壳、主窗口和基础区域职责
>
> 变更规则：调整 Desktop 主骨架时必须同步更新本文档和 UI 文档。

Desktop 第一版采用工作台式主界面：

- 顶部 TopBar。
- 左侧 Navigation Tree。
- 中间 Terminal Workspace / Content Area。
- 右侧可选 Inspector 或 SFTP 面板，第一版默认不做复杂 Docking。
- 底部 StatusBar。

主窗口只负责承载区域，不直接执行业务流程。

启动失败时，Desktop 可以显示最小错误界面，但不能进入正常 Shell。
