# GameAudioSwitcher · 游戏音频自动切换

Windows 托盘常驻小工具：检测到 **无畏契约 (Valorant)** 或 **暗区突围:无限 (Arena Breakout: Infinite)** 的游戏本体进程启动时，自动将系统默认输出设备（含语音通信设备）切换为**耳机**；游戏全部关闭后，自动恢复为**扬声器**。

- **零依赖**：单文件 .NET Framework 4.8 程序，Windows 10/11 自带运行环境，双击即用
- **托盘常驻**：驻留系统托盘，右键可退出，退出后完全释放后台
- **智能保护**：耳机未插入/不可用时自动跳过切换，绝不强行接管
- **边界切换模式**：仅在游戏启动/关闭瞬间自动切换，游戏运行期间你的手动操作不会被干预

## 快速开始

1. 下载 Release 中的 `GameAudioSwitcher.exe`（或自行构建），与 `config.ini` 放在同一目录
2. 双击运行，程序驻留托盘（蓝色耳机图标）
3. 打开游戏 → 自动切换耳机；关闭游戏 → 自动恢复扬声器

> 提示：右键托盘图标可开关「开机自启」。

## 配置（config.ini）

```ini
[devices]
headphone=耳机 (Realtek(R) Audio)   ; 游戏运行时使用的设备（与系统声音设置名称一致）
speaker=扬声器 (Realtek(R) Audio)   ; 无游戏时使用的设备

[settings]
poll_interval_ms=2000               ; 进程检测间隔（毫秒，最小 500）
balloon=1                           ; 切换气泡提示（1 开 / 0 关）

[games]
processes=VALORANT-Win64-Shipping.exe;VALORANT.exe;UAGame.exe;ABInfinite-Win64-Shipping.exe;ABInfinite.exe
                                    ; 游戏本体进程名，分号分隔，任一存在即视为运行中
                                    ; 暗区突围:无限 国服(WeGame)本体进程为 UAGame.exe
```

## 自行构建

环境：Windows + .NET Framework 4.x（自带 C# 编译器 csc.exe）

```bat
build.bat
```

产物：`GameAudioSwitcher.exe`、`AuditAudio.exe`（设备列表诊断）、`SetTest.exe`（切换测试）

## 工作原理

- 通过 `IMMDeviceEnumerator` 枚举渲染端点，按 `FriendlyName` 精确匹配设备，并校验端点 `ACTIVE` 状态
- 通过未文档化的 `PolicyConfig` COM 接口（`CLSID 870AF99C-...`，Redstone → Win7 → Vista 接口级联）调用 `SetDefaultEndpoint`，同时设置 eConsole / eMultimedia / eCommunications 三个角色
- 状态机按「游戏进程出现/消失」边界触发切换，运行期间不干预用户手动设置

## 版本历史

- **v1.0.0**（2026-09-01）：首个公开版本。支持无畏契约 / 暗区突围:无限，双设备自动切换，托盘界面，开机自启，配置化进程名与设备名。

## 免责声明

本工具为个人开源项目，与 Riot Games、腾讯等游戏厂商无任何关联。使用本工具产生的任何音频设备设置变化均由使用者自行负责。
