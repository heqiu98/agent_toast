# agent_toast

给编程 agent（Codex / Claude Code / DSH / OpenCode）用的轻量通知工具。
Steam 风格弹窗 + 提示音 + 图形化设置界面。agent 配置里只需一行钩子命令，其余都在本工具内管理。

## 下载与安装

从 [Releases](https://github.com/heqiu98/agent_toast/releases) 下载 `agent_toast.exe`（单文件、免安装、仅依赖 Windows 自带 .NET Framework 4.x），双击打开设置界面，点「安装到本机」即可。首次运行可能被 SmartScreen 拦截：点「更多信息 → 仍要运行」即可（未签名软件的常规提示，攒够下载量后自动消失）。

**隐私**：本工具完全本地运行——不联网、无遥测、不收集任何数据；仅在 agent 钩子触发时读取本地会话记录以提取任务名。

## 两种运行模式

| 启动方式 | 行为 |
|---|---|
| 双击运行（无参数、无管道） | 打开**设置界面** |
| 被 agent hook 调用（stdin 管道） | 弹出通知（样式/音效按设置文件执行） |
| 命令行带参数 | 直接弹通知（通用模式） |

## 安装到固定位置（推荐）

各 agent 的钩子配置里写的是 exe 的**绝对路径**，所以 exe 一旦被移动/删除，提醒会静默失效。建议先安装到固定位置：

```powershell
agent_toast.exe --install     # 复制到 %LOCALAPPDATA%\agent_toast，迁移设置与自定义音效，
                              # 创建开始菜单快捷方式，并把各 agent 配置刷新到新路径
agent_toast.exe --uninstall   # 移除所有 agent 的钩子 + 快捷方式 + 安装目录
agent_toast.exe --apply-enabled  # 重新应用所有已启用的 agent（手动刷新路径用）
```

设置界面里也有「安装到本机」按钮；每次打开设置界面时会自动检查各 agent 配置里的路径是否仍指向当前 exe，不一致会提示一键修复。不想安装也可以继续便携使用，只是别随便挪 exe。

## 设置界面

- 选择 agent：Codex / Claude Code / DSH / OpenCode；启动时自动检测已安装的 agent 并选中
- 每个 agent 独立配置：
  - 主任务提示：开关、弹窗样式、提示音（可无声）
  - 子 agent 提示：开关、弹窗样式、提示音（可无声）
  - 显示时长：弹窗停留时间（1–60 秒，主任务与子 agent 弹窗共用）
- **确定**：保存偏好（不写入 agent 配置）
- **应用**：保存偏好 + 自动写入该 agent 的配置文件（Codex 的 config.toml / Claude 的 settings.json / DSH 的 profile 补丁）
- **一键开启**：为所有检测到的 agent 开启并应用（默认样式/音效）
- **测试**：立即用当前选择弹一条测试通知
- **取消提示**：关闭该 agent 的提示并移除已写入的钩子配置

偏好保存在 exe 同目录的 `agent_toast.settings.json`。

## 命令行用法

```powershell
# 通用弹通知
agent_toast.exe "标题" "内容" --duration 2000

# 指定样式/音效（可用值见下）
agent_toast.exe "标题" "内容" --style light --sound beep

# 仅响铃 / 仅弹窗
agent_toast.exe --no-toast
agent_toast.exe "标题" "内容" --no-sound

# 无界面地应用/移除某个 agent 的配置（等价于设置界面的 应用 / 取消提示）
agent_toast.exe --apply codex
agent_toast.exe --unapply codex

# 强制打开设置界面
agent_toast.exe --gui
```

样式预设：`steam`（深色+蓝条，默认）、`light`（浅色）、`minimal`（极简暗色）。
音效：`none`、`asterisk`（系统叮）、`beep`（蜂鸣）、`exclamation`（感叹号）。

## 作为 agent 钩子使用

### Codex（由 应用 按钮自动写入，无需手改）

```toml
# >>> agent_toast >>>
[[hooks.Stop]]
matcher = ""
[[hooks.Stop.hooks]]
type = "command"
command = "F:/path/to/agent_toast.exe --agent codex"
# <<< agent_toast <<<
```

（实际路径以 应用 写入的为准；子 agent 提示还会有对应的 SubagentStop 区块。）

### Claude Code

`应用` 按钮自动合并写入 `~/.claude/settings.json` 的 `hooks` 字段（保留已有其他配置）。

### DSH（DeepSeek Harness）

`应用` 按钮会写入（均带 `agent_toast` 标记，取消提示 时干净移除）：

- `~/.dsh/profiles/*/agent_toast-dsh.mjs` —— 每个 profile 一个零依赖的原生小插件，直接监听 DSH 的回合停止 / 子 agent 结束事件并拉起本工具（即发即弃，不拖慢回合结束）
- `~/.dsh/profiles/*/cordis.patch.yml` —— 挂载该插件的标记区块

不用 DSH 内置的 hooks 桥接插件是有意的：profile 里预置的桥接包版本可能旧于运行时，会被 DSH 的插件版本门禁禁用；本地文件插件没有包清单，天然免疫。

DSH 的 profile 补丁在进程启动时合成一次，**完全退出并重启 DSH 后生效**。

### OpenCode

`应用` 按钮写入 `~/.config/opencode/plugins/agent_toast.js` —— OpenCode 的全局插件目录在启动时自动加载其中的 JS 文件（[官方插件机制](https://opencode.ai/docs/plugins/)），无需改任何配置文件；`取消提示` 直接删除该文件。

插件订阅 `session.idle` 事件：会话空闲（回合结束）即触发；子 agent 会话通过 SDK 的 `session.get` 检查 `parentID` 区分，按子 agent 提示的样式/音效走。**重启 OpenCode 后生效**。

## 调试

设置环境变量 `AGENT_TOAST_LOG` 为文件路径，每次触发会记录解析出的事件、agent、样式和文案。

## 构建

```powershell
.\build.ps1
```

源码在 `src/`（core.cs 样式/音效/弹窗/配置，app.cs 模式分发与 agent 配置写入，gui.cs 设置界面），
输出到 `release/agent_toast.exe`。构建脚本直接调用 .NET Framework 自带的 csc.exe，源码因此保持旧版 C# 语法。
## 自定义提示音

- 设置界面"主任务提示"组的 **导入...** 按钮可选择 `.wav` / `.mp3` 文件，自动复制到 exe 旁 `sounds/` 目录
- 导入后，主/子 agent 的音效下拉框会列出所有自定义文件（标注"自定义"）
- 也可以手动把 `.wav` / `.mp3` 文件丢进 `sounds/` 目录，同样会被自动识别
- 重复文件名自动加 `_1`、`_2` 后缀避免覆盖
- 自定义音效在设置文件中记为 `custom:文件名`，内置音效为 `asterisk` / `beep` / `exclamation` / `none`

## 主任务弹窗文案

- 标题自动显示任务名（从会话记录中提取你本轮的指令，超长截断 20 字）；仅 Codex 支持提取，其余 agent 标题回退为 agent 名（如 "DSH"）
- 正文文字可在设置界面自定义，最多 6 个字，默认「任务已完成」

