---
name: project-docs-convention
description: 项目文档存放位置和命名规范
metadata:
  type: reference
---

**项目文档**（需求、架构、规范等）放在 `doc/` 目录下，按编号序列命名：
- 格式：`XX-标题.md`（如 `00-需求文档.md`、`07-运行报错日志.md`）
- 新文档接续最大编号 +1

**Agent 定义**放在 `.claude/agents/*.md`
**Workflow 脚本**放在 `.claude/workflows/*.js`
**Memory 记录**放在 `.claude/memory/xxx.md`，同时在 `MEMORY.md` 加一行索引

**为什么：** 这是项目已有的文档规范，统一位置方便所有 agent 和开发者查找。
