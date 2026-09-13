# codex-toast

给编程 agent（Codex / Claude Code / OpenCode / Aider ...）用的轻量通知弹窗工具。
Steam 风格深色小窗，右下角弹出，默认 2 秒自动消失（淡入淡出），自带提示音。
所有提示逻辑都在 exe 内部，agent 配置里只需一行钩子命令。

## 功能

- 弹窗：右下角、置顶、圆角、点击可提前关闭、多个弹窗自动向上堆叠
- 提示音：Windows 系统提示音（`--no-sound` 关闭）
- 事件识别：从 stdin 读取 agent hook 的 JSON，自动区分主任务 / 子任务
- 零依赖单文件 exe，WinForms 实现，无需安装

## 命令行用法

```powershell
# 通用：标题 + 内容 + 时长(毫秒)
codex-toast.exe "Codex" "任务已完成" 2000

# 只响铃不弹窗
codex-toast.exe --no-toast

# 只弹窗不响铃
codex-toast.exe "Codex" "任务已完成" --no-sound
```

参数：`[title] [message] [--duration ms] [--no-sound] [--no-toast]`
不传标题/内容时，默认显示 "Codex / Task finished"。

## 作为 agent 钩子使用

### Codex（config.toml）

```toml
[[hooks.Stop]]
matcher = ""
[[hooks.Stop.hooks]]
type = "command"
command = "F:/repositories/new/codex-toast/release/codex-toast.exe"

[[hooks.SubagentStop]]
matcher = ""
[[hooks.SubagentStop.hooks]]
type = "command"
command = "F:/repositories/new/codex-toast/release/codex-toast.exe"
```

exe 从 stdin 的 JSON 里自动识别事件：`Stop` → "任务已完成"，`SubagentStop` → "子任务已完成"。
首次配置后 TUI 会弹一次 "Hooks need review"，选 **Trust all and continue** 即可（只问一次）。

### Claude Code（settings.json）

```json
{
  "hooks": {
    "Stop": [{"hooks": [{"type": "command", "command": "F:/repositories/new/codex-toast/release/codex-toast.exe"}]}],
    "SubagentStop": [{"hooks": [{"type": "command", "command": "F:/repositories/new/codex-toast/release/codex-toast.exe"}]}]
  }
}
```

### 其他

任何支持"事件触发时执行 shell 命令"的工具（Aider 的 `--notification-command`、OpenCode 插件等）
都可以直接调用这个 exe，带参数或管道 JSON 均可。

## 调试

设置环境变量 `CODEX_TOAST_LOG` 指向一个文件路径，每次触发会记录解析出的事件和文案。

## 构建

```powershell
.\build.ps1
```

源码在 `src/codex-toast.cs`（单文件 C#），输出到 `release/codex-toast.exe`。