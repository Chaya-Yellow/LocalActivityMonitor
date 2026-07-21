# 报表模块 — 构建与报错记录

> 创建日期：2026-07-21
> 作用：记录报表模块实现过程中的编译报错及解决方案

---

## 构建验证结果

| 项目 | 状态 | 说明 |
|------|------|------|
| ActivityMonitor.Core | ✅ 构建成功 | |
| ActivityMonitor.Data | ✅ 构建成功 | 含新增的 3 个聚合服务 |
| ActivityMonitor.TrayApp | ❌ **XAML 报错** | 预置骨架文件问题，非本模块代码 |

### [2026-07-21] [Report] TrayApp 骨架 XAML 编译错误

**错误现象：**
```
src\ActivityMonitor.TrayApp\App.xaml(34,25): error MC4005: 无法在类型
  "System.Windows.Controls.Button"上找到 Style Property"CornerRadius"。
src\ActivityMonitor.TrayApp\Dashboard\Controls\RealTimeStatsControl.xaml(52,38):
  error MC3000: "'local' is an undeclared prefix." XML 无效。
```

**根因分析：**
- `App.xaml` 中引用了 `CornerRadius` 属性，该属性仅在 .NET 8 WPF 中需要额外 `Microsoft.UI.Xaml` 包或自定义附加属性，骨架代码中直接使用但缺少依赖
- `RealTimeStatsControl.xaml` 中使用了 `local:` 前缀引用本地控件类，但缺少对应的 `xmlns:local` 命名空间声明

**解决方案：** 不属于报表模块职责范围，需由前端工程师在后续 Wave 1 中修复 XAML 骨架文件。

**涉及文件：**
- `src/ActivityMonitor.TrayApp/App.xaml`
- `src/ActivityMonitor.TrayApp/Dashboard/Controls/RealTimeStatsControl.xaml`

---

## 实现文件清单

| 文件 | 行数 | 功能 |
|------|------|------|
| `src/ActivityMonitor.TrayApp/Exporters/DailyReportBuilder.cs` | ~180 | 将 ActivityEvent 转为结构化日报模型 |
| `src/ActivityMonitor.TrayApp/Exporters/MarkdownExporter.cs` | ~260 | IReportExporter 实现，6 章节 Markdown 输出 |
| `src/ActivityMonitor.Data/Aggregation/DailyAggregationService.cs` | ~160 | 每日 00:05 聚合，SQL GROUP BY |
| `src/ActivityMonitor.Data/Aggregation/WeeklyAggregationService.cs` | ~140 | 每周一合并 7 天 daily_summaries |
| `src/ActivityMonitor.Data/Aggregation/MonthlyAggregationService.cs` | ~140 | 每月 1 日合并整月 daily_summaries |
| `src/ActivityMonitor.TrayApp/ActivityMonitor.TrayApp.csproj` | — | 新建项目文件，引用 Core + Data |

---

## 关键设计决策

1. **SQL 聚合不加载全量到内存**：DailyAggregationService 使用 `SQL GROUP BY` 在数据库端完成聚合，仅返回合计行，不加载原始事件行到 C# 内存
2. **Markdown 用 StringBuilder**：MarkdownExporter 全程使用 `StringBuilder` 拼接，不产生中间字符串
3. **周/月聚合基于 daily_summaries**：Weekly/MonthlyAggregationService 从已聚合的 daily_summaries 表读取，合并 JSON breakdown 字典，不再查询原始 activity_events 表
4. **JSON 序列化使用 System.Text.Json**：对 Breakdown 字典做 JSON 序列化/反序列化，内置 .NET 8 无额外依赖
