# agent_toast

给编程 agent（Codex / Claude Code / OpenCode）用的轻量通知工具。
Steam 风格弹窗 + 提示音 + 图形化设置界面。agent 配置里只需一行钩子命令，其余都在本工具内管理。

## 两种运行模式

| 启动方式 | 行为 |
|---|---|
| 双击运行（无参数、无管道） | 打开**设置界面** |
| 被 agent hook 调用（stdin 管道） | 弹出通知（样式/音效按设置文件执行） |
| 命令行带参数 | 直接弹通知（通用模式） |

## 设置界面

- 选择 agent：Codex / Claude Code / OpenCode（预留）
- 每个 agent 独立配置：
  - 主任务提示：开关、弹窗样式、提示音（可无声）
  - 子 agent 提示：开关、弹窗样式、提示音（可无声）
- **确定**：保存偏好（不写入 agent 配置）
- **应用**：保存偏好 + 自动写入该 agent 的配置文件（Codex 的 config.toml / Claude 的 settings.json）
- **测试**：立即用当前选择弹一条测试通知
- **取消提示**：关闭该 agent 的提示并移除已写入的钩子配置

偏好保存在 exe 同目录的 `agent_toast.settings.json`。

## 命令行用法

```powershell
# 通用弹通知
agent_toast.exe "标题" "内容" 2000

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

## 调试

设置环境变量 `AGENT_TOAST_LOG` 为文件路径，每次触发会记录解析出的事件、agent、样式和文案。

## 构建

```powershell
.\build.ps1
```

源码在 `src/`（core.cs 样式/音效/弹窗/配置，app.cs 模式分发与 agent 配置写入，gui.cs 设置界面），
输出到 `release/agent_toast.exe`。构建脚本会把公共 using 合并、兼容旧版 C# 编译器。
## 自定义提示音

- 设置界面"主任务提示"组的 **导入...** 按钮可选择 `.wav` 文件，自动复制到 exe 旁 `sounds/` 目录
- 导入后，主/子 agent 的音效下拉框会列出所有自定义文件（标注"自定义"）
- 也可以手动把 `.wav` 文件丢进 `sounds/` 目录，同样会被自动识别
- 重复文件名自动加 `_1`、`_2` 后缀避免覆盖
- 自定义音效在设置文件中记为 `custom:文件名.wav`，内置音效为 `asterisk` / `beep` / `exclamation` / `none`

## 主任务弹窗文案

- 标题自动显示任务名（从会话记录中提取你本轮的指令，超长截断 20 字）
- 正文文字可在设置界面自定义，最多 6 个字，默认「任务已完成」

