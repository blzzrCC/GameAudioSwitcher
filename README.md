# GameAudioSwitcher · 游戏音频自动切换

Windows 托盘常驻小工具：检测到 **无畏契约 (Valorant)** 或 **暗区突围:无限 (Arena Breakout: Infinite)** 的游戏本体进程启动时，自动将系统默认输出设备（含语音通信设备）切换为**耳机**；游戏全部关闭后，自动恢复为**扬声器**。

- **零依赖**：单文件 .NET Framework 4.8 程序，Windows 10/11 自带运行环境，双击即用
- **托盘常驻**：驻留系统托盘，右键可退出，退出后完全释放后台
- **一键扫描设备（新增）**：托盘菜单「扫描音频输出设备…」自动枚举本机全部输出端点，按名称关键字自动识别耳机 / 扬声器，可当场「测试切换」，保存后立即生效 —— 新用户不必再去系统声音设置里手工抄写设备名
- **首次运行自动引导**：启动时若配置中的设备在本机对不上，自动弹出扫描向导完成配置
- **自定义图标**：EXE 与托盘图标统一使用「耳机 + 双向切换箭头」图案，`app.ico` 含 16/20/24/32/40/48/64/128/256 共 9 种尺寸，由 `make_icon.py` 从源图自动裁白边、等比居中生成
- **智能保护**：耳机未插入/不可用时自动跳过切换，绝不强行接管
- **边界切换模式**：仅在游戏启动/关闭瞬间自动切换，游戏运行期间你的手动操作不会被干预
- **一键开关**：托盘菜单「自动切换」可随时暂停/恢复，暂停后游戏启停不再干预设备
- **手动切换 + 全局快捷键**：随时在 耳机/扬声器 间手动切换；快捷键（默认 Ctrl+Alt+H）可在托盘菜单自定义，全屏游戏中同样生效

## 快速开始

1. 下载 Release 中的 `GameAudioSwitcher.exe`（或自行构建），与 `config.ini` 放在同一目录
2. 双击运行，程序驻留托盘（耳机图标）
3. 若提示未找到音频设备，在自动弹出的「扫描音频输出设备」窗口中点「自动识别」→「保存并应用」
4. 打开游戏 → 自动切换耳机；关闭游戏 → 自动恢复扬声器

> 提示：右键托盘图标可开关「开机自启」「自动切换」，可点「扫描音频输出设备…」重新指定设备、点「立即切换输出设备」或按全局快捷键（默认 `Ctrl+Alt+H`）手动切换；「设置切换快捷键…」可自定义快捷键。

## 配置（config.ini）

