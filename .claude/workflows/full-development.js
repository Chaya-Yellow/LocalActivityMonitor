// ActivityMonitor 完整开发 Workflow v3
// 使用方式: Workflow({ scriptPath: '.claude/workflows/full-development.js' })
//
// Phase 0: 项目骨架
// Phase 1: PM 审查
// Phase 2: Wave 0 — 架构师
// Phase 3: Wave 1 — 5 角色并行
// Phase 4: 集成

export const meta = {
  name: 'full-development',
  description: '完整开发流程：项目初始化 → 需求审查 → Wave 0 → Wave 1 并行(5角色) → 集成测试',
  phases: [
    { title: 'Init', detail: '创建 .sln + .csproj 项目骨架' },
    { title: 'Review', detail: 'pm-assistant 审查开发计划' },
    { title: 'Wave 0', detail: '技术架构师完成 M1-M4 公共契约' },
    { title: 'Wave 1', detail: '5 个角色 worktree 隔离并行开发' },
    { title: 'Integration', detail: '合并 worktree + 编译 + 全量测试' },
  ],
}

// ============================================================
// Phase 0: 项目初始化
// ============================================================
phase('Init')

log('=== 创建 .NET 项目骨架 ===')

const initResult = await agent(
  `# 创建 ActivityMonitor 项目骨架

  使用 .NET CLI 创建以下 4 个项目结构，不写业务逻辑：

  ## 项目清单

  | 项目 | 类型 | 引用 | NuGet |
  |------|------|------|-------|
  | ActivityMonitor.Core | classlib(net8.0-windows) | — | — |
  | ActivityMonitor.Data | classlib(net8.0-windows) | Core | Microsoft.Data.Sqlite |
  | ActivityMonitor.TrayApp | wpf(net8.0-windows) | Core, Data | CommunityToolkit.Mvvm |
  | ActivityMonitor.Tests | xunit(net8.0-windows) | Core, Data | xunit,FluentAssertions,NSubstitute |

  ## 目录结构
  Core: Models/, Interfaces/, Tracking/, Classification/, Win32/
  Data: Database/, Repositories/, Aggregation/
  TrayApp: TrayIcon/, Dashboard/Controls/, ReportEditor/, History/, Settings/, Exporters/
  Tests: DataTests/, TrackerTests/, ClassificationTests/, ReporterTests/, PerformanceTests/

  ## 全局配置
  - Directory.Build.props: Nullable enable, ImplicitUsings enable
  - 子目录放 .gitkeep

  ## 验收
  \`dotnet build\` 通过`,
  { label: 'project-init' },
)

log(`项目骨架: ${initResult ? '✅' : '❌'}`)

// ============================================================
// Phase 1: 计划审查
// ============================================================
phase('Review')

log('=== PM Assistant 审查 ===')

const review = await agent(
  `审查多 agent 并行开发计划：

  Wave 0: 技术架构师 → M1-M4（数据模型/接口/SQLite/Win32）
  Wave 1（并行）:
    - 后端引擎 → Core/Tracking/ 5 模块
    - 内容分析 → Core/Tracking+Classification/ 6 模块
    - WPF 前端 → TrayApp/ 5 UI 模块（Mock 先行）
    - 数据报表 → Data/Aggregation+TrayApp/Exporters/ 5 模块
    - 测试 → Tests/ 全部用例
  Phase 4: 合并 worktree → build → test

  检查完整性、冲突、边界、是否需要新角色。`,
  {
    label: 'pm-review',
    schema: {
      type: 'object',
      properties: {
        summary: { type: 'string' },
        issues: {
          type: 'array',
          items: {
            type: 'object',
            properties: {
              severity: { type: 'string', enum: ['blocker', 'major', 'minor', 'info'] },
              description: { type: 'string' },
              suggestion: { type: 'string' },
            },
            required: ['severity', 'description'],
          },
        },
        recommendation: { type: 'string', enum: ['approve', 'revise', 'reject'] },
        newRolesNeeded: { type: 'array', items: { type: 'string' } },
      },
      required: ['summary', 'issues', 'recommendation'],
    },
  },
)

log(`=== 审查: ${review.recommendation} ===`)
log(review.summary)
for (const issue of review.issues || []) {
  log(`[${issue.severity}] ${issue.description}`)
  if (issue.suggestion) log(`  建议: ${issue.suggestion}`)
}
if (review.newRolesNeeded?.length) log(`需新增角色: ${review.newRolesNeeded.join(', ')}`)

if (review.recommendation === 'reject') {
  log('❌ 计划驳回，请修改后重试')
  return { status: 'rejected', review }
}

// ============================================================
// Phase 2: Wave 0 — 技术架构师
// ============================================================
phase('Wave 0')

log('=== Wave 0: 架构师 M1-M4（预计 ~2 天）===')

