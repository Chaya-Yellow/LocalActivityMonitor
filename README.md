# LocalActivityMonitor — 本地活动监控器

> Windows 11 桌面活动追踪工具，自动记录前台窗口使用情况，一键导出工作日报。

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-net8.0--windows-512BD4)](https://learn.microsoft.com/zh-cn/dotnet/desktop/wpf/)
[![SQLite](https://img.shields.io/badge/SQLite-本地存储-003B57)](https://www.sqlite.org/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

---

## 功能

| 功能 | 说明 |
|------|------|
| 🔍 **全量追踪** | 记录所有前台 exe 活动（非白名单模式） |
| 🪟 **窗口解析** | 自动识别浏览器域名、文件路径、IDE 项目 |
| 🏷️ **智能分类** | 规则引擎自动归类为工作/非工作/网页/文件/应用 |
| 📊 **实时统计** | 按应用、项目、域名、分类实时展示时间分布 |
| 📝 **日报导出** | 一键生成 Markdown 日报（含时间线、分布图、项目耗时） |
| 🔌 **低资源占用** | 2 秒轮询，空闲降频，内存 < 50MB |
| 🛡️ **隐私优先** | 数据全量存储在本地 SQLite，不上传网络 |
| 🔄 **崩溃恢复** | 5 分钟窗口自动补录，睡眠/唤醒自动检测 |

---

## 快速开始

### 前置要求

- Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### 运行

```powershell
git clone https://github.com/Chaya-Yellow/LocalActivityMonitor.git
cd LocalActivityMonitor
dotnet run --project src\ActivityMonitor.TrayApp
```

或直接双击:

```
src\ActivityMonitor.TrayApp\bin\Debug\net8.0-windows\ActivityMonitor.TrayApp.exe
```

### 首次使用

1. 启动后应用会最小化到**系统托盘**（通知区域）
2. 右键托盘图标 → **打开 Dashboard** 查看实时活动统计
3. 关闭 Dashboard 窗口不会退出程序，保持后台监控
4. 右键托盘图标 → **退出** 完全关闭

---

## 系统架构

```
Windows 11 前台
    │
    ▼
ActivityMonitor.TrayApp (WPF)
  ├── TrayIcon          ← 系统托盘（右键菜单）
  ├── Dashboard         ← 实时时间线 + 统计面板
  ├── ReportEditor      ← 日报编辑/导出为 Markdown
  ├── History           ← 历史活动浏览
  └── Settings          ← 开机自启 / 隐私模式
    │
    ▼
ActivityMonitor.Core
  ├── Tracking/         ← WindowTracker, IdleDetector, SleepDetector...
  ├── Classification/   ← ActivityCategorizer, KeywordExtractor...
  ├── Models/           ← ActivityEvent, DailySummary...
  ├── Interfaces/       ← IActivityTracker, IActivityRepository...
  └── Win32/            ← NativeMethods (P/Invoke)
    │
    ▼
ActivityMonitor.Data
  ├── SqliteContext     ← 建表/迁移（5 张表）
  └── Repositories/     ← CRUD + 聚合查询
```

---

## 技术栈

| 层 | 技术 |
|-----|--------|
| UI | WPF (.NET 8), CommunityToolkit.Mvvm |
| 追踪 | Win32 P/Invoke (GetForegroundWindow, GetLastInputInfo) |
| 存储 | SQLite (Microsoft.Data.Sqlite) |
| 分类 | 规则引擎（非 ML），中文停用词 |
| 测试 | xUnit, FluentAssertions, NSubstitute |
| 导出 | Markdown (StringBuilder) |

---

## 项目结构

```
ActivityMonitor/
├── src/
│   ├── ActivityMonitor.Core/       ← 核心逻辑
│   │   ├── Models/                 ← 数据模型
│   │   ├── Interfaces/             ← 接口契约
│   │   ├── Tracking/               ← 窗口追踪/空闲/休眠/崩溃恢复
│   │   ├── Classification/         ← 分类引擎/关键词提取
│   │   └── Win32/                  ← P/Invoke 声明
│   ├── ActivityMonitor.Data/       ← 数据层
│   │   ├── Database/               ← SQLite 初始化
│   │   ├── Repositories/           ← CRUD 实现
│   │   └── Aggregation/            ← 日/周/月聚合
│   └── ActivityMonitor.TrayApp/    ← WPF 桌面应用
│       ├── TrayIcon/               ← 系统托盘
│       ├── Dashboard/              ← 主面板
│       ├── ReportEditor/           ← 日报编辑
│       ├── History/                ← 历史浏览
│       ├── Settings/               ← 设置
│       ├── Exporters/              ← Markdown 导出
│       └── Mock/                   ← 开发用 Mock 实现
├── tests/
│   └── ActivityMonitor.Tests/      ← 183 个单元测试
├── doc/
│   ├── 00-需求文档.md              ← 需求规格
│   ├── 01-架构设计.md              ← 技术方案
│   ├── 02-细节问题.md              ← 24 个需求问答
│   ├── 03-开发规范.md              ← 编码规范
│   ├── 04-拆分任务.md              ← 任务拆分
│   ├── 05-测试用例.md              ← 58 个测试用例
│   ├── 06-项目路线图.md            ← 交接文档
│   ├── 07-运行报错日志.md          ← 错误日志
│   ├── 08-待办建议-Backlog.md      ← 待办清单
│   └── 09-会话检查点.md            ← 进度记录
└── .claude/
    ├── agents/                     ← 多角色 agent 定义
    └── workflows/                  ← 自动化开发工作流
```

---

## 测试

```powershell
dotnet test
```

当前通过 **183 个测试**，覆盖数据层、追踪引擎、分类引擎、报表导出全链路。

---

## 构建

```powershell
dotnet build -c Release
```

发布到单文件：

```powershell
dotnet publish src\ActivityMonitor.TrayApp -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## 隐私声明

- ✅ 所有数据存储在本地 SQLite 数据库
- ✅ 不上传任何数据到网络
- ✅ 不设白名单，追踪所有前台活动
- ✅ 可随时清除全部数据（设置页面）
- ✅ 无后台网络请求

---

## 技术细节

| 特性 | 实现 |
|------|------|
| 轮询间隔 | 2 秒（活动）/ 10 秒（空闲） |
| 空闲阈值 | 15 分钟无操作 |
| 空闲恢复 | 延续上一条记录（`is_continued=true`） |
| 崩溃恢复 | 启动时检查 5 分钟窗口，自动补录 |
| 睡眠检测 | 电源事件监听（RegisterPowerSettingNotification） |
| 内存目标 | < 50 MB |

---

## License

MIT
