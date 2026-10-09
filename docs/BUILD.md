# Learnest 构建与打包指南 · Build & Packaging Guide

> 适用版本：Learnest **1.0.0**（“A gateway to everyday management”）
> 目标平台：Windows 10 1607 及以上 / Windows 11（x64）；.NET 8 + WPF 桌面程序
> 相关文档：[README.md](../README.md) · [language-packs.md](language-packs.md)

本文只讲构建、自检、打包与运行期数据位置，不涉及业务功能说明。

---

## 1. 前置条件 Prerequisites

### 1.1 首选工具链：.NET 8 或 9 SDK + Windows Desktop 工作负载

| 项目 | 说明 |
| --- | --- |
| 目标框架 | `src\Learnest.csproj` 为 `net8.0-windows`、`UseWPF=true`（.NET 9 SDK 也能构建 `net8.0-windows` 目标） |
| 需要的组件 | .NET SDK + **Windows Desktop 工作负载**（提供 WPF 引用程序集 `Microsoft.WindowsDesktop.App.Ref`） |
| 检查是否已安装 | `dotnet --list-sdks`（没有任何 `8.x` / `9.x` 输出就需要安装） |

安装方式任选其一：

```powershell
# 官方安装包 / winget
winget install --id Microsoft.DotNet.SDK.8 -e
# 也可以下载安装程序： https://dotnet.microsoft.com/download/dotnet/8.0
```

如果使用 Visual Studio / Build Tools 安装器，请勾选工作负载 **“.NET 桌面开发” / “.NET Desktop Development”**，它包含 WPF 所需的 Windows Desktop 引用包。

