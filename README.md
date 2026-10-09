# Learnest

> A gateway to everyday management
> 属于大学生的时间线和项目管理器。 · 版本 1.0.0 · .NET 8/9 + WPF

## 主要功能

1. ToDos and Deadlines: 可以设置待办事项和ddl，例如“高数作业1.4-1.5章节”，设置deadline到2026-10-10 23:59。内置一个万年历，无需联网，支持在电脑桌面最顶层钉住一个清单小组件，自动检测是否有项目接近ddl，标红提醒。
   
3. Timeline: 在Timeline界面可以查看所有ToDos的时间跨度，包含创建时间、修改时间和ddl。不同分类的ToDos会显示为对应的颜色。
   
3. Examination Planner: 可以添加考试信息，会显示在Timeline里面。

4. Competitions Planner: 是一个相对独立的功能，可以创建自己参加的竞赛或者活动，作为长期项目。支持作为一个标签被不同的ToDos引用，一个ToDo也可以同时引用多个Competitions。Competition内部则有里程碑记录功能，记录里程碑日期和详情，详情支持一行字/高级记录。

## 备注
软件使用.NET原生开发，占用空间小，性能极佳，且个人数据支持完整导入导出为压缩包（高级记录的文件也直接pack and go）。2026-10-10凌晨发布了1.0.0 release，欢迎下载使用喵~

程序完全离线、没有云端、不发任何网络请求。第一次启动时，它会请你**新建一个容器文件夹**或**打开已有的容器文件夹**，之后你的全部数据都在那个文件夹里，随时可以整个复制走喵~

## ***下面是Deepseek帮我写的。（懒***

## 容器与数据

**程序本体不保存任何业务数据。** 首次启动时会弹出向导，让你：

- **新建容器**：选一个你愿意备份的文件夹（例如 `文档\Learnest`），程序在其中初始化数据库；
- **打开已有容器**：选择以前用过的容器文件夹，或在换电脑后导入一个容器压缩包。

容器里的内容：

| 路径 | 内容 |
| --- | --- |
| `learnest.db` | SQLite 数据库，全部业务数据（待办、分类、考试、竞赛、里程碑、活动日志…） |
| `learnest.container.json` | 容器清单：容器名、schema 版本等元数据 |
| `attachments\` | 导入的作业文件等附件（库中保存为容器相对路径） |
| `notes\` | 导出的 markdown 笔记 |
| `backups\` | 备份与导出的压缩包 |
| `logs\` | 运行日志 |
| `backgrounds\` | 背景图片 |
| `languages\` | 导入的自定义 JSON 语言包 |

- 容器与安装位置无关：把整个容器文件夹拷到另一台电脑、另一块硬盘或 U 盘，程序打开它就能继续用。
- 程序支持把**整个容器**导出/导入为 zip，命名契约固定为：

  ```
  yyyy-MM-dd_HH-mm-ss.<容器名>.learnest.zip
  例： 2026-10-10_21-30-05.MyLearnest.learnest.zip
  ```

- 容器之外，程序只写一个文件：`%LocalAppData%\Learnest\appsettings.json`，其中只记录上次容器路径、最近容器列表与机器级的界面回退值（不含任何业务数据）。
- 数据引擎是 **SQLite**：优先使用程序同级目录的便携 `sqlite3.dll`，没有时使用 Windows 自带的 `winsqlite3.dll`。

## 目录结构

```
Learnest\
├─ src\               C# / XAML 源码：Core、Data、Services、Theming、Themes、Views、Lang
├─ dist\              构建产物：Learnest.exe、（可选）sqlite3.dll、（可选）Lang\*.json、运行时文件
├─ installer\         Learnest.iss（Inno Setup 6 脚本）与 Build-Installer.ps1（打包包装脚本）
├─ docs\              文档：BUILD.md（构建与打包）、language-packs.md（语言包）
├─ Lang\              语言包目录（可选）：放在 Learnest.exe 同级的 Lang\*.json 会被自动加载；
│                     内置的简体中文与 English 已编译进程序（源文件在 src\Lang\）
├─ lib\               （可选）放入 64 位 sqlite3.dll，SDK 构建会自动复制到 dist
├─ assets\            （可选）learnest.ico 应用图标
└─ build.ps1          构建脚本
```

## 数据与隐私

- **完全离线**：程序不发起任何网络请求，没有账号、没有遥测、没有云同步。
- **数据都在容器里**：所有待办、考试、竞赛、附件、笔记、日志、备份与背景图都存放在你自己选择的容器文件夹中；删除容器文件夹即删除全部数据。
- **容器之外只有一个配置文件**：`%LocalAppData%\Learnest\appsettings.json`（上次容器路径、最近容器列表、机器级界面回退值）。
- **日历是纯算法公历**：不含农历、不含节假日数据，因此也不需要联网更新。
- 容器可以放在 U 盘/移动硬盘上随身携带；建议定期导出 zip 备份。注意 SQLite 不适合长期放在网络共享盘或被网盘实时同步的目录里（原因与替代做法见 [docs/BUILD.md](docs/BUILD.md) 的疑难排解）。

## 许可 / 第三方

- **SQLite**：public domain（<https://www.sqlite.org/copyright.html>）。Learnest 通过 Windows 自带的 `winsqlite3.dll` 使用它，或由你自行提供便携 `sqlite3.dll`。
- **winsqlite3.dll**：Windows 系统组件，随操作系统提供，不随本仓库分发。
- **.NET / WPF**：使用 .NET 自带的 BCL 与 WPF 程序集；**没有任何第三方 .NET NuGet 包**。
- **Inno Setup**：仅在需要生成安装包时使用，属构建期工具而非运行时依赖，许可见 <https://jrsoftware.org/>；本仓库不包含其任何文件。
- 本仓库目前未附带 LICENSE 文件；如需以开源方式发布，请自行补充许可声明。