```ini
[devices]
headphone=耳机 (Realtek(R) Audio)   ; 游戏运行时使用的设备（与系统声音设置名称一致）
speaker=扬声器 (Realtek(R) Audio)   ; 无游戏时使用的设备
                                    ; 不确定设备名？用托盘菜单「扫描音频输出设备…」自动填写

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

环境：Windows + .NET Framework 4.x（自带 C# 编译器）+ Python 3（带 Pillow，仅生成图标时需要）

```bat
python make_icon.py "app.png"       :: 生成 app.ico（9 种尺寸），已生成过可跳过
build.bat                           :: 编译，缺少 app.ico 会直接报错退出
```

产物：`GameAudioSwitcher.exe`（内嵌图标）、`AuditAudio.exe`（设备列表 / 扫描结果 / 自动识别诊断）、`SetTest.exe`（切换测试）

图标相关脚本：

| 脚本 | 用途 |
| --- | --- |
| `make_icon.py` | 源图 → `app.ico`（9 种尺寸；先按 Alpha 裁掉四周透明留白，再等比缩放居中到 256 画布、四周留 4% 边距 —— 保证 16px 托盘图标主体尽量大） |
| `verify_icon.py` | 校验 `app.ico` 尺寸完整性、EXE 内嵌情况，并输出尺寸对照预览 `preview_icon.png` |

## 工作原理

- 通过 `IMMDeviceEnumerator` 枚举渲染端点，按 `FriendlyName` 匹配设备（比对时自动忽略 Windows 为同名端点添加的「N- 」消歧序号，如 `耳机 (2- Realtek(R) Audio)`），并优先选择 `ACTIVE` 端点、校验其状态
- 通过未文档化的 `PolicyConfig` COM 接口（`CLSID 870AF99C-...`，Redstone → Win7 → Vista 接口级联）调用 `SetDefaultEndpoint`，同时设置 eConsole / eMultimedia / eCommunications 三个角色
- 状态机按「游戏进程出现/消失」边界触发切换，运行期间不干预用户手动设置
- 全局快捷键通过 `RegisterHotKey` 注册到隐藏消息窗口（`NativeWindow`），收到 `WM_HOTKEY` 后在耳机/扬声器间切换；暂停自动切换不影响快捷键
- **设备扫描**：`AudioCore.ScanRenderDevices()` 返回每个端点的名称 / 归一化名称 / 端点 ID / 状态 / 是否当前默认，按「可用优先 → 默认优先 → 名称」排序；`AutoDetectPair()` 再按关键字分层推定候选（耳机：`耳机/耳麦/headphone/headset/earphone/earbud/airpod/buds/tws`；扬声器：`扬声器/喇叭/speaker` → 次关键字 `display audio/hdmi/digital output/line out/spdif` → 当前默认 → 首个可用），推定结果交用户在窗口中确认或改选
- **图标**：`app.ico` 同时以 `/win32icon`（EXE 图标）与 `/resource,AppIcon.ico`（运行时读取）两种方式内嵌；`AppIcon.Load()` 按优先级「嵌入资源 → EXE 关联图标 → 内置矢量兜底」取图标，实际来源写入日志首行便于自查

## 版本历史

- **v1.2.2**（2026-09-15）：应用图标换用高清新版源图重新生成（仍为「耳机 + 双向切换箭头」图案，9 种尺寸），并清理仓库无用/遗留文件（历史图标脚本 `make_icon_variant.py`、上版图标 `app_tray_cat.ico`、对比图 `preview_icon_alt.png`、csc 编译临时文件、运行日志等）。
- **v1.2.1**（2026-09-15）：应用图标更换为「耳机 + 双向切换箭头」图案；`make_icon.py` 增加「Alpha 自动裁白边 + 等比居中（长边占画布 92%）」预处理，修正上一版图标透明留白过多、主体偏小的问题（同尺寸下主体由 188×146 放大到 236×183）。
- **v1.2.0**（2026-09-15）：新增「扫描音频输出设备」功能（托盘菜单入口 + 自动枚举端点 + 关键字自动识别耳机/扬声器 + 测试切换 + 保存即生效），并在启动时自检设备配置、对不上时自动弹出扫描向导引导新用户；应用图标换为头像图案（`app.ico` 9 种尺寸，EXE 与托盘统一），新增 `make_icon.py` / `verify_icon.py` / `make_icon_variant.py` 图标工具链；`AuditAudio.exe` 增加扫描结果与自动识别诊断输出。
- **v1.1.1**（2026-09-11）：修复 Windows 端点重名导致切换失败的问题。设备名匹配现会忽略 Windows 自动添加的「N- 」消歧序号（如 `耳机 (2- Realtek(R) Audio)`），并在同名端点中优先选用可用（ACTIVE）的那个，避免命中已失效的历史端点；手动切换的当前设备判定同步归一化。
- **v1.1.0**（2026-09-02）：托盘新增「自动切换」总开关与「立即切换输出设备」「设置切换快捷键…」；新增可自定义的全局切换快捷键（默认 Ctrl+Alt+H），支持弹窗按键捕获并同步写入 config.ini。
- **v1.0.0**（2026-09-01）：首个公开版本。支持无畏契约 / 暗区突围:无限，双设备自动切换，托盘界面，开机自启，配置化进程名与设备名。

## 免责声明

本工具为个人开源项目，与 Riot Games、腾讯等游戏厂商无任何关联。使用本工具产生的任何音频设备设置变化均由使用者自行负责。
