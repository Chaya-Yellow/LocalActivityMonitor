---
name: agent-preflight
description: Agent 开发前必读流程 — doc/03-开发规范.md#十五，所有 agent 编码前必须执行
metadata:
  type: reference
---

`doc/03-开发规范.md` 第 15 章规定了所有 Agent 开发前必读流程。

**五个步骤（编码前）：**
1. 读 `doc/09-会话检查点.md` — 知道当前进度
2. 读 `doc/07-运行报错日志.md` — 避免重复踩坑
3. 读 `doc/04-拆分任务.md` 对应任务清单 — 明确范围和依赖
4. 检查目标文件是否已存在 — 增量开发不覆盖
5. 开始编码

**开发中：** 完成最小任务→更新检查点；遇到报错→写 error log；关键决策→记入检查点。

**为什么：** agent 没有这些流程就会凭记忆工作，容易遗忘上下文和踩过的坑。

**如何应用：** 每次 agent 启动时 system prompt 中应引用"Agent 开发前必读流程"。
