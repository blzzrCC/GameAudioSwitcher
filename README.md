# GameAudioSwitcher · 游戏音频自动切换

Windows 托盘常驻小工具：检测到 **无畏契约 (Valorant)** 或 **暗区突围:无限 (Arena Breakout: Infinite)** 的游戏本体进程启动时，自动将系统默认输出设备（含语音通信设备）切换为**耳机**；游戏全部关闭后，自动恢复为**扬声器**。

- **零依赖**：单文件 .NET Framework 4.8 程序，Windows 10/11 自带运行环境，双击即用
- **托盘常驻**：驻留系统托盘，右键可退出，退出后完全释放后台
- **智能保护**：耳机未插入/不可用时自动跳过切换，绝不强行接管
- **边界切换模式**：仅在游戏启动/关闭瞬间自动切换，游戏运行期间你的手动操作不会被干预
- **一键开关**：托盘菜单「自动切换」可随时暂停/恢复，暂停后游戏启停不再干预设备
- **手动切换 + 全局快捷键**：随时在 耳机/扬声器 间手动切换；快捷键（默认 Ctrl+Alt+H）可在托盘菜单自定义，全屏游戏中同样生效

## 快速开始

1. 下载 Release 中的 `GameAudioSwitcher.exe`（或自行构建），与 `config.ini` 放在同一目录
2. 双击运行，程序驻留托盘（蓝色耳机图标）
3. 打开游戏 → 自动切换耳机；关闭游戏 → 自动恢复扬声器

> 提示：右键托盘图标可开关「开机自启」「自动切换」，点「立即切换输出设备」或按全局快捷键（默认 `Ctrl+Alt+H`）可手动在耳机/扬声器间切换；「设置切换快捷键…」可自定义快捷键。

## 配置（config.ini）

```ini
[devices]
headphone=耳机 (Realtek(R) Audio)   ; 游戏运行时使用的设备（与系统声音设置名称一致）
speaker=扬声器 (Realtek(R) Audio)   ; 无游戏时使用的设备

[settings]
poll_interval_ms=2000               ; 进程检测间隔（毫秒，最小 500）
balloon=1                           ; 切换气泡提示（1 开 / 0 关）
hotkey=Ctrl+Alt+H                   ; 全局切换快捷键，留空=禁用；可托盘菜单自定义

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

- 通过 `IMMDeviceEnumerator` 枚举渲染端点，按 `FriendlyName` 匹配设备（比对时自动忽略 Windows 为同名端点添加的「N- 」消歧序号，如 `耳机 (2- Realtek(R) Audio)`），并优先选择 `ACTIVE` 端点、校验其状态
- 通过未文档化的 `PolicyConfig` COM 接口（`CLSID 870AF99C-...`，Redstone → Win7 → Vista 接口级联）调用 `SetDefaultEndpoint`，同时设置 eConsole / eMultimedia / eCommunications 三个角色
- 状态机按「游戏进程出现/消失」边界触发切换，运行期间不干预用户手动设置
- 全局快捷键通过 `RegisterHotKey` 注册到隐藏消息窗口（`NativeWindow`），收到 `WM_HOTKEY` 后在耳机/扬声器间切换；暂停自动切换不影响快捷键

## 版本历史

- **v1.1.1**（2026-09-11）：修复 Windows 端点重名导致切换失败的问题。设备名匹配现会忽略 Windows 自动添加的「N- 」消歧序号（如 `耳机 (2- Realtek(R) Audio)`），并在同名端点中优先选用可用（ACTIVE）的那个，避免命中已失效的历史端点；手动切换的当前设备判定同步归一化。
- **v1.1.0**（2026-09-02）：托盘新增「自动切换」总开关与「立即切换输出设备」「设置切换快捷键…」；新增可自定义的全局切换快捷键（默认 Ctrl+Alt+H），支持弹窗按键捕获并同步写入 config.ini。
- **v1.0.0**（2026-09-01）：首个公开版本。支持无畏契约 / 暗区突围:无限，双设备自动切换，托盘界面，开机自启，配置化进程名与设备名。

## 免责声明

本工具为个人开源项目，与 Riot Games、腾讯等游戏厂商无任何关联。使用本工具产生的任何音频设备设置变化均由使用者自行负责。
