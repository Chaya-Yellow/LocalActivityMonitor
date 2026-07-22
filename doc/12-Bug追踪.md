# 活动监控器 — Bug 追踪

> 创建日期：2026-07-22
> 用途：记录测试发现的 Bug，按责任人分类，跟踪修复状态。
> 工作流：测试工程师发现 Bug → 写入此文档 → 对应角色修复 → 测试复测 → 关闭

---

## Bug 状态说明

| 状态 | 含义 |
|:----:|------|
| 🔴 待修复 | 测试发现，尚未分配给任何人 |
| 🟡 修复中 | 已分配给对应角色，正在修 |
| 🟢 已修复 | 代码已合并，等待复测 |
| ✅ 已关闭 | 复测通过 |
| ❌ 非Bug | 确认是预期行为或无法复现 |

---

## Bug 列表

<!-- 按编号倒序，最新的在最上面 -->

| 编号 | 测试用例 | 现象（实际 vs 预期） | 责任人 | 涉及文件:行号 | 状态 | 备注 |
|:----:|---------|-------------------|:------:|:-------------|:----:|:----:|
| BUG-001 | ExportDailyAsync_NoEvents_ReturnsMarkdownWithAllSections | 实际:2026-07-22 预期:2026-07-21 | 数据报表工程师 | DailyReportBuilder.cs:27 | ✅ 已关闭 | 复测通过，183 tests passed |
| BUG-002 | Core 项目编译失败（缺失 Win32 目录） | .gitignore 误拦截 src/ActivityMonitor.Core/Win32/ 源码目录，导致 NativeMethods.cs 未提交 | 技术架构师 | .gitignore:21 | 🔴 待修复 | [Ww][Ii][Nn]32/ 规则匹配了源码路径 |
| BUG-003 | 测试项目启用 UseWPF 后 System.IO 隐式 using 丢失 | 8 个测试文件编译失败：Path/File/Directory 类型未找到 | 技术架构师 | ActivityMonitor.Tests.csproj | 🟢 已修复 | 已在各文件添加 using System.IO; |

---

## 按责任人筛选

### 后端工程师

| 编号 | 摘要 | 状态 |
|:----:|------|:----:|
| — | 暂无 | — |

### 前端工程师

| 编号 | 摘要 | 状态 |
|:----:|------|:----:|
| — | 暂无 | — |

### 内容分析工程师

| 编号 | 摘要 | 状态 |
|:----:|------|:----:|
| — | 暂无 | — |

### 数据报表工程师

| 编号 | 摘要 | 状态 |
|:----:|------|:----:|
| BUG-001 | 空事件时日报日期显示为 DateTime.Today | ✅ 已关闭 |

### 技术架构师（数据层/接口/Win32）

| 编号 | 摘要 | 状态 |
|:----:|------|:----:|
| BUG-002 | .gitignore 误拦截 src/ActivityMonitor.Core/Win32/ 目录 | 🔴 待修复 |
| BUG-003 | 测试项目 UseWPF 后 System.IO using 丢失 | 🟢 已修复 |

---

## Bug 详细描述

> 每个 Bug 展开写在这里，包括复现步骤、环境信息、截图/日志。

### BUG-001: ExportDailyAsync_NoEvents_ReturnsMarkdownWithAllSections
- **责任人：** 数据报表工程师
- **涉及文件：** DailyReportBuilder.cs:27
- **复现步骤：**
  1. 调用 `MarkdownExporter.ExportDailyAsync(new DateTime(2026, 7, 21))`
  2. 目标日期无任何活动事件（空数据库）
  3. `DailyReportBuilder.Build()` 中 `events.Count == 0` 分支设置 `data.Date = DateTime.Today`
- **实际结果：** 生成的日报标题为 "2026-07-22 (周三)" （运行测试时的当天日期）
- **预期结果：** 日报标题应为 "2026-07-21 (周二)"（与传入日期参数一致）
- **日志：**
  ```
  Expected markdown "# 工作日报 - 2026-07-22 (周三)
  ..." to contain "2026-07-21".
  ```
- **发现日期：** 2026-07-22
- **修复分支：** `fix/report-bug-001`
- **修复方案：** `Build()` 签名改为 `Build(DateTime, IReadOnlyList<ActivityEvent>, DailySummary?)`
- **复测结果（2026-07-22）：** ❌ 修复后未同步更新 `DailyReportBuilderTests.cs`，20+ 行调用旧签名 `Build(events, summary)` 导致编译失败
  ```
  DailyReportBuilderTests.cs(205,29): error CS7036: 未提供与"DailyReportBuilder.Build(DateTime, IReadOnlyList<ActivityEvent>, DailySummary?)"的所需参数"events"对应的参数
  ```
  **结论：** 源代码修复正确，但测试文件未同步更新，无法编译验证。需数据报表工程师更新测试后重新复测。
- **最终修复（2026-07-22）：** 数据报表工程师在 `fix/report-bug-001` 提交 `113e4a2`，同步更新所有测试调用签名。cherry-pick 到 main 后 `dotnet test` 全部 183 个通过 ✅
- **关闭日期：** 2026-07-22

---

### BUG-002: .gitignore 误拦截 src/ActivityMonitor.Core/Win32/ 源码目录
- **责任人：** 技术架构师
- **涉及文件：** `.gitignore:21`
- **现象：** Core 项目编译失败，4 个 Tracking 文件（ActivityEngine.cs, IdleDetector.cs, LockScreenDetector.cs, WindowTracker.cs）引用了 `ActivityMonitor.Core.Win32` 命名空间，但 `NativeMethods.cs` 因 `.gitignore` 规则 `[Ww][Ii][Nn]32/` 被排除在版本控制之外。
- **根因：** `.gitignore` 中 `[Ww][Ii][Nn]32/` 规则本意是排除 build 产物目录 `Win32/`（与 `x64/`、`x86/` 并列），但该规则也匹配了源码目录 `src/ActivityMonitor.Core/Win32/`。
- **临时绕过：** 从其他 worktree 复制 Win32/ 目录到当前工作区，并在 `.gitignore` 中添加 `!src/ActivityMonitor.Core/Win32/` 例外规则。
- **修复方案：** 在 `.gitignore` 中为源码 Win32 目录添加 `!src/ActivityMonitor.Core/Win32/` 例外，并确保 `NativeMethods.cs` 文件已提交到版本控制。
- **发现日期：** 2026-07-22
- **分支：** feature/test
- **状态：** 🔴 待修复

### BUG-003: 测试项目启用 UseWPF 后 System.IO 隐式 using 丢失
- **责任人：** 技术架构师
- **涉及文件：** `ActivityMonitor.Tests.csproj`；8 个测试文件
- **现象：** 在测试项目中添加 `<UseWPF>true</UseWPF>` 后，8 个已有测试文件编译失败，报告 `Path`、`File`、`Directory` 类型在当前上下文中不存在。
- **根因：** .NET SDK 的 `<UseWPF>true</UseWPF>` 属性改变了默认的隐式 using 集合，移除了 `System.IO` 的自动导入。
- **受影响文件：** ProjectDetectorTests.cs, ActivityEventRepositoryTests.cs, SettingsRepositoryTests.cs, MarkdownExporterTests.cs, WeeklyReportBuilderTests.cs, CrashRecoveryServiceTests.cs, AggregationServiceTests.cs, ActivityEngineTests.cs
- **修复方案：** 在每个受影响文件开头添加 `using System.IO;`
- **发现日期：** 2026-07-22
- **分支：** feature/test
- **状态：** 🟢 已修复（本次提交已修复）

---
