# App Keeper 协作说明

## 项目定位

App Keeper 是 Windows-only 的 .NET 10 WPF 应用守护工具。它管理用户添加的 EXE，在进程退出后按策略重新启动，并通过系统托盘提供常驻入口。

## 技术边界

- 目标框架：`net10.0-windows`
- UI：WPF
- 托盘：Windows Forms `NotifyIcon`
- 配置：JSON，优先写入程序目录，失败时回退到当前用户 LocalAppData
- 进程等待：`ThreadPool.RegisterWaitForSingleObject`，不要改回定时轮询
- 发布：win-x64、框架依赖、单文件 EXE

## 核心语义

- `ProcessGuardService` 负责添加、移除、启停和监听目标程序
- `ProcessWaitRegistration` 只负责等待进程句柄退出
- `RestartPolicy` 使用 5 分钟滚动窗口；短时间内连续退出达到阈值时进入 `Paused`
- `Paused` 状态必须提供明确的“恢复”操作
- 移除守护项目不得关闭目标程序
- 关闭主窗口默认隐藏到托盘；只有托盘菜单的“退出”才真正结束 App Keeper

## UI 约定

- 保持 Windows 桌面工具的克制、清晰和高对比度风格
- 主窗口是右下角的小型托盘面板，不要恢复成大面积仪表盘
- 状态不能只依靠颜色表达，必须同时有文字
- 所有按钮和复选框保留可访问名称、工具提示和键盘焦点
- 添加程序窗口使用 `.exe` 文件选择器，并在确认前验证文件存在且扩展名正确
- 不为未实现的功能添加入口或宣传文案

## 常用验证

本机无全局 .NET SDK 时，使用已存在的 SDK：

```powershell
& 'F:\DiskTrace\.dotnet\dotnet.exe' build .\AppKeeper.csproj -c Release --nologo
& 'F:\DiskTrace\.dotnet\dotnet.exe' test .\Tests\AppKeeper.Tests.csproj -c Release --nologo
.\publish.ps1
```

## 文件边界

- 不要提交 `bin/`、`obj/`、`publish/`、`.dotnet/` 或 `appkeeper.settings.json`
- 修改跨层行为时，同时检查 ViewModel、服务、配置模型和测试
- 不要使用破坏性 Git 操作覆盖用户未提交的工作
- 发布目录可能被正在运行的 EXE 锁定；发布前先关闭对应的 App Keeper 进程
