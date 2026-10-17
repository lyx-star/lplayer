# lplayer

<p align="center">
  <b>轻量、纯粹的本地音乐播放器</b><br/>
  只做播放，别无冗余
</p>

<p align="center">
  <img alt="Windows" src="https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-0078D6?logo=data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCA4OCA4OCI+PHBhdGggZmlsbD0iIzAwNzhENiIgZD0iTTAsMGg0MXY0MEgweiIvPjxwYXRoIGZpbGw9IiMwMDc4RDYiIGQ9Ik00OCwwaDQwdjQwSDQ4eiIvPjxwYXRoIGZpbGw9IiMwMDc4RDYiIGQ9Ik0wLDQ4aDQxdjQwSDB6Ii8+PHBhdGggZmlsbD0iIzAwNzhENiIgZD0iTTQ4LDQ4aDQwdjQwSDQ4eiIvPvc="/>
  <img alt=".NET" src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet">
  <img alt="NAudio" src="https://img.shields.io/badge/audio-NAudio-FF6A00">
  <img alt="License" src="https://img.shields.io/badge/license-MIT-green">
</p>

---

## 简介

**lplayer** 是一款面向 Windows 的轻量级本地音乐播放器。

没有播放列表管理、没有歌词、没有均衡器、没有网络和账号——  
打开文件、看波形、拖进度、听歌，仅此而已。

适合希望「双击就能听、界面干净、冷启动快」的场景。

## 功能

- 🎵 播放本地音频：**MP3 / WAV / FLAC / M4A / AAC / OGG / WMA**
- 🌊 自绘**波形进度条**，点击 / 拖动定位，拖动时实时显示时间
- ⏯ 播放 / 暂停、时移跳转、倍速（0.5x–2.0x）
- 🎛 音频属性面板（编码、采样率、比特率、时长等）
- 📁 拖入文件播放；支持命令行参数与文件关联
- 🖼 内置 **MiSans** 字体与作者头像
- ⚡ 启动快、资源占用低（峰值波形，不缓存整段 PCM）

## 界面

| 主界面 | 设置 | 关于 |
|--------|------|------|
| 波形 + 播放控制 + 时间 | 时移时长、文件关联 | 作者 lyx-star |

## 快捷键

| 按键 | 作用 |
|------|------|
| `空格` | 播放 / 暂停 |
| `←` / `→` | 后退 / 前进时移（时长可在设置中调整） |

## 下载与运行

### 方式一：自包含版（推荐分发）

下载 **`lplayer-selfcontained-win-x64.zip`**，解压后双击 `lplayer.exe`。

- ✅ 目标电脑**无需安装** .NET 或其它运行时  
- ❌ 不能用于 ARM 设备；仅 **Windows 10/11 x64**

### 方式二：框架依赖版（体积更小）

需本机已安装 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)。

## 从源码构建

```bash
git clone https://github.com/lyx-star/lplayer.git
cd lplayer
dotnet build -c Release
```

### 发布单文件（框架依赖）

```bash
dotnet publish -c Release -r win-x64 \
  -p:PublishSingleFile=true \
  -p:SelfContained=false \
  -o publish
```

### 发布自包含（目标机零依赖）

```bash
dotnet publish -c Release -r win-x64 \
  -p:PublishSingleFile=true \
  -p:SelfContained=true \
  -o publish-sc
```

## 项目结构

```
lplayer/
├── App.xaml                 # 应用入口与全局字体
├── Controls/
│   └── WaveformControl.cs   # 波形自绘控件
├── Helpers/
│   └── TimeFormatter.cs     # mm:ss.xx / hh:mm:ss.xx
├── Models/                  # 设置与音频元数据
├── Services/                # 播放 / 波形 / 设置 / 注册表关联
├── ViewModels/              # MVVM
├── Views/                   # 主窗口、设置、关于、属性
└── Assets/
    ├── avatar.png           # 关于页头像（可替换）
    └── Fonts/               # MiSans
```

## 技术栈

| 层 | 技术 |
|----|------|
| UI | WPF (.NET 8) + 原生 XAML 自绘 |
| 音频 | NAudio（WaveOut / Media Foundation / Vorbis） |
| 架构 | MVVM（ViewModel / Service / View 分离） |
| 配置 | `%AppData%\lplayer\settings.json` |

## 文件关联

在 **设置 → 一键关联支持的音频格式** 中，将常见音频扩展名指向 lplayer（写入 `HKCU\Software\Classes`）。  
Windows 11 可能仍要求在「默认应用」中确认一次。

## 关于作者

**lyx-star**  
GitHub：[https://github.com/lyx-star](https://github.com/lyx-star)

## 许可

本项目采用 [MIT License](LICENSE) 发布（若仓库未附 LICENSE 文件，可自行补充）。

---

<p align="center">
  轻量、纯粹的本地音乐播放器 · <b>lplayer</b>
</p>