const architectResult = await agent(
  `# 实现 M1-M4 公共契约

  ## 前置阅读
  - doc/01-架构设计.md → 五(数据模型)、四(追踪模块)、十一(隐私)
  - doc/00-需求文档.md → 三(功能需求全貌)
  - doc/03-开发规范.md → 八(Win32 API)、九(SQLite)

  ## 产出清单

  **M1** — Core/Models/{ActivityEvent,DailySummary,WeeklySummary,MonthlySummary}.cs
  **M2** — Core/Interfaces/{IActivityTracker,IActivityRepository,IActivityCategorizer,IIdleDetector,ISleepDetector,ITodayStatsService,IReportExporter,ISettingsRepository}.cs
  **M3** — Data/Database/SqliteContext.cs + Repositories/ 下 5 个实现
  **M4** — Core/Win32/NativeMethods.cs

  ## 关键约束
  - 接口全 XML 注释、Repository 参数化 SQL、建索引
  - Win32 声明标注 DLL + ExactSpelling
  - dotnet build 编译通过无 warning`,
  { label: 'architect', agentType: 'architect', isolation: 'worktree' },
)

log(`Wave 0: ${architectResult ? '✅' : '❌'}`)

if (!architectResult) {
  log('❌ Wave 0 失败，终止流程')
  return { status: 'architect-failed' }
}

// ============================================================
// Phase 2.5: 合并架构师代码 → main（修复 worktree 依赖问题）
// ============================================================
phase('Merge Architect')

log('=== 合并架构师 worktree 到 main ===')

const mergeArchitect = await agent(
  `# 合并架构师 worktree 到 main

  1. git branch | grep -i "worktree.*architect\\|worktree.*wf.*3" 找到架构师分支
  2. git checkout main
  3. git merge --squash <架构师分支>
  4. git commit -m "chore: merge architect M1-M4 to main"
  5. dotnet build 验证编译通过`,
  { label: 'merge-architect' },
)

log(`合并架构师代码: ${mergeArchitect ? '✅' : '❌'}`)

if (!mergeArchitect) {
  log('❌ 架构师合并失败，终止流程')
  return { status: 'merge-architect-failed' }
}

// ============================================================
// Phase 3: Wave 1 — 5 角色并行
// ============================================================
phase('Wave 1')

log('=== Wave 1 并行开发 ===')
log('架构师接口已合入 main，5 角色各在独立 worktree 中工作')

