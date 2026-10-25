# lplayer

轻量、纯粹的本地音乐播放器。**只做播放，别无冗余。**

- 应用名：`lplayer`
- 作者：[lyx](https://github.com/lyx-star)
- 技术：C# + WPF（.NET 8）+ NAudio
- 定位：本地音频文件播放；无播放列表 / 歌词 / 均衡器 / 网络 / 账号

## 功能

- 播放本地音频：MP3、WAV、FLAC、M4A/AAC、OGG、WMA
- 自绘波形进度条（点击 / 拖动定位）
- 播放 / 暂停、从头开始、前后时移、倍速、音量 / 静音
- 时间显示精确到 0.01s；短于 60 分钟为 `mm:ss.xx`，达到 1 小时为 `hh:mm:ss.xx`
- 音频属性面板、主题（深 / 浅）、设置持久化
- 拖入文件播放；命令行参数 / 文件关联启动
- 快捷键：空格播放暂停，`←` `→` 时移，`↑` `↓` 音量，`M` 静音

## 环境要求

- Windows 10/11 x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（构建时）
- 运行时：自包含发布无需安装 .NET；框架依赖发布需本机 .NET 8 Desktop Runtime

## 构建

### 开发调试

```powershell
cd lplayer
dotnet build -c Release
dotnet run -c Release
```

### 发布单文件（推荐）

**框架依赖（体积更小，需本机 .NET 8 Desktop Runtime）：**

```powershell
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=false -o publish
```

产物：`publish\lplayer.exe`（通常约 150KB 左右 + 本机运行时）

**自包含单文件（目标机器可无 .NET 直接运行，体积较大）：**

```powershell
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -o publish-sc
```

产物：`publish-sc\lplayer.exe`

### 指定输出目录示例

```powershell
dotnet publish lplayer.csproj -c Release -r win-x64 `
  -p:PublishSingleFile=true `
  -p:SelfContained=false `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o out
```

## 使用

1. 运行 `lplayer.exe`
2. 将音频文件拖入窗口，或通过资源管理器双击已关联的音频文件
3. 命令行：

```powershell
.\lplayer.exe "D:\Music\song.mp3"
```

### 文件关联

「设置 → 一键关联支持的音频格式」写入 `HKCU\Software\Classes` 下的 ProgID（`lplayer.Audio`）与扩展名映射。  
Windows 11 对默认应用有系统保护（UserChoice Hash），关联后若仍提示选择，请在 **设置 → 应用 → 默认应用** 中选择 lplayer。

「解除关联」可恢复。

### 头像

关于页头像路径：`Assets/avatar.png`（发布后位于程序目录 `Assets\avatar.png`）。  
替换该文件即可显示真实头像；缺失时显示占位图标，不会报错。

> 注意：单文件自包含发布时，非 `Resource` 的松散文件不会自动打入 exe。请将 `avatar.png` 复制到 exe 旁的 `Assets\` 目录，或在 csproj 中保持 `<Resource Include="Assets\avatar.png" />` 并通过 pack URI 加载（当前代码优先读程序目录，便于用户直接替换）。

## 配置文件

```
%AppData%\lplayer\settings.json
```

保存主题、音量、倍速、时移时长、静音状态。

## 项目结构

```
lplayer/
├── lplayer.csproj
├── App.xaml / App.xaml.cs
├── Assets/avatar.png
├── Controls/WaveformControl.cs     # 波形自绘控件
├── Helpers/TimeFormatter.cs        # 时间格式 mm:ss.xx / hh:mm:ss.xx
├── Models/
│   ├── AppSettings.cs
│   └── AudioFileInfo.cs
├── Services/
│   ├── AudioPlayerService.cs       # NAudio 播放
│   ├── WaveformService.cs          # 后台峰值解析
│   ├── SettingsService.cs          # settings.json
│   ├── FileAssociationService.cs   # 注册表关联
│   └── SpeedSampleProvider.cs      # 倍速
├── ViewModels/
│   ├── MainViewModel.cs
│   ├── SettingsViewModel（位于 SettingsWindow.xaml.cs）
│   └── RelayCommand.cs
└── Views/
    ├── MainWindow.xaml(.cs)
    ├── SettingsWindow.xaml(.cs)
    ├── AboutWindow.xaml(.cs)
    └── PropertiesWindow.xaml(.cs)
```

## 架构

```
View (XAML)  ←binding→  ViewModel  ←interface→  Service
                                              ├─ AudioPlayerService (NAudio)
                                              ├─ WaveformService (后台峰值)
                                              ├─ SettingsService
                                              └─ FileAssociationService
```

## 许可

按作者仓库惯例使用。作者主页：https://github.com/lyx-star
