# App Keeper

App Keeper 是一个轻量的 Windows 桌面应用守护工具：添加一个 EXE 后，它会在目标程序退出时重新启动，并在系统托盘中保持运行。

## 功能

- 添加和移除需要守护的 EXE
- 查看运行状态、PID、本次与累计重启次数
- 单独启用或暂停某个程序的守护
- 连续异常退出达到阈值后自动暂停，避免无限重启
- 随 Windows 启动
- 关闭窗口时隐藏到系统托盘，双击托盘图标可重新打开
- 使用进程退出句柄等待，不使用后台定时轮询

## 使用

运行 `App Keeper.exe` 后，点击“添加程序”选择一个 `.exe` 文件。添加后会立即开始守护。

窗口右上角的 `×` 会隐藏到托盘；若要完全退出，请在托盘图标菜单中选择“退出”。移除守护项目不会关闭目标程序。

## 运行要求

- Windows x64
- `.NET 10 Desktop Runtime`

发行版采用框架依赖单文件模式。目标电脑没有 .NET 10 Desktop Runtime 时，Windows 会提示下载安装；首次安装运行时需要联网。

## 开发

项目使用 `.NET 10 WPF` 和 Windows Forms 托盘图标。如果使用本机的 DiskTrace SDK：

```powershell
& 'F:\DiskTrace\.dotnet\dotnet.exe' build .\AppKeeper.csproj -c Release --nologo
& 'F:\DiskTrace\.dotnet\dotnet.exe' test .\Tests\AppKeeper.Tests.csproj -c Release --nologo
```

## 发布

```powershell
.\publish.ps1
```

主发行文件为：`publish\win-x64\App Keeper.exe`。

配置默认写入程序目录的 `appkeeper.settings.json`；若目录不可写，将回退到当前用户的 LocalAppData。该配置文件不应提交到 Git。

## 已知边界

当前版本不支持启动参数、自定义工作目录、服务账户、远程管理或 Windows 服务安装。目标程序本身的权限、依赖和启动失败由目标程序负责。
