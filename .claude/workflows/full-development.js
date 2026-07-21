// ActivityMonitor 完整开发 Workflow v5
// 使用方式: Workflow({ scriptPath: '.claude/workflows/full-development.js' })
//
// 特性:
// - 自动写入 doc/09-会话检查点.md 记录每个阶段完成状态
// - 自动写入 doc/07-运行报错日志.md 记录失败详情
// - Phase 0: 项目骨架 -> 提交到 main
// - Phase 1: PM 审查
// - Phase 2: Wave 0 - 架构师
// - Phase 3: 合并架构师代码到 main
// - Phase 4: Wave 1 - 5 角色并行
// - Phase 5: 集成

export const meta = {
  name: 'full-development',
  description: '完整开发流程：Init -> Review -> Wave 0 -> Merge Architect -> Wave 1(并行) -> Integration',
  phases: [
    { title: 'Init', detail: '创建 .sln + .csproj 项目骨架并提交到 main' },
    { title: 'Review', detail: 'pm-assistant 审查开发计划' },
    { title: 'Wave 0', detail: '技术架构师完成 M1-M4 公共契约' },
    { title: 'Merge Architect', detail: '合并架构师代码到 main，供 Wave 1 使用' },
    { title: 'Wave 1', detail: '5 个角色 worktree 隔离并行开发' },
    { title: 'Integration', detail: '合并 worktree + 编译 + 全量测试' },
  ],
}

// ============================================================
// 辅助函数：更新会话检查点
// ============================================================
async function updateCheckpoint(phase, status, details) {
  await agent(
    `# 更新 doc/09-会话检查点.md

    1. 读取 doc/09-会话检查点.md
    2. 更新以下内容：
       - 将最后更新日期改为今天的日期
       - 在"任务完成情况"表中添加一行：
         | ${phase} | ${status} | ${details} |
       - 更新"当前代码状态"部分，反映最新进展
       - 更新"下一步（中断后接续点）"部分，指向未完成的阶段
    3. 写回 doc/09-会话检查点.md
    4. 保持原有 emoji 风格和 markdown 格式不变`,
    { label: 'checkpoint-' + phase.toLowerCase().replace(/\s+/g, '-') },
  )
}

