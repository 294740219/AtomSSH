# AtomSSH 当前开发上下文

更新时间：2026-06-16

本文档是滚动上下文，用于恢复当前设计状态和下一步工作入口。冻结设计以 `docs/product/features.md`、`docs/architecture/`、`docs/modules/`、`docs/ui/` 和 `docs/implementation/roadmap.md` 为准；本文档不定义新的架构边界。

## 工作规则

当前项目处于设计落地前阶段。正式编码前必须按以下顺序加载上下文：

1. 读取 `docs/implementation/roadmap.md`，确定当前 Phase 和目标模块。
2. 读取 `docs/product/features.md`，确认产品功能、版本范围和产品模块到工程模块的映射。
3. 读取 `docs/architecture/overview.md` 和 `docs/architecture/lifecycle.md`，建立全局架构、依赖和生命周期边界。
4. 读取 `docs/modules/overview.md`，判断改动属于哪个模块。
5. 按目标模块读取 `docs/modules/` 下对应设计文档。
6. 如果涉及 Desktop 页面或交互，再读取 `docs/ui/` 下对应 UI 文档。

## 当前实际进度

当前仓库只有 Git 仓库和设计文档目录，尚未创建 C# 解决方案、生产项目或测试项目。

已完成：

- 创建 AtomSSH 文档体系。
- 按 AtomBox 文档风格建立 architecture、modules、ui、implementation 分层。
- 新增 `docs/product/features.md`，作为产品功能总账和产品模块到工程模块的映射表。
- 参考 MobaXterm 和 SecureCRT 能力，收敛 AtomSSH 第一版和后续能力边界。
- 补充云 VPC 内网、跳板机、连接路径规划和远端到远端文件传递场景。

## 下一步入口

下一步应从 `docs/implementation/roadmap.md` 的 Phase 0 开始：

1. 创建解决方案和项目骨架。
2. 建立 `Core`、`Application`、`Network`、`Session`、`Transfer`、`Infrastructure`、`Desktop` 的项目引用方向。
3. 创建测试项目。
4. 确认 `Core` 不依赖任何外层模块。

## 注意事项

- `D:\work\c#\AtomBox\docs` 只作为文档风格参考，不属于 AtomSSH 项目内容。
- 未完成 Core 契约前，不接真实 SSH 协议库。
- 未完成 fake session 链路前，不写复杂终端 UI。
- 未完成生命周期文档阅读前，不写 DI 注册。
