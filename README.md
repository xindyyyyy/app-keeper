# App Keeper

轻量 Windows 桌面应用守护工具。每个启用的程序使用进程句柄注册退出等待，不使用持续轮询。

## 开发

项目目标为 `.NET 10 WPF`。如果使用本机的 DiskTrace SDK：

```powershell
& 'F:\DiskTrace\.dotnet\dotnet.exe' build .\AppKeeper.csproj
& 'F:\DiskTrace\.dotnet\dotnet.exe' test .\Tests\AppKeeper.Tests.csproj
```

## 发布

```powershell
.\publish.ps1
```

发布结果为 `publish\win-x64\App Keeper.exe`，采用框架依赖单文件模式：程序本身不内置 .NET 运行时，因此体积较小；目标机器缺少 .NET 10 Desktop Runtime 时，Windows 会提示下载安装。首次启动需要联网完成运行时安装。

配置默认写入程序目录的 `appkeeper.settings.json`；若目录不可写，将回退到当前用户的 LocalAppData。
