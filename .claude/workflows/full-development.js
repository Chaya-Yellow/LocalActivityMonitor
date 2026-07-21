// ActivityMonitor 完整开发 Workflow v2
// 使用方式: Workflow({ scriptPath: '.claude/workflows/full-development.js' })
//
// Phase 0: 项目初始化（创建 .sln 和 .csproj 骨架）
// Phase 1: PM Assistant 审查开发计划
// Phase 2: Wave 0 — 架构师完成 M1-M4
// Phase 3: Wave 1 — 5 个角色 worktree 隔离并行
// Phase 4: 集成（合并 worktree → 编译 → 全量测试）

export const meta = {
  name: 'full-development',
  description: '完整开发流程：项目初始化 → 需求审查 → Wave 0 → Wave 1 并行(5角色) → 集成测试',
  phases: [
    { title: 'Init', detail: '创建 .sln + .csproj 项目骨架' },
    { title: 'Review', detail: 'pm-assistant 审查开发计划' },
    { title: 'Wave 0', detail: '技术架构师完成 M1-M4 公共契约' },
    { title: 'Wave 1', detail: '5 个角色 worktree 隔离并行' },
    { title: 'Integration', detail: '合并 worktree + 编译 + 全量测试' },
  ],
}

// ============================================================
// Phase 0: 项目初始化
// ============================================================
phase('Init')

log('=== 创建 .NET 项目骨架 ===')

// 这里不做 dotnet CLI（可能未安装），直接创建项目文件结构和解决方案
// 后续 agent 在各自 worktree 中创建 csproj 并引用

const initResult = await agent(
  `# 任务: 创建 ActivityMonitor 项目骨架

  你需要在 CWD 下创建完整的 .NET 8 解决方案结构和项目文件。
  不要写任何业务逻辑代码，只建骨架。

  ## 创建步骤

  ### 1. 解决方案
  - \`ActivityMonitor.sln\`

  ### 2. 项目目录和 .csproj 文件

  ##### ActivityMonitor.Core/
  \`ActivityMonitor.Core.csproj\` — 类库
  - TargetFramework: net8.0-windows
  - 子目录: Models/, Interfaces/, Tracking/, Classification/, Win32/

  ##### ActivityMonitor.Data/
  \`ActivityMonitor.Data.csproj\` — 类库
  - TargetFramework: net8.0-windows
  - NuGet: Microsoft.Data.Sqlite
  - 项目引用: ActivityMonitor.Core
  - 子目录: Database/, Repositories/, Aggregation/

  ##### ActivityMonitor.TrayApp/
  \`ActivityMonitor.TrayApp.csproj\` — WPF 应用
  - TargetFramework: net8.0-windows
  - OutputType: WinExe
  - UseWPF: true
  - NuGet: CommunityToolkit.Mvvm
  - 项目引用: ActivityMonitor.Core, ActivityMonitor.Data
  - 子目录: TrayIcon/, Dashboard/Controls/, ReportEditor/, History/, Settings/, Exporters/

  ##### ActivityMonitor.Tests/
  \`ActivityMonitor.Tests.csproj\` — 测试项目
  - TargetFramework: net8.0-windows
  - NuGet: xunit, xunit.runner.visualstudio, FluentAssertions, NSubstitute, Microsoft.NET.Test.Sdk
  - 项目引用: ActivityMonitor.Core, ActivityMonitor.Data
  - 子目录: DataTests/, TrackerTests/, ClassificationTests/, ReporterTests/, PerformanceTests/

  ### 3. 解决方案引用
  将 4 个项目添加到 ActivityMonitor.sln

  ### 4. 全局配置
  - \`Directory.Build.props\`: Nullable enable, ImplicitUsings enable
  - \`GlobalUsings.cs\`: 在 Core 项目中添加常用 using

  ### 5. 验证
  运行 \`dotnet build\` 确保编译通过

  ## 约束
  - 只创建骨架，无业务逻辑代码
  - .csproj 使用 SDK 风格（短格式）
  - 每个目录放一个 placeholder \`.gitkeep\` 文件`,
  { label: 'project-init' },
)