const wave1Results = await parallel([

  // 角色A
  () => agent(
    `# 实现追踪引擎 T1-T5

    ## 读这些文件了解你的模块
    - doc/01-架构设计.md → 4.1(WinTracker), 4.4(Idle), 4.5(Sleep), 4.6(Crash), 4.8(性能)
    - doc/00-需求文档.md → F1, F4, F5
    - Core/Interfaces/ → IActivityTracker, IIdleDetector, ISleepDetector

    ## 要创建的文件
    Core/Tracking/{WindowTracker,IdleDetector,SleepDetector,CrashRecoveryService,ActivityEngine}.cs

    ## 约束
    - Timer 非 while(true), 线程 BelowNormal
    - 空闲降频 10s, 异常 catch 不崩溃
    - 空闲 15min 阈值，恢复延续 is_continued=true`,
    { label: 'engine', agentType: 'backend-engineer', isolation: 'worktree' },
  ),

  // 角色B
  () => agent(
    `# 实现内容解析 C1-C6

    ## 读这些文件
    - doc/01-架构设计.md → 4.2(Browser), 4.3(File), 4.7(Categorizer)
    - doc/00-需求文档.md → F2, F3, F6
    - Core/Interfaces/ → IActivityCategorizer, ITodayStatsService

    ## 要创建的文件
    Core/Tracking/{BrowserTracker,FileTracker}.cs
    Core/Classification/{ProjectDetector,ActivityCategorizer,KeywordExtractor,TodayStatsService}.cs

    ## 一句话说清楚每个模块
    C1: 从窗口标题解析域名(Chrome/Edge/Firefox)
    C2: 从标题+进程名+工作目录推断文件和项目
    C3: 找.git或取文件夹名
    C4: 规则表分类+用户覆盖
    C5: 去停用词提取关键词
    C6: 按app/project/domain/work_tag聚合当天`,
    { label: 'classification', agentType: 'content-analyst', isolation: 'worktree' },
  ),

  // 角色C
  () => agent(
    `# 实现 WPF 界面 U1-U5

    ## 读这些文件
    - doc/01-架构设计.md → 三(架构图), 九(日报模板)
    - doc/00-需求文档.md → F7,F8,F9,F11,F12
    - Core/Interfaces/ → ITodayStatsService, IActivityRepository, IReportExporter

    ## 重要: 接口未完成时用 Mock 数据

    ## 要创建的文件
    TrayApp/TrayIcon/TrayIconManager.cs
    TrayApp/Dashboard/{DashboardWindow.xaml, DashboardViewModel.cs}
    TrayApp/Dashboard/Controls/{TimelineControl,RealTimeStatsControl,SummaryCard,TimelineItem}.xaml
    TrayApp/ReportEditor/{ReportEditorWindow.xaml, ReportEditorViewModel.cs}
    TrayApp/History/{HistoryWindow.xaml, HistoryViewModel.cs}
    TrayApp/Settings/{SettingsWindow.xaml, SettingsViewModel.cs}

    ## 约束
    - MVVM(CommunityToolkit.Mvvm), Dispatcher 更新 UI
    - 中文注释`,
    { label: 'frontend', agentType: 'frontend-engineer', isolation: 'worktree' },
  ),

  // 角色D
  () => agent(
    `# 实现报表 R1-R5

    ## 读这些文件
    - doc/01-架构设计.md → 五(聚合表), 九(日报模板)
    - doc/00-需求文档.md → F9, F10
    - Core/Interfaces/ → IReportExporter

    ## 要创建的文件
    TrayApp/Exporters/{MarkdownExporter,DailyReportBuilder}.cs
    Data/Aggregation/{Daily,Weekly,Monthly}AggregationService.cs

    ## 一句话说清楚
    R1: 拼 Markdown(6 章节)
    R2: 拉数据 → 调统计 → 拼模型 → 导出
    R3: 每晚 00:05 按 app/project/domain 聚合
    R4: 每周一聚合 7 天
    R5: 每月 1 日聚合整月

    ## 约束
    - SQL 聚合，不加载全量到内存
    - Markdown 用 StringBuilder`,
    { label: 'report', agentType: 'report-engineer', isolation: 'worktree' },
  ),

  // 角色E
  () => agent(
    `# 编写测试 E1-E5

    ## 读这些文件
    - doc/05-测试用例.md → 所有 58 个用例的步骤和预期
    - doc/03-开发规范.md → 十(测试规范)
    - Core/Interfaces/, Core/Models/ → 了解接口和数据

    ## 框架: xUnit + FluentAssertions + NSubstitute + 内存 SQLite

    ## 文件结构
    Tests/DataTests/{SqliteContext,ActivityEventRepository,SettingsRepository}Tests.cs
    Tests/TrackerTests/{WindowTracker,IdleDetector,CrashRecoveryService,ActivityEngine}Tests.cs
    Tests/ClassificationTests/{BrowserTracker,FileTracker,ProjectDetector,ActivityCategorizer,KeywordExtractor,TodayStatsService}Tests.cs
    Tests/ReporterTests/{MarkdownExporter,DailyReportBuilder,AggregationService}Tests.cs

    ## 约束
    - 命名: [方法]_[场景]_[预期]
    - Mock Win32, 内存 SQLite`,
    { label: 'test', agentType: 'test-engineer', isolation: 'worktree' },
  ),
])

// 汇总
const roleNames = ['后端引擎', '内容分析', 'WPF前端', '数据报表', '测试']
let allPass = true
log('=== Wave 1 结果 ===')
wave1Results.forEach((r, i) => { allPass &&= !!r; log(`  ${r ? '✅' : '❌'} ${roleNames[i]}`) })

// ============================================================
// Phase 4: 集成
// ============================================================
phase('Integration')

log('=== 集成: 合并 worktree → 编译 → 测试 ===')

const mergeResult = await agent(
  `# 合并 worktree 分支到 main

  ## 步骤
  1. git branch | grep worktree → 列出所有 worktree 分支
  2. 按顺序 merge --squash 每个分支:
     core → engine → classification → report → frontend → test
  3. 每步 git commit + dotnet build 验证

  ## 注意
  各模块路径不重叠，预期无冲突`,
  { label: 'merge-worktrees' },
)

log(`合并: ${mergeResult ? '✅' : '❌'}`)

const testResult = await agent(
  `# 运行全量测试并报告

  1. dotnet test
  2. 统计: 通过/失败/跳过
  3. 失败用例按模块分类 + 错误信息
  4. 只报告，不修改代码`,
  { label: 'run-tests', agentType: 'test-engineer' },
)

log(`测试: ${testResult ? '✅' : '❌ 有失败'}`)

// 最终
log('===== 完成 =====')
log(`Wave 0: ${architectResult ? '✅' : '❌'}`)
log(`Wave 1: ${allPass ? '✅ 全部通过' : '⚠️ 部分失败'}`)
log(`集成: ${mergeResult && testResult ? '✅' : '⚠️ 需人工介入'}`)

return {
  status: allPass ? 'success' : 'partial',
  summary: {
    init: !!initResult,
    architect: !!architectResult,
    wave1: wave1Results.map((r, i) => ({ role: roleNames[i], success: !!r })),
    integration: !!mergeResult && !!testResult,
  },
}
