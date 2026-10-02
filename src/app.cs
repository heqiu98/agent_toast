// agent_toast app: mode dispatch, notification mode, agent config writers.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace AgentToast
{
    public class Args
    {
        public string Agent;        // --agent codex|claude|opencode
        public string StyleId;      // --style
        public string SoundId;      // --sound
        public int DurationMs = 0;         // 0 = not specified on the command line
        public bool NoSound, NoToast;
        public string Title, Message;
        public bool Gui;            // --gui
        public string ApplyAgent;   // --apply <agent>
        public string UnapplyAgent; // --unapply <agent>
        public bool Install;        // --install
        public bool Uninstall;      // --uninstall
        public bool ApplyEnabled;   // --apply-enabled
        public bool Version;        // --version

        public static Args Parse(string[] argv)
        {
            var a = new Args();
            for (int i = 0; i < argv.Length; i++)
            {
                string s = argv[i];
                if (s == "--agent" && i + 1 < argv.Length) a.Agent = argv[++i];
                else if (s == "--style" && i + 1 < argv.Length) a.StyleId = argv[++i];
                else if (s == "--sound" && i + 1 < argv.Length) a.SoundId = argv[++i];
                else if (s == "--duration" && i + 1 < argv.Length) int.TryParse(argv[++i], out a.DurationMs);
                else if (s == "--no-sound") a.NoSound = true;
                else if (s == "--no-toast") a.NoToast = true;
                else if (s == "--gui") a.Gui = true;
                else if (s == "--apply" && i + 1 < argv.Length) a.ApplyAgent = argv[++i];
                else if (s == "--unapply" && i + 1 < argv.Length) a.UnapplyAgent = argv[++i];
                else if (s == "--install") a.Install = true;
                else if (s == "--uninstall") a.Uninstall = true;
                else if (s == "--apply-enabled") a.ApplyEnabled = true;
                else if (s == "--version") a.Version = true;
                else if (a.Title == null) a.Title = s;
                else if (a.Message == null) a.Message = s;
            }
            return a;
        }
    }

    public static class Program
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();
        [STAThread]
        public static int Main(string[] argv)
        {
            // Console.OutputEncoding defaults to the system OEM codepage, which cannot
            // represent CJK in paths (mojibake / failed writes). UTF-8 is lossless.
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
            Args args = Args.Parse(argv);

            // Mode detection:
            //  --gui flag           -> always GUI
            //  no args + no console -> GUI (double-clicked GUI-subsystem exe; stdin checks are meaningless there)
            //  no args + tty stdin  -> GUI (console exe run interactively)
            //  otherwise            -> popup / CLI / apply modes
            bool noConsole = (GetConsoleWindow() == IntPtr.Zero);
            if (args.Gui || (argv.Length == 0 && (noConsole || !Console.IsInputRedirected)))
            {
                Application.EnableVisualStyles();
                Application.Run(new SettingsForm());
                return 0;
            }
            if (args.Install) return Installer.Install();
            if (args.Uninstall) return Installer.Uninstall();
            if (args.ApplyEnabled) return Installer.ApplyEnabled();
            if (args.Version)
            {
                var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                Console.WriteLine("agent_toast " + v.Major + "." + v.Minor + "." + v.Build);
                return 0;
            }
            if (args.ApplyAgent != null) return AgentWriter.Apply(args.ApplyAgent) ? 0 : 1;
            if (args.UnapplyAgent != null) { AgentWriter.Remove(args.UnapplyAgent); return 0; }
            return Notify.Run(args);
        }
    }

    public static class Notify
    {
        public static int Run(Args args)
        {
            string hookEvent = null;
            string transcriptPath = null;
            if (Console.IsInputRedirected)
            {
                // Some agents never close the hook's stdin pipe; never block on it.
                // Read on a background thread, wait briefly, then proceed with defaults.
                string json = null;
                var sync = new object();
                var reader = new System.Threading.Thread(() =>
                {
                    try
                    {
                        string s = Console.In.ReadToEnd();
                        lock (sync) { json = s; }
                    }
                    catch { }
                });
                reader.IsBackground = true;
                reader.Start();
                reader.Join(300);
                lock (sync) { }
                if (!string.IsNullOrEmpty(json))
                {
                    // Parse hook_event_name precisely; substring sniffing misfires when the
                    // words "Stop"/"SubagentStop" appear in paths or other fields.
                    try
                    {
                        var d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json.Trim());
                        object ev;
                        if (d != null && d.TryGetValue("hook_event_name", out ev) && ev != null) hookEvent = Convert.ToString(ev);
                    }
                    catch { }
                    var m = Regex.Match(json, "\"transcript_path\"\\s*:\\s*\"([^\"]+)\"");
                    if (m.Success) transcriptPath = m.Groups[1].Value.Replace("\\\\", "\\");
                }
            }
            // Agent known but no usable payload (stdin timeout etc.): assume main completion.
            if (hookEvent == null && !string.IsNullOrEmpty(args.Agent)) hookEvent = "Stop";

            string styleId = args.StyleId;
            string soundId = args.SoundId;

            // Per-agent settings override defaults (CLI flags still win).
            AgentOptions o = null;
            if (!string.IsNullOrEmpty(args.Agent))
            {
                var cfg = ConfigStore.Load();
                if (!cfg.Agents.TryGetValue(args.Agent, out o) || !o.Enabled)
                {
                    Dbg.Log("agent=" + args.Agent + " disabled or unknown, skip");
                    return 0;
                }
                bool isSub = hookEvent == "SubagentStop";
                if (isSub)
                {
                    if (!o.SubEnabled) { Dbg.Log("subagent notify disabled, skip"); return 0; }
                    if (styleId == null) styleId = o.SubStyleId;
                    if (soundId == null) soundId = o.SubSoundId;
                }
                else
                {
                    if (styleId == null) styleId = o.StyleId;
                    if (soundId == null) soundId = o.SoundId;
                }
            }
            if (soundId == null) soundId = args.NoSound ? "none" : "asterisk";

            string title = args.Title;
            string message = args.Message;
            string agentName = AgentWriter.DisplayName(args.Agent);
            if (title == null)
            {
                if (hookEvent == "SubagentStop")
                {
                    title = agentName + " 子agent";
                    if (message == null) message = "子任务已完成";
                }
                else if (hookEvent == "Stop")
                {
                    string taskName = TaskName.FromTranscript(transcriptPath);
                    title = taskName != null ? taskName : agentName;
                    if (message == null)
                    {
                        string custom = (o != null && !string.IsNullOrEmpty(o.TextMain)) ? o.TextMain : null;
                        message = custom != null ? custom : "任务已完成";
                    }
                }
                else { title = agentName; if (message == null) message = "Task finished"; }
            }
            if (message == null) message = "Task finished";

            Dbg.Log("transcript=" + transcriptPath + " event=" + hookEvent + " agent=" + args.Agent + " style=" + styleId + " sound=" + soundId + " title=" + title + " message=" + message);


            int durationMs = args.DurationMs > 0 ? args.DurationMs
                         : (o != null && o.DurationMs > 0 ? o.DurationMs : 2000);

            var soundTask = SoundEngine.Play(args.NoSound ? "none" : soundId);
            if (!args.NoToast)
            {
                Application.EnableVisualStyles();
                Application.Run(new ToastForm(StyleDef.Find(styleId), title, message, durationMs));
            }
            // Let long custom sounds finish after the toast closes: process exit would
            // destroy the MCI device / SoundPlayer mid-playback.
            try { soundTask.Wait(30000); } catch { }
            return 0;
        }
    }

    // Writes/removes hook configuration for each agent.
    public static class AgentWriter
    {
        public static string ExePath()
        {
            try { return System.Reflection.Assembly.GetExecutingAssembly().Location; }
            catch { return "agent_toast.exe"; }
        }

        // Path safe to embed in TOML/JSON config files (forward slashes, no escapes needed).
        public static string ConfigSafeExePath()
        {
            return ExePath().Replace("\\", "/");
        }

        public static string Home()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        public static bool IsSupported(string agent)
        {
            return agent == "codex" || agent == "claude" || agent == "dsh" || agent == "opencode";
        }

        // Display name used for toast titles when no task name can be extracted.
        public static string DisplayName(string agent)
        {
            if (agent == "codex") return "Codex";
            if (agent == "claude") return "Claude Code";
            if (agent == "dsh") return "DSH";
            if (agent == "opencode") return "OpenCode";
            return "agent_toast";
        }

        // Whether the agent itself appears to be installed on this machine.
        public static bool AgentHomeExists(string agent)
        {
            if (agent == "codex") return Directory.Exists(Path.Combine(Home(), ".codex"));
            if (agent == "claude") return Directory.Exists(Path.Combine(Home(), ".claude"));
            if (agent == "dsh") return Directory.Exists(Path.Combine(Home(), ".dsh"));
            if (agent == "opencode") return Directory.Exists(Path.Combine(Home(), ".config", "opencode"));
            return false;
        }

        // Enabled agents whose written config still points at a DIFFERENT exe path
        // (e.g. the exe was moved after applying). Their hooks silently fire nothing.
        public static List<string> FindStaleAgents(AppConfig cfg)
        {
            var stale = new List<string>();
            string cur = ConfigSafeExePath();
            foreach (var kv in cfg.Agents)
            {
                string agent = kv.Key;
                if (!kv.Value.Enabled) continue;
                if (agent == "codex" || agent == "claude")
                {
                    string p = agent == "codex"
                        ? Path.Combine(Home(), ".codex", "config.toml")
                        : Path.Combine(Home(), ".claude", "settings.json");
                    if (File.Exists(p))
                    {
                        string t = TextFile.Read(p);
                        if (t.Contains("agent_toast") && !t.Contains(cur)) stale.Add(agent);
                    }
                }
                else if (agent == "dsh")
                {
                    string prof = Path.Combine(Home(), ".dsh", "profiles");
                    if (Directory.Exists(prof))
                    {
                        foreach (string dir in Directory.GetDirectories(prof))
                        {
                            string f = Path.Combine(dir, "agent_toast-dsh.mjs");
                            if (File.Exists(f) && !TextFile.Read(f).Contains(cur)) { stale.Add(agent); break; }
                        }
                    }
                    if (!stale.Contains(agent))
                    {
                        string legacy = Path.Combine(Home(), ".dsh", "agent_toast.hooks.json");
                        if (File.Exists(legacy) && !TextFile.Read(legacy).Contains(cur)) stale.Add(agent);
                    }
                }
                else if (agent == "opencode")
                {
                    string f = Path.Combine(Home(), ".config", "opencode", "plugins", "agent_toast.js");
                    if (File.Exists(f) && !TextFile.Read(f).Contains(cur)) stale.Add(agent);
                }
            }
            return stale;
        }

        public static bool Apply(string agent)
        {
            if (!IsSupported(agent)) { Console.Error.WriteLine("unknown agent: " + agent); return false; }
            var cfg = ConfigStore.Load();
            AgentOptions o;
            if (!cfg.Agents.TryGetValue(agent, out o)) { o = cfg.For(agent); ConfigStore.Save(cfg); }
            if (agent == "codex") return ApplyCodex(o);
            if (agent == "claude") return ApplyClaude(o);
            if (agent == "dsh") return ApplyDsh(o);
            if (agent == "opencode") return ApplyOpencode(o);
            return false;
        }

        public static void Remove(string agent)
        {
            var cfg = ConfigStore.Load();
            cfg.For(agent).Enabled = false;
            ConfigStore.Save(cfg);
            if (agent == "codex") ApplyCodex(cfg.Agents[agent]);
            else if (agent == "claude") ApplyClaude(cfg.Agents[agent]);
            else if (agent == "dsh") ApplyDsh(cfg.Agents[agent]);
            else if (agent == "opencode") ApplyOpencode(cfg.Agents[agent]);
        }

        // ---- codex: managed region inside ~/.codex/config.toml ----
        // Codex gates hooks behind trust: each handler is keyed by
        // <config path>:<event>:<group>:<handler> under [hooks.state], and only runs when
        // its trusted_hash matches a hash of the normalized hook identity (sha256 over the
        // compact key-sorted JSON; see codex-rs config/fingerprint.rs + hooks/src/engine).
        // We therefore also write the trust entries, so applying is enough — no TUI approval.
        static bool ApplyCodex(AgentOptions o)
        {
            try
            {
                string dir = Path.Combine(Home(), ".codex");
                if (!Directory.Exists(dir))
                {
                    // Never create the agent's home: that would make detection think it is installed.
                    if (o.Enabled) { Console.Error.WriteLine("codex not detected (~/.codex missing), skipped"); return false; }
                    Console.WriteLine("codex not detected, nothing to remove");
                    return true;
                }
                string path = Path.Combine(dir, "config.toml");
                string text = File.Exists(path) ? TextFile.Read(path) : "";
                // drop our managed region
                text = Regex.Replace(text, @"(?s)# >>> (?:codex-toast|agent_toast) >>>.*?# <<< (?:codex-toast|agent_toast) <<<\r?\n?", "");
                // one-time migration: drop legacy unmarked hook blocks that point at us
                text = Regex.Replace(text,
                    @"(?ms)^\[\[hooks\.(Stop|SubagentStop)\]\]\r?\n.*?^\[\[hooks\.\1\.hooks\]\]\r?\ntype = ""command""\r?\ncommand = ""[^""]*(agent_toast|codex-toast|notify-hook)[^""]*""\r?\n",
                    "");
                // drop stale trust-state entries for our hooks (an outdated trusted_hash would
                // mark us "Modified" and silently never run; re-adding the same key would
                // also collide as a duplicate TOML table)
                text = Regex.Replace(text, @"(?ms)^\[hooks\.state\.'[^'\r\n]*:(?:stop|subagent_stop):\d+:\d+'\][^\r\n]*\r?\ntrusted_hash[^\r\n]*\r?\n?", "");
                if (o.Enabled)
                {
                    string cmd = ConfigSafeExePath() + " --agent codex";
                    // Our group index = number of same-event groups the user already has.
                    int stopIndex = Regex.Matches(text, @"(?m)^\[\[hooks\.Stop\]\]").Count;
                    int subIndex = Regex.Matches(text, @"(?m)^\[\[hooks\.SubagentStop\]\]").Count;
                    string region = "# >>> agent_toast >>>\n\n[[hooks.Stop]]\nmatcher = \"\"\n\n[[hooks.Stop.hooks]]\ntype = \"command\"\ncommand = \"" + cmd + "\"\n";
                    if (o.SubEnabled)
                    {
                        region += "\n[[hooks.SubagentStop]]\nmatcher = \"\"\n\n[[hooks.SubagentStop.hooks]]\ntype = \"command\"\ncommand = \"" + cmd + "\"\n";
                    }
                    region += "\n[hooks.state.'" + path + ":stop:" + stopIndex + ":0']\ntrusted_hash = \"" + CodexHookHash("stop", cmd, false) + "\"\n";
                    if (o.SubEnabled)
                    {
                        region += "\n[hooks.state.'" + path + ":subagent_stop:" + subIndex + ":0']\ntrusted_hash = \"" + CodexHookHash("subagent_stop", cmd, true) + "\"\n";
                    }
                    region += "\n# <<< agent_toast <<<\n";
                    text = text.TrimEnd() + "\n\n" + region;
                }
                TextFile.Write(path, text);
                Console.WriteLine("codex config updated: " + path);
                return true;
            }
            catch (Exception ex) { Console.Error.WriteLine("codex apply failed: " + ex.Message); return false; }
        }

        // Reproduces codex's hook trust hash: sha256 of the compact, key-sorted JSON of the
        // normalized hook identity (verified byte-exact against codex-cli 0.155.1).
        // Stop ignores matchers (identity omits "matcher"); SubagentStop keeps ours ("").
        static string CodexHookHash(string eventLabel, string command, bool includeMatcher)
        {
            string json = "{\"event_name\":\"" + eventLabel + "\",\"hooks\":[{\"async\":false,\"command\":" + JsonString(command) + ",\"timeout\":600,\"type\":\"command\"}]"
                        + (includeMatcher ? ",\"matcher\":\"\"" : "") + "}";
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
                var sb = new StringBuilder("sha256:", 71);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        // serde_json escapes only ", \ and control chars; non-ASCII stays raw UTF-8.
        static string JsonString(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                else sb.Append(c);
            }
            return sb.Append("\"").ToString();
        }

        // ---- claude: merge hooks into ~/.claude/settings.json ----
        static bool ApplyClaude(AgentOptions o)
        {
            try
            {
                string dir = Path.Combine(Home(), ".claude");
                if (!Directory.Exists(dir))
                {
                    if (o.Enabled) { Console.Error.WriteLine("claude not detected (~/.claude missing), skipped"); return false; }
                    Console.WriteLine("claude not detected, nothing to remove");
                    return true;
                }
                string path = Path.Combine(dir, "settings.json");
                var ser = new JavaScriptSerializer();
                Dictionary<string, object> root;
                if (File.Exists(path))
                {
                    try { root = ser.Deserialize<Dictionary<string, object>>(TextFile.Read(path)) ?? new Dictionary<string, object>(); }
                    catch { root = new Dictionary<string, object>(); }
                }
                else root = new Dictionary<string, object>();

                object hooksObj;
                Dictionary<string, object> hooks;
                if (!root.TryGetValue("hooks", out hooksObj)) { hooks = new Dictionary<string, object>(); }
                else hooks = hooksObj as Dictionary<string, object> ?? new Dictionary<string, object>();

                SetClaudeHook(hooks, "Stop", o.Enabled, o);
                SetClaudeHook(hooks, "SubagentStop", o.Enabled && o.SubEnabled, o);

                root["hooks"] = hooks;
                // strict JSON consumers (JSON.parse) reject a BOM
                TextFile.WritePlain(path, ser.Serialize(root));
                Console.WriteLine("claude settings updated: " + path);
                return true;
            }
            catch (Exception ex) { Console.Error.WriteLine("claude apply failed: " + ex.Message); return false; }
        }

        static void SetClaudeHook(Dictionary<string, object> hooks, string eventName, bool enable, AgentOptions o)
        {
            string cmd = ConfigSafeExePath() + " --agent claude";
            List<object> list = null;
            object existing;
            if (hooks.TryGetValue(eventName, out existing)) list = existing as List<object>;
            if (list == null) list = new List<object>();

            // drop entries that already point at us
            var kept = new List<object>();
            foreach (var entry in list)
            {
                var d = entry as Dictionary<string, object>;
                string s = d == null ? entry.ToString() : new JavaScriptSerializer().Serialize(d);
                if (s != null && s.IndexOf("agent_toast", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                kept.Add(entry);
            }
            list = kept;

            if (enable)
            {
                var innerCmd = new Dictionary<string, object>();
                innerCmd["type"] = "command";
                innerCmd["command"] = cmd;
                var inner = new List<object>();
                inner.Add(innerCmd);
                var entry = new Dictionary<string, object>();
                entry["hooks"] = inner;
                list.Add(entry);
            }
            if (list.Count == 0) hooks.Remove(eventName);
            else hooks[eventName] = list;
        }

        // ---- dsh: native mini-plugin + managed insert block in ~/.dsh/profiles/*/cordis.patch.yml ----
        // We ship a tiny dependency-free ESM plugin into each profile directory and mount it
        // through the profile's patch layer. A local file has no package manifest, so DSH's
        // plugin version gate does not apply — unlike the bundled hooks-bridge package, whose
        // profile-store copy can be older than the runtime and gets disabled at startup.
        // The plugin listens on the agent/turn-stopping and subagent/end seams and launches
        // this exe with the hook JSON on stdin (fire-and-forget, so turns never wait).
        // Profile patches compose at process start, so changes take effect after a DSH restart.
        static bool ApplyDsh(AgentOptions o)
        {
            try
            {
                string dshHome = Path.Combine(Home(), ".dsh");
                string profilesDir = Path.Combine(dshHome, "profiles");
                if (!Directory.Exists(profilesDir))
                {
                    Console.Error.WriteLine("dsh profiles not found: " + profilesDir);
                    return false;
                }

                int patched = 0;
                foreach (string dir in Directory.GetDirectories(profilesDir))
                {
                    string patchFile = Path.Combine(dir, "cordis.patch.yml");
                    if (!File.Exists(patchFile)) continue;
                    string pluginFile = Path.Combine(dir, "agent_toast-dsh.mjs");

                    if (o.Enabled) TextFile.Write(pluginFile, DshPluginSource());
                    else { try { File.Delete(pluginFile); } catch { } }

                    string text = TextFile.Read(patchFile);
                    text = Regex.Replace(text, @"(?s)# >>> agent_toast >>>.*?# <<< agent_toast <<<", "");
                    // A bare "[]" is the empty-array placeholder (the default DSH patch file
                    // ships as a comment header plus one [] line); it cannot coexist with real
                    // entries in the same document, so drop it before appending ours.
                    text = Regex.Replace(text, @"(?m)^[ \t]*\[\][ \t]*\r?\n?", "");
                    if (o.Enabled)
                    {
                        string region =
                            "# >>> agent_toast >>>\n" +
                            "- insert:\n" +
                            "    - id: agent_toast-hooks\n" +
                            "      name: './agent_toast-dsh.mjs'\n" +
                            "# <<< agent_toast <<<";
                        string head = text.TrimEnd();
                        text = (head.Length > 0 ? head + "\n\n" : "") + region + "\n";
                    }
                    else if (!HasYamlEntries(text))
                    {
                        // Nothing but comments left: keep them and re-add the empty array.
                        string tail = text.TrimEnd();
                        text = (tail.Length > 0 ? tail + "\n" : "") + "[]\n";
                    }
                    TextFile.Write(patchFile, text);
                    patched++;
                }

                // Legacy artifacts from the superseded hook-bridge integration.
                try { File.Delete(Path.Combine(dshHome, "agent_toast.hooks.json")); } catch { }
                try { File.Delete(Path.Combine(dshHome, "agent_toast.dsh-hook.cmd")); } catch { }

                if (patched == 0)
                {
                    Console.Error.WriteLine("no dsh profile patch files found under: " + profilesDir);
                    return false;
                }
                Console.WriteLine("dsh config updated (" + patched + " profile(s)): " + profilesDir);
                return true;
            }
            catch (Exception ex) { Console.Error.WriteLine("dsh apply failed: " + ex.Message); return false; }
        }

        static string DshPluginSource()
        {
            return DshPlugin.Replace("@@EXE@@", ConfigSafeExePath());
        }

        // The profile-mounted mini plugin. Single quotes only, so this verbatim string
        // needs no escaping; @@EXE@@ is replaced with the forward-slashed exe path.
        const string DshPlugin =
@"// agent_toast DSH plugin — managed file, rewritten on every apply.
// Fires agent_toast when a turn stops or a subagent ends; never blocks the turn.
import { spawn } from 'node:child_process';

const EXE = '@@EXE@@';

export const name = 'agent_toast-dsh';

function fire(hookEvent) {
	try {
		const child = spawn(EXE, ['--agent', 'dsh'], { stdio: ['pipe', 'ignore', 'ignore'], windowsHide: true, detached: true });
		child.on('error', function () {});
		child.stdin.on('error', function () {});
		child.stdin.end(JSON.stringify({ session_id: '', transcript_path: '', cwd: '', hook_event_name: hookEvent }) + '\n');
		child.unref();
	} catch (e) {}
}

export function apply(ctx) {
	ctx.on('agent/turn-stopping', function () { fire('Stop'); });
	ctx.on('subagent/end', function () { fire('SubagentStop'); });
}
";

        static bool HasYamlEntries(string text)
        {
            foreach (string ln in text.Split('\n'))
            {
                string s = ln.Trim();
                if (s.Length == 0 || s == "[]" || s.StartsWith("#")) continue;
                return true;
            }
            return false;
        }

        // ---- opencode: local plugin file in ~/.config/opencode/plugins/ ----
        // OpenCode has no hook config; it auto-loads JS/TS plugin files from the global
        // plugins directory at startup (docs: https://opencode.ai/docs/plugins/). The
        // plugin subscribes to session.idle (turn finished) and launches this exe.
        // Subagent sessions idle too; they are told apart via the SDK client's
        // session.get (child sessions carry a parentID). Fire-and-forget, turns never wait.
        static bool ApplyOpencode(AgentOptions o)
        {
            try
            {
                string home = Path.Combine(Home(), ".config", "opencode");
                if (!Directory.Exists(home))
                {
                    if (o.Enabled) { Console.Error.WriteLine("opencode not detected (~/.config/opencode missing), skipped"); return false; }
                    Console.WriteLine("opencode not detected, nothing to remove");
                    return true;
                }
                string dir = Path.Combine(home, "plugins");
                string file = Path.Combine(dir, "agent_toast.js");
                if (!o.Enabled)
                {
                    if (File.Exists(file)) File.Delete(file);
                    Console.WriteLine("opencode plugin removed: " + file);
                    return true;
                }
                Directory.CreateDirectory(dir);
                TextFile.Write(file, OpencodePlugin.Replace("@@EXE@@", ConfigSafeExePath()));
                Console.WriteLine("opencode plugin written: " + file + " (restart opencode to load)");
                return true;
            }
            catch (Exception ex) { Console.Error.WriteLine("opencode apply failed: " + ex.Message); return false; }
        }

        // Single quotes only, so this verbatim string needs no escaping;
        // @@EXE@@ is replaced with the forward-slashed exe path.
        const string OpencodePlugin =
@"// agent_toast OpenCode plugin — managed file, rewritten on every apply.
// Fires agent_toast when a session goes idle (turn finished) or a subagent ends.
import { spawn } from 'node:child_process';

const EXE = '@@EXE@@';

function fire(hookEvent, sessionID) {
	try {
		const child = spawn(EXE, ['--agent', 'opencode'], { stdio: ['pipe', 'ignore', 'ignore'], windowsHide: true, detached: true });
		child.on('error', function () {});
		child.stdin.on('error', function () {});
		child.stdin.end(JSON.stringify({ session_id: sessionID || '', transcript_path: '', cwd: '', hook_event_name: hookEvent }) + '\n');
		child.unref();
	} catch (e) {}
}

export const AgentToastPlugin = async function ({ client }) {
	return {
		event: async function ({ event }) {
			if (event.type !== 'session.idle') return;
			const p = event.properties || {};
			const sessionID = p.sessionID || '';
			let isSub = false;
			if (sessionID && client && client.session) {
				try {
					const r = await client.session.get({ path: { id: sessionID } });
					isSub = !!(r && r.data && r.data.parentID);
				} catch (e) {}
			}
			fire(isSub ? 'SubagentStop' : 'Stop', sessionID);
		},
	};
};
";

    }

    // Installs the exe to a fixed per-user location so agent configs (which embed the
    // exe's absolute path) keep working when the user moves or deletes the download.
    public static class Installer
    {
        public static string InstallDir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "agent_toast");
        }

        public static string InstalledExePath()
        {
            return Path.Combine(InstallDir(), "agent_toast.exe");
        }

        public static bool IsInstalledLocation()
        {
            return string.Equals(AgentWriter.ExePath(), InstalledExePath(), StringComparison.OrdinalIgnoreCase);
        }

        static string ShortcutPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "agent_toast.lnk");
        }

        // Copy exe + settings + custom sounds to the fixed location, create a Start Menu
        // shortcut, then re-apply every enabled agent from the INSTALLED exe so all hook
        // configs get rewritten to the stable path.
        public static int Install()
        {
            try
            {
                string target = InstalledExePath();
                if (IsInstalledLocation())
                {
                    Console.WriteLine("already installed: " + target);
                    return ApplyEnabled();
                }
                Directory.CreateDirectory(InstallDir());
                string src = AgentWriter.ExePath();
                File.Copy(src, target, true);

                string settingsName = "agent_toast.settings.json";
                string srcSettings = Path.Combine(Path.GetDirectoryName(src), settingsName);
                string dstSettings = Path.Combine(InstallDir(), settingsName);
                if (File.Exists(srcSettings) && !File.Exists(dstSettings)) File.Copy(srcSettings, dstSettings);

                string srcSounds = Path.Combine(Path.GetDirectoryName(src), "sounds");
                if (Directory.Exists(srcSounds))
                {
                    string dstSounds = Path.Combine(InstallDir(), "sounds");
                    Directory.CreateDirectory(dstSounds);
                    foreach (string f in Directory.GetFiles(srcSounds))
                    {
                        string d = Path.Combine(dstSounds, Path.GetFileName(f));
                        if (!File.Exists(d)) File.Copy(f, d);
                    }
                }

                TryCreateShortcut(target);

                var psi = new ProcessStartInfo();
                psi.FileName = target;
                psi.Arguments = "--apply-enabled";
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                var p = Process.Start(psi);
                string output = p.StandardOutput.ReadToEnd();
                p.StandardError.ReadToEnd();
                p.WaitForExit(30000);
                if (output.Length > 0) Console.Write(output);

                Console.WriteLine("installed to: " + InstallDir());
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine("install failed: " + ex.Message); return 1; }
        }

        // Apply every agent that is enabled in settings. Used after install and as a
        // general "refresh all paths" command.
        public static int ApplyEnabled()
        {
            var cfg = ConfigStore.Load();
            int n = 0, ok = 0;
            foreach (string agent in new[] { "codex", "claude", "dsh", "opencode" })
            {
                AgentOptions o;
                if (!cfg.Agents.TryGetValue(agent, out o) || !o.Enabled) continue;
                n++;
                if (AgentWriter.Apply(agent)) ok++;
            }
            Console.WriteLine("applied " + ok + "/" + n + " enabled agent(s)");
            return ok == n ? 0 : 1;
        }

        // Remove every agent's hooks, the Start Menu shortcut, and the install directory.
        public static int Uninstall()
        {
            foreach (string agent in new[] { "codex", "claude", "dsh", "opencode" }) AgentWriter.Remove(agent);
            try { if (File.Exists(ShortcutPath())) File.Delete(ShortcutPath()); } catch { }
            if (IsInstalledLocation())
            {
                // A running exe cannot delete itself: schedule directory removal after exit.
                var psi = new ProcessStartInfo();
                psi.FileName = "cmd.exe";
                psi.Arguments = "/c ping 127.0.0.1 -n 3 >nul & rmdir /s /q \"" + InstallDir() + "\"";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                try { Process.Start(psi); } catch { }
                Console.WriteLine("uninstalled: hooks removed; " + InstallDir() + " will be deleted after this process exits");
            }
            else
            {
                try { if (Directory.Exists(InstallDir())) Directory.Delete(InstallDir(), true); } catch { }
                Console.WriteLine("hooks removed; install directory deleted (if present)");
            }
            return 0;
        }

        // Best effort: a Start Menu entry so users can find the settings GUI again.
        static void TryCreateShortcut(string targetExe)
        {
            try
            {
                string lnk = ShortcutPath().Replace("'", "''");
                string exe = targetExe.Replace("'", "''");
                string dir = InstallDir().Replace("'", "''");
                string script = "$s=(New-Object -ComObject WScript.Shell).CreateShortcut('" + lnk + "');" +
                                "$s.TargetPath='" + exe + "';$s.WorkingDirectory='" + dir + "';$s.Save()";
                var psi = new ProcessStartInfo();
                psi.FileName = "powershell";
                psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" + script + "\"";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                var p = Process.Start(psi);
                if (p != null) p.WaitForExit(10000);
            }
            catch { }
        }
    }
}