log(`项目初始化: ${initResult ? '✅ 完成' : '❌ 失败'}`)

// ============================================================
// Phase 1: 计划审查
// ============================================================
phase('Review')

log('=== PM Assistant 审查开发计划 ===')

const review = await agent(
  `审查以下多 agent 并行开发计划是否有遗漏或矛盾。

  ## 计划概要

  Wave 0（技术架构师）：实现 M1-M4（数据模型、接口契约、SQLite 层、Win32 声明）
  Wave 1（5 角色并行，各 worktree 隔离）：
    - 后端引擎工程师 → Core/Tracking/ 下 5 个模块
    - 内容分析工程师 → Core/Tracking/ + Core/Classification/ 下 6 个模块
    - WPF 前端工程师 → TrayApp/ 下 5 个 UI 模块（可用 Mock）
    - 数据报表工程师 → Data/Aggregation/ + TrayApp/Exporters/ 下 5 个模块
    - 测试工程师 → Tests/ 下全部测试用例
  Phase 4 集成：合并 worktree → dotnet build → dotnet test

  ## 审查维度

  1. 完整性——角色是否覆盖了全部开发工作？
  2. 依赖冲突——Wave 1 之间有文件重叠吗？
  3. 边界——如果某个 agent 失败，容错机制？
  4. 新角色——需要新增 agent 类型吗？

  输出审查报告（含总结、问题列表、推荐操作）。`,
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

log('=== 审查报告 ===')
log(`结论: ${review.recommendation}`)
log(`摘要: ${review.summary}`)

for (const issue of review.issues || []) {
  log(`[${issue.severity}] ${issue.description}`)
  if (issue.suggestion) log(`  建议: ${issue.suggestion}`)
}

if (review.newRolesNeeded && review.newRolesNeeded.length > 0) {
  log(`需新增角色: ${review.newRolesNeeded.join(', ')}`)
}

if (review.recommendation === 'reject') {
  log('❌ 计划被驳回，请修改后再执行')
  return { status: 'rejected', review }
}

if (review.recommendation === 'revise') {
  log('⚠️ 计划需修正，已采纳审查建议。进入修正流程')
  // 如果有新角色需求，在这里处理
}

// ============================================================
// Phase 2: Wave 0 — 技术架构师
// ============================================================
phase('Wave 0')

log('=== Wave 0: 技术架构师 — M1-M4 公共契约 ===')
log('预计耗时: 约 2 天（串行）')

const architectResult = await agent(
  `# 任务: 实现 ActivityMonitor Wave 0 公共契约

  ## 前置

  项目骨架已创建。先 \`Read\` 以下文档了解全局：

  1. \`doc/01-架构设计.md\` — 重点读「五、数据模型」和「四、核心追踪模块」
  2. \`doc/00-需求文档.md\` — 了解功能需求全貌
  3. \`doc/03-开发规范.md\` — 编码约定

  ## M1 数据模型（Core/Models/）

  在 Core/Models/ 下创建 4 个 POCO 类：
  \`\`\`
  ActivityEvent — 核心事件
    Id(int), StartTime(DateTime), EndTime(DateTime?), DurationMs(long),
    Category(string: web/file/app/idle/sleep), WorkTag(string: work/break/personal/unknown),
    SubCategory(string?), WindowTitle(string?), ProcessName(string?),
    ProcessPath(string?), ProcessId(int), Detail(string?), Domain(string?),
    Project(string?), Keywords(string?), IsContinued(bool), IsPrivate(bool),
    IsCrashRecovered(bool), EditedTitle(string?), EditedDesc(string?),
    UserCategory(string?)

  DailySummary — 每日聚合
    Date(string), TotalActiveMs(long), TotalIdleMs(long), TotalSleepMs(long),
    AppBreakdown(string/JSON), DomainBreakdown(string/JSON),
    ProjectBreakdown(string/JSON), WorkMs(long), BreakMs(long),
    KeywordCloud(string/JSON), RawReport(string?), UserNotes(string?)

  WeeklySummary / MonthlySummary 见架构设计文档
  \`\`\`

  ## M2 接口（Core/Interfaces/）

  创建以下接口，全部带 XML 注释：
  - IActivityTracker: Start/Stop/Pause/Resume + event OnActivityChanged
  - IActivityRepository: InsertAsync/InsertBatchAsync/GetTodayEventsAsync/GetByDateAsync/UpdateAsync/DeleteAsync/GetDailyStatsAsync
  - IActivityCategorizer: Classify(ActivityEvent) → (Category, WorkTag)
  - IIdleDetector: IsIdle, IdleThreshold + event OnIdleStateChanged
  - ISleepDetector: IsSleeping + event OnSleepStateChanged
  - ITodayStatsService: GetByAppAsync/GetByProjectAsync/GetByDomainAsync/GetByCategoryAsync/GetByWorkTagAsync
  - IReportExporter: ExportDailyAsync(DateTime) → string
  - ISettingsRepository: GetAsync(string key), SetAsync(string key, string value)

  ## M3 SQLite 数据层（Data/）

  Data/Database/SqliteContext.cs — 初始化 + 建表 + 索引
  Data/Repositories/ 下 5 个 Repository 实现上述接口
  - 全部使用参数化 SQL
  - 索引: idx_events_date, idx_events_cat, idx_events_proc

  ## M4 Win32（Core/Win32/NativeMethods.cs）

  P/Invoke 声明（全部 static extern）:
  - GetForegroundWindow() → IntPtr
  - GetWindowThreadProcessId(IntPtr, out uint) → int
  - GetWindowText(IntPtr, StringBuilder, int) → int
  - GetLastInputInfo(ref LASTINPUTINFO) → bool
  - RegisterPowerSettingNotification(IntPtr, ref Guid, uint) → IntPtr

  ## 验收标准

  \`dotnet build\` 编译通过，无 warning（除 XML 注释警告外）`,
  { label: 'architect', agentType: 'architect', isolation: 'worktree' },
)

log(`Wave 0: ${architectResult ? '✅ M1-M4 已完成' : '❌ 失败'}`)

// ============================================================
// Phase 3: Wave 1 — 5 角色并行
// ============================================================
phase('Wave 1')

log('=== 启动 Wave 1 并行开发（5 角色） ===')
log('预计等待时间: 最慢的角色（WPF 前端 ~3.5 天）决定总时长')
log('各角色在独立 worktree 中工作，互不干扰')

const wave1Results = await parallel([

  // ---- 角色A: 后端引擎工程师 ----
  () => agent(
    `# 任务: 实现追踪引擎（T1-T5）

    ## 前置阅读
    先 Read 以下文件了解你的模块要求：
    - \`doc/01-架构设计.md\` — 4.1, 4.4, 4.5, 4.6, 4.8（引擎相关章节）
    - \`doc/00-需求文档.md\` — F1, F4, F5（需求描述）
    - \`doc/03-开发规范.md\` — 四、十三（编码和性能要求）
    - 然后读 \`Core/Interfaces/\` 下 IActivityTracker, IIdleDetector, ISleepDetector

    ## 你的模块清单

    T1 WindowTracker — Core/Tracking/WindowTracker.cs
    → 2s 定时器轮询 GetForegroundWindow → GetWindowText → GetWindowThreadProcessId
    → 窗口不变累加时长，切换则结束旧创建新
    → 排除 IntPtr.Zero 和无窗口进程

    T2 IdleDetector — Core/Tracking/IdleDetector.cs
    → GetLastInputInfo + 15 分钟阈值
    → 触发/恢复事件通知

    T3 SleepDetector — Core/Tracking/SleepDetector.cs
    → WM_POWERBROADCAST 电源事件监听

    T4 CrashRecoveryService — Core/Tracking/CrashRecoveryService.cs
    → 退出标记文件 + 5 分钟窗口补录

    T5 ActivityEngine — Core/Tracking/ActivityEngine.cs
    → 组装 T1-T4: Start/Pause/Resume/Stop
    → 空闲恢复延续上一条记录(is_continued=true)
    → 每个 tick 的异常必须 catch 不崩溃

    ## 约束
    - System.Timers.Timer，禁止 while(true)
    - 线程 BelowNormal
    - 空闲时轮询频率降到 10s`,
    {
      label: 'engine',
      agentType: 'backend-engineer',
      isolation: 'worktree',
    },
  ),

  // ---- 角色B: 内容分析工程师 ----
  () => agent(
    `# 任务: 实现内容解析与分类（C1-C6）

    ## 前置阅读
    - \`doc/01-架构设计.md\` — 4.2, 4.3, 4.7（浏览器/文件/分类器）
    - \`doc/00-需求文档.md\` — F2, F3, F6
    - 然后读 \`Core/Interfaces/\` 下 IActivityCategorizer, ITodayStatsService
    - 读 Core/Win32/NativeMethods.cs 了解可用 Win32 函数

    ## 你的模块清单

    C1 BrowserTracker — Core/Tracking/BrowserTracker.cs
    → Chrome: "标题 - Google Chrome" | Edge: " - Microsoft Edge" | Firefox: " - Mozilla Firefox"
    → 返回 {Title, Domain}，非浏览器返回 null

    C2 FileTracker — Core/Tracking/FileTracker.cs
    → VS Code → 提取文件名+项目名
    → PS → 标题只有文件名，通过进程工作目录补全路径
    → 终端 → "cmd.exe - C:\\path" 提取目录
    → 远程桌面 → 记录目标地址

    C3 ProjectDetector — Core/Classification/ProjectDetector.cs
    → 向上找 .git，取仓库名
    → 无 .git 取最后两级文件夹
    → 空路径返回 "unknown"

    C4 ActivityCategorizer — Core/Classification/ActivityCategorizer.cs
    → 实现 IActivityCategorizer
    → 规则: chrome/msedge/firefox → Web | code/ps/notepad++/winword → File
    | mstsc/todesk → App + remote | 其余 → App
    → user_category 有值优先使用

    C5 KeywordExtractor — Core/Classification/KeywordExtractor.cs
    → 去停用词(的/了/是/在/就/都/也/很/有/和/与/及/或)
    → 去标点，保留中英文数字
    → 空标题返回 []

    C6 TodayStatsService — Core/Classification/TodayStatsService.cs
    → 实现 ITodayStatsService
    → 按 process_name/project/domain/work_tag 分组 SUM(duration_ms)
    → 返回带占比数据`,
    {
      label: 'classification',
      agentType: 'content-analyst',
      isolation: 'worktree',
    },
  ),

  // ---- 角色C: WPF 前端工程师 ----
  () => agent(
    `# 任务: 实现 WPF 界面（U1-U5）

    ## 前置阅读
    - \`doc/01-架构设计.md\` — 三（架构图看 UI 层位置）、九（日报模板）
    - \`doc/00-需求文档.md\` — F7, F8, F9, F11, F12
    - \`doc/03-开发规范.md\` — 七.3（WPF 线程要求）
    - 读 \`Core/Interfaces/\` 下 ITodayStatsService, IActivityRepository, IReportExporter, ISettingsRepository

    ## 重要说明
    后端接口可能尚未完成。先 Read 接口定义了解方法签名，
    然后用硬编码 Mock 数据实现 UI，等后端完成后切换为真实调用。

    ## 你的模块清单

    U1 TrayIconManager — TrayApp/TrayIcon/TrayIconManager.cs
    → NotifyIcon: 运行/暂停/空闲三种状态
    → Tooltip: "今日已记录 X 小时"
    → 右键: 打开面板/暂停/恢复/退出
    → 左键: 打开 Dashboard

    U2 DashboardWindow — TrayApp/Dashboard/
    → TimelineControl: 时间线列表(时间+应用+标题+时长)
    → RealTimeStatsControl: 4 Tab(按软件/项目/域名/类别)
    → SummaryCard: 总时长/工作/空闲/占比
    → 内联编辑选中记录

    U3 ReportEditor — TrayApp/ReportEditor/
    → Markdown 预览 + 编辑/删除/合并/插入线下活动 + 导出 .md

    U4 HistoryWindow — TrayApp/History/
    → DatePicker + 历史时间线 + 搜索

    U5 SettingsWindow — TrayApp/Settings/
    → 保留策略(下拉) + 开机自启(Toggle) + 空闲阈值(NumericUpDown)

    ## 要求
    - MVVM 模式(CommunityToolkit.Mvvm)
    - Dispatcher.Invoke 更新 UI 线程
    - 全部用中文注释`,
    {
      label: 'frontend',
      agentType: 'frontend-engineer',
      isolation: 'worktree',
    },
  ),

  // ---- 角色D: 数据报表工程师 ----
  () => agent(
    `# 任务: 实现报表与聚合服务（R1-R5）

    ## 前置阅读
    - \`doc/01-架构设计.md\` — 五（数据模型看聚合表结构）、九（日报模板）
    - \`doc/00-需求文档.md\` — F9, F10
    - 读 \`Core/Interfaces/\` 下 IReportExporter
    - 读 Data/Repositories/ 了解 Repository 方法

    ## 你的模块清单

    R1 MarkdownExporter — TrayApp/Exporters/MarkdownExporter.cs
    → 实现 IReportExporter
    → 输出 Markdown 包含 6 章节: 概览/时间线/项目分布/应用分布/网页分类/补充区
    → 模板见 doc/01-架构设计.md 第九章

    R2 DailyReportBuilder — TrayApp/Exporters/ReportBuilder.cs
    → 拉取当天数据 → 调用 TodayStatsService + KeywordExtractor
    → 构建 ReportData → 传给 MarkdownExporter

    R3 DailyAggregationService — Data/Aggregation/DailyAggregationService.cs
    → 每日 00:05 执行: group by process_name/domain/project/work_tag
    → 写入 daily_summaries

    R4 WeeklyAggregationService — Data/Aggregation/WeeklyAggregationService.cs
    → 每周一: 汇总 7 天 + 计算 avg_daily_hours

    R5 MonthlyAggregationService — Data/Aggregation/MonthlyAggregationService.cs
    → 每月 1 日: 汇总当月

    ## 约束
    - SQL 聚合查询，不加载全量到内存
    - Markdown 用 StringBuilder 拼装`,
    {
      label: 'report',
      agentType: 'report-engineer',
      isolation: 'worktree',
    },
  ),

  // ---- 角色E: 测试工程师 ----
  () => agent(
    `# 任务: 编写全部测试用例（E1-E5）

    ## 前置阅读
    - \`doc/05-测试用例.md\` — 全部 58 个测试用例的详细步骤和预期
    - \`doc/03-开发规范.md\` — 十（测试规范）
    - 读 Core/Interfaces/ 下所有接口
    - 读 Core/Models/ 下所有 Model
    - 读 Tests/ 项目结构

    ## 框架
    xUnit + FluentAssertions + NSubstitute
    数据层测试用内存 SQLite (DataSource=:memory:)

    ## 你的测试文件清单
    参见 doc/05-测试用例.md 的详细用例列表，共 58 个用例。

    按模块分文件:
    Tests/DataTests/
    ├── SqliteContextTests.cs
    ├── ActivityEventRepositoryTests.cs
    └── SettingsRepositoryTests.cs

    Tests/TrackerTests/
    ├── WindowTrackerTests.cs
    ├── IdleDetectorTests.cs
    ├── CrashRecoveryServiceTests.cs
    └── ActivityEngineTests.cs

    Tests/ClassificationTests/
    ├── BrowserTrackerTests.cs
    ├── FileTrackerTests.cs
    ├── ProjectDetectorTests.cs
    ├── ActivityCategorizerTests.cs
    ├── KeywordExtractorTests.cs
    └── TodayStatsServiceTests.cs

    Tests/ReporterTests/
    ├── MarkdownExporterTests.cs
    ├── DailyReportBuilderTests.cs
    └── AggregationServiceTests.cs

    ## 命名规范
    [方法名]_[场景]_[预期结果]

    ## 要求
    - Mock 替换 Win32 API
    - 内存 SQLite 做数据层测试`,
    {
      label: 'test',
      agentType: 'test-engineer',
      isolation: 'worktree',
    },
  ),
])

// 汇总
const roleNames = ['后端引擎', '内容分析', 'WPF前端', '数据报表', '测试']
log('=== Wave 1 结果 ===')
let allSucceeded = true
wave1Results.forEach((r, i) => {
  const status = r ? '✅' : '❌'
  if (!r) allSucceeded = false
  log(`  ${status} ${roleNames[i]}`)
})

// ============================================================
// Phase 4: 集成验证
// ============================================================
phase('Integration')

log('=== 开始集成: 合并 worktree + 编译 + 测试 ===')

// 逐个合并 worktree 分支到 main
const mergeResult = await agent(
  `# 任务: 合并所有 worktree 分支到 main

  ## 背景
  Wave 1 的 5 个角色分别在独立的 worktree 中完成了开发。
  每个 worktree 对应一个 git 分支，现在需要将它们合并回 main。

  ## 步骤

  1. 列出所有 worktree 分支: git branch | grep worktree
  2. 切换到 main 分支
  3. 逐个合并 worktree 分支:
     - git merge --squash worktree/[分支名]
     - git commit -m "merge: [角色名] 模块"
  4. 处理可能出现的冲突（路径不重叠则无冲突）

  ## 合并顺序
  1. 先合 core（架构师 Wave 0）
  2. 再合 engine、classification、report（无 UI 依赖）
  3. 然后合 frontend（底层已就绪）
  4. 最后合 test（需要所有业务代码就绪）

  ## 验证
  每合并一个分支就运行 dotnet build 确保编译通过`,
  { label: 'merge-worktrees' },
)

log(`合并 worktree: ${mergeResult ? '✅' : '❌'}`)

// 运行全量测试
const testResult = await agent(
  `# 任务: 运行全量测试并输出报告

  ## 步骤
  1. 运行 dotnet test --no-build
  2. 收集测试结果:
     - 总用例数 / 通过 / 失败 / 跳过
  3. 如果发现失败:
     - 输出失败用例的详细错误信息
     - 按模块归类: 数据层/引擎/分类/报表
     - 给出修复建议（不修改代码）

  ## 重要
  这是集成验证阶段，只报告问题，不修改生产代码。`,
  { label: 'run-tests', agentType: 'test-engineer' },
)

log(`全量测试: ${testResult ? '✅' : '❌ 存在失败用例'}`)

// 最终总结
log('===== 开发流程完成 =====')
log(`Wave 0: ${architectResult ? '✅' : '❌'}`)
log(`Wave 1: ${allSucceeded ? '✅ 全部完成' : '⚠️ 部分完成'}`)
log(`集成: ${mergeResult && testResult ? '✅' : '⚠️ 需人工介入'}`)

return {
  status: allSucceeded ? 'success' : 'partial',
  summary: {
    init: !!initResult,
    architect: !!architectResult,
    wave1: wave1Results.map((r, i) => ({ role: roleNames[i], success: !!r })),
    integration: !!mergeResult && !!testResult,
  },
}