// ============================================================
// 辅助函数：记录错误到报错日志
// ============================================================
async function logError(phase, errorMsg) {
  await agent(
    `# 追加报错到 doc/07-运行报错日志.md

    1. 读取 doc/07-运行报错日志.md
    2. 在"报错记录"区顶部插入一条新记录，格式要与 doc/07 的模板完全一致（包含所有字段）：

    ### [YYYY-MM-DD] ${phase} 失败

    **错误现象：**
    - Workflow 阶段 "${phase}" 执行失败
    - 错误详情：${errorMsg}

    **根因分析：**
    参见 workflow 运行日志

    **解决方案：**
    待排查。可能是编译错误、接口不一致或网络问题。

    **预防措施：**
    待补充

    **涉及文件：** 待定

    3. 同时在"快速索引"表第一行插入一条记录：
    | YYYY-MM-DD | ${phase} 失败 | workflow | 待修复 |
    4. 写回 doc/07-运行报错日志.md
    5. 日期的 YYYY-MM-DD 替换为今天的日期`,
    { label: 'error-' + phase.toLowerCase().replace(/\s+/g, '-') },
  )
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
  | ActivityMonitor.Core | classlib(net8.0-windows) | - | - |
  | ActivityMonitor.Data | classlib(net8.0-windows) | Core | Microsoft.Data.Sqlite |
  | ActivityMonitor.TrayApp | wpf(net8.0-windows) | Core, Data | CommunityToolkit.Mvvm |
  | ActivityMonitor.Tests | xunit(net8.0-windows) | Core, Data | xunit,FluentAssertions,NSubstitute |

  ## 目录结构
  src/
    ActivityMonitor.Core/: Models/, Interfaces/, Tracking/, Classification/, Win32/
    ActivityMonitor.Data/: Database/, Repositories/, Aggregation/
    ActivityMonitor.TrayApp/: TrayIcon/, Dashboard/, Dashboard/Controls/, ReportEditor/, History/, Settings/, Exporters/
  tests/
    ActivityMonitor.Tests/: DataTests/, TrackerTests/, ClassificationTests/, ReporterTests/, PerformanceTests/

  ## 全局配置
  - Directory.Build.props: Nullable enable, ImplicitUsings enable
  - 子目录放 .gitkeep

  ## 验收
  dotnet build 通过

  ## 提交到 main（关键！否则后续 worktree 看不到骨架文件）
  git add -A
  git commit -m "chore: project skeleton with 4 projects"
  git push`,
  { label: 'project-init' },
)

log(`Init: ${initResult ? 'OK' : 'FAIL'}`)

if (!initResult) {
  log('FAIL: init failed, recording error and aborting')
  await logError('Phase 0 Init', 'dotnet new 或 dotnet build 失败，项目骨架未创建')
  return { status: 'init-failed' }
}

// ============================================================
// Phase 1: 计划审查
// ============================================================
phase('Review')

log('=== PM Assistant 审查 ===')

const review = await agent(
  `审查多 agent 并行开发计划：

  Wave 0: 技术架构师 -> M1-M4（数据模型/接口/SQLite/Win32）
  Wave 1（并行）:
    - 后端引擎 -> Core/Tracking/ 5 模块
    - 内容分析 -> Core/Tracking+Classification/ 6 模块
    - WPF 前端 -> TrayApp/ 5 UI 模块（Mock 先行）
    - 数据报表 -> Data/Aggregation+TrayApp/Exporters/ 5 模块
    - 测试 -> Tests/ 全部用例
  Phase 4: 合并 worktree -> build -> test

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

log(`=== Review: ${review.recommendation} ===`)
log(review.summary)
for (const issue of review.issues || []) {
  log(`[${issue.severity}] ${issue.description}`)
  if (issue.suggestion) log(`  Suggestion: ${issue.suggestion}`)
}
if (review.newRolesNeeded?.length) log(`New roles needed: ${review.newRolesNeeded.join(', ')}`)

if (review.recommendation === 'reject') {
  log('Plan rejected, aborting')
  await logError('Phase 1 Review', 'PM Assistant 驳回了开发计划')
  return { status: 'rejected', review }
}

// ============================================================
// Phase 2: Wave 0 - 技术架构师
// ============================================================
phase('Wave 0')

log('=== Wave 0: 架构师 M1-M4（预计 ~30min）===')

const architectResult = await agent(
  `# 实现 M1-M4 公共契约

  ## 前置阅读
  - doc/01-架构设计.md -> 五(数据模型)、四(追踪模块)、十一(隐私)
  - doc/00-需求文档.md -> 三(功能需求全貌)
  - doc/03-开发规范.md -> 八(Win32 API)、九(SQLite)

  ## 产出清单

  **M1** src/ActivityMonitor.Core/Models/{ActivityEvent,DailySummary,WeeklySummary,MonthlySummary}.cs
  **M2** src/ActivityMonitor.Core/Interfaces/{IActivityTracker,IActivityRepository,IActivityCategorizer,IIdleDetector,ISleepDetector,ITodayStatsService,IReportExporter,ISettingsRepository}.cs
  **M3** src/ActivityMonitor.Data/Database/SqliteContext.cs + Repositories/ 下 5 个实现
  **M4** src/ActivityMonitor.Core/Win32/NativeMethods.cs

  ## 关键约束
  - 接口全 XML 注释
  - Repository 参数化 SQL
  - Win32 声明标注 DLL + ExactSpelling
  - dotnet build 编译通过无 warning`,
  { label: 'architect', agentType: 'architect', isolation: 'worktree' },
)

log(`Wave 0: ${architectResult ? 'OK' : 'FAIL'}`)

if (!architectResult) {
  log('Wave 0 failed, aborting')
  await logError('Phase 2 Wave 0', '架构师 M1-M4 实现失败，编译不通过')
  return { status: 'architect-failed' }
}

// ============================================================
// Phase 2.5: 合并架构师代码到 main
// ============================================================
phase('Merge Architect')

log('=== 合并架构师 worktree 到 main ===')

const mergeArchitect = await agent(
  `# 合并架构师 worktree 到 main

  1. git worktree list
  2. 从输出中找到架构师的分支名（非 main 的那个）
  3. git checkout main
  4. git merge --squash <架构师分支名>
  5. git commit -m "chore: merge architect M1-M4 to main"
  6. dotnet build 验证编译通过`,
  { label: 'merge-architect' },
)

log(`Merge architect: ${mergeArchitect ? 'OK' : 'FAIL'}`)

if (!mergeArchitect) {
  log('Merge failed, aborting')
  await logError('Phase 2.5 Merge Architect', '架构师 worktree 合并到 main 失败')
  return { status: 'merge-architect-failed' }
}

// 检查点：架构师阶段完成
await updateCheckpoint(
  'Wave 0 (M1-M4)',
  'OK',
  '架构师完成 Interfaces/Models/SQLite/Win32，已合入 main'
)

// ============================================================
// Phase 3: Wave 1 - 5 角色并行
// ============================================================
phase('Wave 1')

log('=== Wave 1 并行开发 ===')
log('架构师接口已合入 main，5 角色各在独立 worktree 中工作')

const wave1Results = await parallel([

  // 后端引擎
  () => agent(
    `# 实现追踪引擎 T1-T5

    ## 读这些文件
    - doc/01-架构设计.md -> 4.1(WinTracker), 4.4(Idle), 4.5(Sleep), 4.6(Crash), 4.8(性能)
    - doc/00-需求文档.md -> F1, F4, F5
    - src/ActivityMonitor.Core/Interfaces/ -> IActivityTracker, IIdleDetector, ISleepDetector

    ## 要创建的文件
    src/ActivityMonitor.Core/Tracking/{WindowTracker,IdleDetector,SleepDetector,CrashRecoveryService,ActivityEngine}.cs

    ## 约束
    - Timer 非 while(true), 线程 BelowNormal
    - 空闲降频 10s, 异常 catch 不崩溃
    - 空闲 15min 阈值，恢复延续 is_continued=true`,
    { label: 'engine', agentType: 'backend-engineer', isolation: 'worktree' },
  ),

  // 内容分析
  () => agent(
    `# 实现内容解析 C1-C6

    ## 读这些文件
    - doc/01-架构设计.md -> 4.2(Browser), 4.3(File), 4.7(Categorizer)
    - doc/00-需求文档.md -> F2, F3, F6
    - src/ActivityMonitor.Core/Interfaces/ -> IActivityCategorizer, ITodayStatsService

    ## 要创建的文件
    src/ActivityMonitor.Core/Tracking/{BrowserTracker,FileTracker}.cs
    src/ActivityMonitor.Core/Classification/{ProjectDetector,ActivityCategorizer,KeywordExtractor,TodayStatsService}.cs

    ## 一句话说清楚每个模块
    C1: 从窗口标题解析域名(Chrome/Edge/Firefox)
    C2: 从标题+进程名+工作目录推断文件和项目
    C3: 找.git或取文件夹名
    C4: 规则表分类+用户覆盖
    C5: 去停用词提取关键词
    C6: 按app/project/domain/work_tag聚合当天`,
    { label: 'classification', agentType: 'content-analyst', isolation: 'worktree' },
  ),

  // WPF 前端
  () => agent(
    `# 实现 WPF 界面 U1-U5

    ## 读这些文件
    - doc/01-架构设计.md -> 三(架构图), 九(日报模板)
    - doc/00-需求文档.md -> F7,F8,F9,F11,F12
    - src/ActivityMonitor.Core/Interfaces/ -> ITodayStatsService, IActivityRepository, IReportExporter

    ## 重要: 接口未完成时用 Mock 数据

    ## 要创建的文件
    src/ActivityMonitor.TrayApp/TrayIcon/TrayIconManager.cs
    src/ActivityMonitor.TrayApp/Dashboard/{DashboardWindow.xaml, DashboardViewModel.cs}
    src/ActivityMonitor.TrayApp/Dashboard/Controls/{TimelineControl,RealTimeStatsControl,SummaryCard,TimelineItem}.xaml
    src/ActivityMonitor.TrayApp/ReportEditor/{ReportEditorWindow.xaml, ReportEditorViewModel.cs}
    src/ActivityMonitor.TrayApp/History/{HistoryWindow.xaml, HistoryViewModel.cs}
    src/ActivityMonitor.TrayApp/Settings/{SettingsWindow.xaml, SettingsViewModel.cs}

    ## 约束
    - MVVM(CommunityToolkit.Mvvm), Dispatcher 更新 UI
    - 中文注释`,
    { label: 'frontend', agentType: 'frontend-engineer', isolation: 'worktree' },
  ),

  // 数据报表
  () => agent(
    `# 实现报表 R1-R5

    ## 读这些文件
    - doc/01-架构设计.md -> 五(聚合表), 九(日报模板)
    - doc/00-需求文档.md -> F9, F10
    - src/ActivityMonitor.Core/Interfaces/ -> IReportExporter

    ## 要创建的文件
    src/ActivityMonitor.TrayApp/Exporters/{MarkdownExporter,DailyReportBuilder}.cs
    src/ActivityMonitor.Data/Aggregation/{Daily,Weekly,Monthly}AggregationService.cs

    ## 一句话说清楚
    R1: 拼 Markdown(6 章节)
    R2: 拉数据 -> 调统计 -> 拼模型 -> 导出
    R3: 每晚 00:05 按 app/project/domain 聚合
    R4: 每周一聚合 7 天
    R5: 每月 1 日聚合整月

    ## 约束
    - SQL 聚合，不加载全量到内存
    - Markdown 用 StringBuilder`,
    { label: 'report', agentType: 'report-engineer', isolation: 'worktree' },
  ),

  // 测试
  () => agent(
    `# 编写测试 E1-E5

    ## 读这些文件
    - doc/05-测试用例.md -> 所有 58 个用例的步骤和预期
    - doc/03-开发规范.md -> 十(测试规范)
    - src/ActivityMonitor.Core/Interfaces/, src/ActivityMonitor.Core/Models/

    ## 框架: xUnit + FluentAssertions + NSubstitute + 内存 SQLite

    ## 文件结构
    tests/ActivityMonitor.Tests/DataTests/{SqliteContext,ActivityEventRepository,SettingsRepository}Tests.cs
    tests/ActivityMonitor.Tests/TrackerTests/{WindowTracker,IdleDetector,CrashRecoveryService,ActivityEngine}Tests.cs
    tests/ActivityMonitor.Tests/ClassificationTests/{BrowserTracker,FileTracker,ProjectDetector,ActivityCategorizer,KeywordExtractor,TodayStatsService}Tests.cs
    tests/ActivityMonitor.Tests/ReporterTests/{MarkdownExporter,DailyReportBuilder,AggregationService}Tests.cs

    ## 约束
    - 命名: [方法]_[场景]_[预期]
    - Mock Win32, 内存 SQLite`,
    { label: 'test', agentType: 'test-engineer', isolation: 'worktree' },
  ),
])

// Wave 1 汇总
const roleNames = ['engine', 'classification', 'frontend', 'report', 'test']
let allPass = true
const roleStatus = []
wave1Results.forEach((r, i) => {
  const ok = !!r
  allPass &&= ok
  roleStatus.push({ role: roleNames[i], success: ok })
  log(`  ${ok ? 'OK' : 'FAIL'} ${roleNames[i]}`)
})

// 日志：记录失败的角色
const failedRoles = roleStatus.filter(r => !r.success)
if (failedRoles.length > 0) {
  const failedNames = failedRoles.map(r => r.role).join(', ')
  await logError('Phase 3 Wave 1', '以下角色失败: ' + failedNames)
}

// 更新检查点
await updateCheckpoint(
  'Wave 1 (5 Role Parallel)',
  allPass ? 'OK' : 'Partially Failed',
  allPass
    ? 'All 5 roles implemented: engine, classification, frontend, report, test'
    : 'Failed roles: ' + failedRoles.map(r => r.role).join(', ')
)

// guard: Wave 1 任一角色失败则终止
if (!allPass) {
  log('Wave 1 partial failure, aborting (merge would fail)')
  return { status: 'wave1-partial-failure' }
}

// ============================================================
// Phase 4: 集成
// 注意：architect 分支已在 Phase 2.5 合入 main，此处不重复合并
// ============================================================
phase('Integration')

log('=== 集成: 合并 worktree -> 编译 -> 测试 ===')

const mergeResult = await agent(
  `# 合并 worktree 分支到 main

  ## 步骤
  1. git worktree list
  2. 按顺序 merge --squash 每个分支到 main:
     engine -> classification -> report -> frontend -> test
  3. 每步: git merge --squash <分支> && git commit -m "merge <role> worktree" && dotnet build

  ## 注意
  - architect 分支已在早期阶段合并，不要再次合并
  - 各模块路径不重叠，预期无冲突`,
  { label: 'merge-worktrees' },
)

log(`Merge all worktrees: ${mergeResult ? 'OK' : 'FAIL'}`)

if (!mergeResult) {
  log('Merge failed, logging error')
  await logError('Phase 4 Integration', '合并 worktree 到 main 失败，存在冲突或编译错误')
  return { status: 'merge-failed' }
}

const testResult = await agent(
  `# 运行全量测试并报告

  1. dotnet test
  2. 统计: 通过/失败/跳过
  3. 失败用例按模块分类 + 错误信息
  4. 只报告，不修改代码`,
  { label: 'run-tests', agentType: 'test-engineer' },
)

log(`Tests: ${testResult ? 'OK' : 'FAIL'}`)

if (!testResult) {
  await logError('Phase 4 Integration', 'dotnet test 有失败用例')
}

// ============================================================
// 最终检查点更新
// ============================================================
const finalStatus = allPass && mergeResult && testResult ? 'success' : 'partial'
await updateCheckpoint(
  'Full Development Pipeline',
  finalStatus === 'success' ? 'OK' : 'Partial',
  finalStatus === 'success'
    ? 'All phases complete: skeleton -> review -> architect -> 5 roles -> integration tests passed'
    : 'Some phases failed, see doc/07 for details'
)

// 最终
log('===== Complete =====')
log(`Overall: ${finalStatus === 'success' ? 'ALL OK' : 'PARTIAL'}`)
log(`  Init: ${initResult ? 'OK' : 'FAIL'}`)
log(`  Review: ${review.recommendation}`)
log(`  Wave 0: ${architectResult ? 'OK' : 'FAIL'}`)
log(`  Merge Architect: ${mergeArchitect ? 'OK' : 'FAIL'}`)
log(`  Wave 1: ${roleStatus.map(r => r.role + '=' + (r.success ? 'OK' : 'FAIL')).join(', ')}`)
log(`  Integration: ${mergeResult && testResult ? 'OK' : 'PARTIAL'}`)

return {
  status: finalStatus,
  summary: {
    init: !!initResult,
    review: review.recommendation,
    architect: !!architectResult,
    mergeArchitect: !!mergeArchitect,
    wave1: roleStatus,
    integration: !!mergeResult && !!testResult,
  },
}