> **分发注意**：`dotnet build` 生成的是**框架依赖 (framework-dependent)** 程序，目标机器需要安装 **.NET Desktop Runtime 8 (x64)**。若希望目标机器什么都不用装，请见 [2.3 自包含发布](#23-可选自包含发布-self-contained-publish)。

### 1.2 备用工具链：系统自带 .NET Framework 4.x MSBuild（不需要 SDK，不需要联网）

Windows 10/11 自带 .NET Framework 4.x，其中包含 MSBuild 与 C# 编译器：

```
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe     (32 位系统: Microsoft.NET\Framework\...)
```

`build.ps1` 会自动检测它，并在没有 .NET SDK（或 SDK 构建失败）时回退到这条路线；也可以加 `-ForceFrameworkToolchain` 强制使用。同一份源码会被编译成针对系统内置 WPF 程序集（`PresentationFramework`、`PresentationCore`、`WindowsBase`、`System.Xaml`）的单个 `Learnest.exe`，`TargetFrameworkVersion` 为 `v4.0`。

* 不需要网络，不需要 Visual Studio，不需要 NuGet 还原。
* 产出的 `dist` 里会多出若干**系统运行时文件**（例如 `mscorlib.dll`、`norm*.nlp`、`zh-Hans\mscorlib.resources.dll`）——这是正常现象，打包时一并带上即可。
* 局限：无法使用 .NET 8 专有 API；如果某个改动用到了新 API，请改用 SDK 工具链。

### 1.3 两条工具链对比

| | .NET SDK（首选） | 系统 MSBuild（备用） |
| --- | --- | --- |
| 需要预装 | .NET 8/9 SDK + Windows Desktop 工作负载 | 无（Windows 自带） |
| 需要联网 | 仅安装 SDK 时需要 | 不需要 |
| 目标机器需要 | .NET Desktop Runtime 8（除非自包含发布） | .NET Framework 4.x（Windows 自带） |
| 产物 | `dist\Learnest.exe`（+ 依赖） | `dist\Learnest.exe`（+ 少量运行时文件） |

---

## 2. 构建命令 Build commands

在 `Learnest\` 目录（`build.ps1` 所在目录）执行：

```powershell
# (1) 直接用 SDK 构建到 dist\（等价于 build.ps1 的首选路径）
dotnet build src\Learnest.csproj -c Release -o dist

# (2) 推荐：包装脚本，自动挑选工具链，SDK 失败时回退到系统 MSBuild
pwsh -File build.ps1

# (3) 构建后运行无界面自检（详见第 3 节）
pwsh -File build.ps1 -SelfTest

# (4) 构建后启动程序
pwsh -File build.ps1 -Run
```

Windows PowerShell 5.1 用户把 `pwsh` 换成：

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

`build.ps1` 的开关：

| 开关 | 作用 |
| --- | --- |
| `-Configuration Release\|Debug` | 构建配置，默认 `Release` |
| `-OutputDirectory <路径>` | 输出目录，默认 `.\dist` |
| `-Run` | 构建完成后启动 Learnest |
| `-SelfTest` | 构建完成后运行 `Learnest.exe --selftest`，并以其退出码结束 |
| `-ForceFrameworkToolchain` | 忽略 .NET SDK，强制使用系统 MSBuild |
| `-Quiet` | 减少输出 |

产物：`dist\Learnest.exe`。用 SDK 构建时，如果存在 `lib\sqlite3.dll`，它会被自动复制到 `dist`（`src\Learnest.csproj` 里的 `CopyToOutputDirectory`）；**备用工具链不会复制**，需要时请手动放进 `dist\`。

### 2.3 可选：自包含发布 (self-contained publish)

不想让用户安装 .NET 运行时，就用自包含发布；它把 .NET 运行时一起放进 `dist`（体积明显变大），之后同样可以 xcopy 部署，也能被 `installer\Learnest.iss` 一起打包：

```powershell
dotnet publish src\Learnest.csproj -c Release -r win-x64 --self-contained true -o dist
```

---

## 3. 无界面自检 Headless self test

```powershell
pwsh -File build.ps1 -SelfTest          # 构建 + 自检
.\dist\Learnest.exe --selftest          # 只跑自检（/selftest 亦可）
```

* **退出码**：`0` = 全部通过；`1` = 有用例失败；`2` = 抛出异常中断。
* Learnest.exe 是窗口程序：在终端里运行时它会附加到父控制台打印报告；没有控制台时报告写到 `%TEMP%\learnest-selftest.log`。
* 自检使用临时容器 `%TEMP%\learnest-selftest-<HHmmss>\`，结束时删除；若文件被占用会保留该目录并打印路径。
* 自检**不联网、不写注册表、不接触你正在使用的真实容器**。

自检覆盖的内容：

1. **容器与数据库**：新建容器目录、`learnest.db`、`learnest.container.json`、`schemaVersion=1`、SQLite 原生库探测结果（`winsqlite3.dll` 或 `sqlite3.dll` 及其版本）。
2. **分类**：默认分类播种（≥6 个）、`Uncategorized` 桶存在、颜色持久化、删除分类后待办回落到 Uncategorized、Uncategorized 不可删除。
3. **待办**：中文/emoji 标题、markdown 高级记录、截止时间、标签、子步骤、进度、完成与重新打开。
4. **附件**：导入到容器的 `attachments\`（落库为相对路径）、重命名后保留扩展名。
5. **考试**：考试计划持久化与中文往返。
6. **竞赛**：里程碑、里程碑进度、重要事件、竞赛标签自动生成、一条待办同时引用多个竞赛。
7. **查询与统计**：计数、按截止时间排序的查询、活动日志。
8. **容器内设置**：主题 / 强调色 / 背景模式 / 背景模糊 / 置顶小组件排序 / 语言，关闭后重新打开容器仍能还原。
9. **备份往返**：导出 zip（校验命名契约）、导入到新目录后数据库、附件、markdown 笔记、设置完全一致。
10. **万年历引擎**：周一为一周起始、周末判定、月网格为整周且包含当月 1 号、闰年规则（2000 闰 / 1900 平）、ISO 周序号、剩余时间文案。

---

## 4. 打包安装程序 Building the installer (optional)

**安装包是可选的**：`dist\` 本身就是可以直接运行、可以整个复制到 U 盘的成品（xcopy 部署）。只有当你想给同学一个双击即装的 `Setup.exe` 时才需要 Inno Setup。

### 4.1 安装 Inno Setup（仓库不附带）

```powershell
winget install --id JRSoftware.InnoSetup -e
# 或从官方页面下载： https://jrsoftware.org/isdl.php
```

需要 **Inno Setup 6.3 或更新版本**（脚本里的 `ArchitecturesInstallIn64BitMode=x64compatible` 自 6.3 起才有）。安装界面的简体中文语言文件 `ChineseSimplified.isl` 自 **6.5.0** 起是官方内置翻译；`Learnest.iss` 用 `#if FileExists(...)` 做了保护，用更老的 6.x 编译器只会得到英文安装界面，不会编译失败。

### 4.2 一条命令打包

```powershell
pwsh -File installer\Build-Installer.ps1              # 先重新构建 dist，再打包
pwsh -File installer\Build-Installer.ps1 -SkipBuild   # 直接用现有 dist 打包
pwsh -File installer\Build-Installer.ps1 -Quiet       # 安静模式，ISCC 用 /Q
```

脚本会：定位 `ISCC.exe` → 找不到时打印中英文提示（安装命令 + 网址）并以非零码退出（**绝不下载任何东西**）→ 编译 `installer\Learnest.iss` → 报告安装包路径与大小。

产物：`dist\installer\Learnest-1.0.0-Setup.exe`

### 4.3 手动编译（含静默命令行）

```powershell
# 有进度输出
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /Qp installer\Learnest.iss
# 完全静默
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /Q  installer\Learnest.iss
```

`Learnest.iss` 里已经写好 `OutputDir=..\dist\installer` 与 `OutputBaseFilename=Learnest-1.0.0-Setup`，不需要额外参数。

### 4.4 安装包行为清单

* **安装位置由用户选择**：`DisableDirPage=no` 是刻意的（脚本里有大写注释标记），目录页始终显示，用户可在“选择目标位置”页输入路径或点“浏览…”选择文件夹；默认值是 `{autopf}\Learnest`。
* **每用户 / 每机器可选**：`PrivilegesRequiredOverridesAllowed=dialog`（也支持 `/CURRENTUSER`、`/ALLUSERS`）。每机器安装会落到 `C:\Program Files\Learnest`，每用户安装落在 `%LocalAppData%\Programs\Learnest`。
* **升级**：`UsePreviousAppDir=yes`，默认沿用上一次的安装目录（仍可修改）。
* **快捷方式**：开始菜单项；可选桌面图标（任务 `desktopicon`）；可选“随 Windows 启动”（任务 `startupicon`，在当前用户的“启动”文件夹放快捷方式）。
* **可选**安装完成后立即启动 Learnest。
* **卸载程序**由 Inno Setup 自动生成，`UninstallDisplayIcon={app}\Learnest.exe`；卸载**不会**删除你的容器文件夹，也不会删除 `%LocalAppData%\Learnest\appsettings.json`。
* **批量静默安装**（机房/实验室）：

  ```powershell
  .\Learnest-1.0.0-Setup.exe /VERYSILENT /CURRENTUSER /NORESTART
  ```

* 安装包只打包 `dist` 的内容，并排除 `*.pdb` 与用户导出的容器压缩包（`*.learnest.zip`）。要带上便携 SQLite，请先按第 6 节把 `sqlite3.dll` 准备好再打包。

---

## 5. 运行期数据位置 Where data lives at run time

Learnest 的程序目录只放程序，**不保存任何业务数据**。首次启动时程序会要求用户**新建容器文件夹**或**打开已有容器文件夹**，之后所有个人数据都在那个容器里。

| 位置 | 内容 |
| --- | --- |
| `%LocalAppData%\Learnest\appsettings.json` | **容器之外唯一**被写入的文件：`lastContainer`（上次容器路径）、`recentContainers`（最近 8 个容器）、`languageOverride`、`themeOverride`、`showSetupWizard`、`portableMode`、`firstRunCompleted`、`lastUpdateCheck`，以及 `app` / `format` 两个标识字段。不含任何待办、考试或竞赛内容。其中 `lastUpdateCheck` 只是一个预留的字符串字段，Learnest 不做任何联网更新检查（源码中没有网络调用）。 |
| `<容器>\learnest.db` | SQLite 数据库，全部业务数据 |
| `<容器>\learnest.container.json` | 容器清单/元数据（容器名、schema 版本等） |
| `<容器>\attachments\` | 从待办/考试/竞赛导入的作业文件等附件 |
| `<容器>\notes\` | 导出的 markdown 笔记 |
| `<容器>\backups\` | 备份与导出的 zip |
| `<容器>\logs\` | 运行日志 |
| `<容器>\backgrounds\` | 背景图片 |
| `<容器>\languages\` | 导入的 JSON 语言包（见 [language-packs.md](language-packs.md)） |

* 容器用 `learnest.db` 或 `learnest.container.json` 作为识别标志，扩展名标记为 `.learnest`。
* 容器与安装位置完全解耦：可以整个文件夹复制到另一台电脑、另一块硬盘或 U 盘，程序打开它即可继续用。
* 导出/导入整个容器的 zip 命名契约固定为：

  ```
  yyyy-MM-dd_HH-mm-ss.<容器名>.learnest.zip
  例： 2026-10-10_21-30-05.MyLearnest.learnest.zip
  ```

  导入时把它解压/导入到一个新文件夹即可。迁移数据请优先用这种方式，而不是直接对正在使用的 `learnest.db` 做文件级复制。

---

## 6. 便携 SQLite（可选）Portable sqlite3.dll

探测顺序（见 `src\Data\Sqlite3.cs`）：

1. **程序同级目录的 `sqlite3.dll`**（便携版，优先使用）；
2. 否则使用 **Windows 系统自带的 `winsqlite3.dll`**（Windows 10 1607 / Server 2016 及以上）。

放置方式：

```powershell
# 方式一（推荐，SDK 构建会自动复制到 dist）
#   把 64 位 sqlite3.dll 放到  Learnest\lib\sqlite3.dll
Copy-Item .\sqlite3.dll .\lib\sqlite3.dll
pwsh -File build.ps1

# 方式二（备用工具链或手工部署）
Copy-Item .\sqlite3.dll .\dist\sqlite3.dll
```

* 来源：<https://www.sqlite.org/download.html> 的 *Precompiled Binaries for Windows*（x64 包里的 `sqlite3.dll`）。文件名必须正好是 `sqlite3.dll`，位数必须与程序一致（x64）。
* 验证：`pwsh -File build.ps1 -SelfTest`，报告中的 `native library:` 一行会显示 `sqlite3.dll <版本>`（使用系统库时显示 `winsqlite3.dll <版本>`）。
* **不需要**把系统的 `winsqlite3.dll` 复制/改名为 `sqlite3.dll`：没有 app-local 文件时程序会自动使用系统库。

---

## 7. 疑难排解 Troubleshooting

| 现象 | 原因与处理 |
| --- | --- |
| 自检里 `sqlite library detected` 失败，报告 `(unavailable)` | 系统太老（低于 Windows 10 1607 / Server 2016）没有 `winsqlite3.dll`。按第 6 节在 `Learnest.exe` 旁边放一个便携 `sqlite3.dll`。 |
| 容器放在网络共享盘 / 网盘同步目录后变慢、打不开或数据异常 | SQLite 依赖本地文件锁，SMB（`\\server\share`）与 OneDrive 等实时同步目录上的锁不可靠。请把容器放在本地磁盘，用导出/导入 zip 在多机之间搬运。 |
| 防火墙/杀毒软件拦截或安装包被 SmartScreen 拦下 | 自编译的程序没有代码签名：SmartScreen 里点“更多信息 → 仍要运行”即可；Defender 实时扫描会对 `learnest.db`+WAL 反复加锁造成卡顿，可把容器目录与 `dist` 加入排除项；构建时同样建议排除 `dist`，避免编译器输出被锁。 |
| 权限错误（无法写入、创建容器失败） | 容器要放在你有写权限的目录（例如“文档”）；每机器安装需要管理员同意，每用户安装不需要（安装界面里可选，也可用 `/CURRENTUSER`）。写入 `%LocalAppData%\Learnest\appsettings.json` 失败时程序会静默降级，不影响容器数据。 |
| 双击 `Learnest.exe` 提示缺少 .NET 运行时 | `dist` 是框架依赖构建。请安装 **.NET Desktop Runtime 8 (x64)**，或用 [2.3 自包含发布](#23-可选自包含发布-self-contained-publish) 重新发布。 |
| `dist` 里出现 `mscorlib.dll`、`norm*.nlp`、`zh-Hans\` | 备用工具链（系统 MSBuild）的产物，属正常运行时文件，打包时保留即可。 |
| `Build-Installer.ps1` 报找不到 ISCC.exe | 未安装 Inno Setup 6。`winget install --id JRSoftware.InnoSetup -e` 或 <https://jrsoftware.org/isdl.php>；脚本不会自动下载。 |
| ISCC 报 `Unknown architecture` 或编译失败 | Inno Setup 低于 6.3 不支持 `x64compatible`，请升级到 6.3+（建议 6.5+，简体中文界面为官方内置）。 |
| 中文安装界面没出现 | 编译器里没有 `Languages\ChineseSimplified.isl`（6.5.0 之前不内置），安装包按英文界面生成，属预期行为。 |

---

## 8. 发布检查清单 Release checklist

- [ ] 更新 `src\Learnest.csproj` 的 `<Version>`
- [ ] 更新 `installer\Learnest.iss` 的 `AppVersion`、`VersionInfoVersion`、`OutputBaseFilename`（默认已按 1.0.0 写好）
- [ ] `pwsh -File build.ps1 -SelfTest` 全绿
- [ ] `pwsh -File installer\Build-Installer.ps1` 生成 `dist\installer\Learnest-1.0.0-Setup.exe`
- [ ] 在一台干净（或至少没装过 Learnest）的机器上安装，走一遍首次启动的容器向导，确认数据写在容器里
- [ ] 若改动过语言包，同步更新 [language-packs.md](language-packs.md)；若改动过打包流程，同步更新本文
