# 活动监控器 — 分类模块报错日志

> 创建日期：2026-07-21
> 用途：记录内容分析工程师（C1-C6）在开发过程中遇到的编译/运行报错及其解决方案

---

### [2026-07-21] [Classification] ActivityEvent 命名歧义 — System.Diagnostics.ActivityEvent

**错误现象：**
```
error CS0104: "ActivityEvent"是"ActivityMonitor.Core.Models.ActivityEvent"
和"System.Diagnostics.ActivityEvent"之间的不明确的引用
```
涉及文件：WindowTracker.cs (line 48), CrashRecoveryService.cs (lines 55, 187)

**根因分析：**
.NET 8 引入 `System.Diagnostics.ActivityEvent` 类，与项目自定义模型 `ActivityMonitor.Core.Models.ActivityEvent` 重名。
这两个文件均使用 `using System.Diagnostics;`，导致编译器无法区分 `ActivityEvent` 引用哪个类型。

**解决方案：**
将 `using System.Diagnostics;` 替换为具体类型别名导入：
- `using Debug = System.Diagnostics.Debug;`
- `using Process = System.Diagnostics.Process;`

仅导入项目中实际使用的类型，避免整个命名空间污染。

**预防措施：**
- 模型类命名时注意避免与 .NET BCL 新引入的类型冲突
- 后续使用 `using` 声明优先导入具体类型而非整个命名空间
- 代码审查时检查是否包含可能冲突的命名空间导入

**涉及文件：**
- `src/ActivityMonitor.Core/Tracking/WindowTracker.cs`
- `src/ActivityMonitor.Core/Tracking/CrashRecoveryService.cs`

**状态：** ✅ 已修复
