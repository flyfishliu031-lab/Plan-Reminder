# 计划表 · Plan Reminder

一款离线使用的 Windows 桌面计划表。用日历安排计划，用颜色区分内容，完成后直接划去。

![添加计划窗口](assets/editor.png)

## 下载与运行

在 [Releases](https://github.com/flyfishliu031-lab/Plan-Reminder/releases) 下载：

- **安装版**：`PlanReminder-1.0.0-Setup-x64.exe`，安装到当前用户目录，可创建桌面快捷方式。
- **便携版**：`PlanReminder-1.0.0-Portable-x64.zip`，解压后双击 `PlanReminder.exe`。

支持 Windows 10/11 64 位。成品包含 .NET 运行环境，无需另装 .NET，无需账号或联网。

## 第一版功能

- 月历与当天计划列表，支持“回到今天”和“待安排”。
- 添加、编辑计划标题与备注；日期、时间段、预期时长均可选。
- 跨天计划在覆盖的每一天显示，恰好结束于午夜时不占用下一天。
- 每个新计划自动分配颜色，支持预设色与自定义颜色。
- 勾选表示完成：文字保留原色并显示删除线。取消勾选即可恢复。
- 删除立即保存，10 秒内可以撤销；快捷键 `Ctrl+Z` 同样可以撤销。
- 按开始时间排序，无时间段的计划排在后面；划去不改变排序。
- 时间段重叠时提示，仍允许保存。
- 系统时间、时区与“今天”标记随设备更新；浏览其他日期时不会被自动跳回今天。
- 自动保存、导入与导出 JSON 备份、上一次写入的自动备份及损坏数据恢复。
- `Ctrl+N` 添加计划，月历支持方向键移动日期。

预期时长单独记录，不是倒计时。重复计划、到点提醒、多设备同步不包含在第一版。

## 数据位置与备份

数据保存在 `%LOCALAPPDATA%\PlanReminder\plans.json`，左下角可以打开数据目录。安装版和便携版使用同一份数据。关闭软件、升级或卸载不会主动删除计划。

每次保存通过临时文件替换完成；上一次有效数据保留在 `plans.json.bak`。无法读取主文件时，软件会询问是否恢复自动备份，并另存损坏的原文件。

“导出备份”生成 JSON 文件。“导入备份”按计划编号合并，编号相同时保留更新时间较新的版本，避免重复导入。删除后超过 10 秒需从先前导出的备份恢复。

时间段保存为本地日历日期和钟表时间。例如 09:00 的计划在修改设备时区后仍显示 09:00。软件显示的是设备当前时间，不通过互联网校准时钟。

标题最多 500 字，备注最多 5000 字；预期时长支持 1 分钟到 365 天。备份与数据文件上限 20 MB。长内容在列表中截断，可在编辑窗口查看全文。

源码仓库和 Releases 只包含程序与示例界面，个人计划数据不上传 GitHub。

## 从源码构建

需要 Windows、.NET 10 SDK；生成安装包另需 Inno Setup 6.7 或更高版本。

```powershell
./scripts/build.ps1 -InnoCompiler 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
```

脚本构建应用、运行功能检查、发布包含运行环境的便携版，再生成安装包和 SHA-256 校验文件。输出位于 `artifacts`，不提交二进制到源码历史。

仅构建与检查：

```powershell
$env:MSBuildEnableWorkloadResolver = 'false'
dotnet build -c Release
dotnet ./bin/Release/net10.0-windows/PlanReminder.dll --self-test
```

检查覆盖可选时间、跨天边界、颜色分配、划去与恢复、删除撤销、数据重启保留、备份合并、损坏恢复和保存失败。检查只操作临时目录。

用于界面演示的样例数据可在独立目录创建：

```powershell
./bin/Release/net10.0-windows/PlanReminder.exe --demo --data-dir ./artifacts/demo
```

## 项目结构

使用 .NET 原生 WinForms、System.Text.Json 和文件系统；没有额外运行时依赖。`PlanItem.cs` 定义计划与时间规则，`PlanStore.cs` 负责保存，`MainForm.cs`、`PlanEditor.cs` 与日历/卡片控件负责界面，`SelfTest.cs` 提供一个可执行功能检查。
