// agent_toast app: mode dispatch, notification mode, agent config writers.
using System;
using System.Collections.Generic;
using System.IO;
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
                    if (json.Contains("SubagentStop")) hookEvent = "SubagentStop";
                    else if (json.Contains("\"Stop\"")) hookEvent = "Stop";
                    var m = Regex.Match(json, "\"transcript_path\"\\s*:\\s*\"([^\"]+)\"");
                    if (m.Success) transcriptPath = m.Groups[1].Value.Replace("\\\\", "\\");
                }
            }

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
            if (title == null)
            {
                if (hookEvent == "SubagentStop")
                {
                    title = "Codex 子agent";
                    if (message == null) message = "子任务已完成";
                }
                else if (hookEvent == "Stop")
                {
                    string taskName = TaskName.FromTranscript(transcriptPath);
                    title = taskName != null ? taskName : "Codex";
                    if (message == null)
                    {
                        string custom = (o != null && !string.IsNullOrEmpty(o.TextMain)) ? o.TextMain : null;
                        message = custom != null ? custom : "任务已完成";
                    }
                }
                else { title = "Codex"; if (message == null) message = "Task finished"; }
            }if (message == null) message = "Task finished";

            Dbg.Log("transcript=" + transcriptPath + " event=" + hookEvent + " agent=" + args.Agent + " style=" + styleId + " sound=" + soundId + " title=" + title + " message=" + message);


            int durationMs = args.DurationMs > 0 ? args.DurationMs
                         : (o != null && o.DurationMs > 0 ? o.DurationMs : 2000);

            SoundEngine.Play(args.NoSound ? "none" : soundId);
            if (!args.NoToast)
            {
                Application.EnableVisualStyles();
                Application.Run(new ToastForm(StyleDef.Find(styleId), title, message, durationMs));
            }
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
            return agent == "codex" || agent == "claude" || agent == "opencode";
        }

        public static bool Apply(string agent)
        {
            if (!IsSupported(agent)) { Console.Error.WriteLine("unknown agent: " + agent); return false; }
            var cfg = ConfigStore.Load();
            AgentOptions o;
            if (!cfg.Agents.TryGetValue(agent, out o)) { o = cfg.For(agent); ConfigStore.Save(cfg); }
            if (agent == "codex") return ApplyCodex(o);
            if (agent == "claude") return ApplyClaude(o);
            // opencode: reserved, no stable hook config known yet.
            Console.WriteLine("opencode: reserved, nothing written");
            return true;
        }

        public static void Remove(string agent)
        {
            var cfg = ConfigStore.Load();
            cfg.For(agent).Enabled = false;
            ConfigStore.Save(cfg);
            if (agent == "codex") ApplyCodex(cfg.Agents[agent]);
            else if (agent == "claude") ApplyClaude(cfg.Agents[agent]);
        }

        // ---- codex: managed region inside ~/.codex/config.toml ----
        static bool ApplyCodex(AgentOptions o)
        {
            try
            {
                string path = Path.Combine(Home(), ".codex", "config.toml");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string text = File.Exists(path) ? TextFile.Read(path) : "";
                // drop our managed region
                text = Regex.Replace(text, @"(?s)# >>> (?:codex-toast|agent_toast) >>>.*?# <<< (?:codex-toast|agent_toast) <<<\r?\n?", "");
                // one-time migration: drop legacy unmarked hook blocks that point at us
                text = Regex.Replace(text,
                    @"(?ms)^\[\[hooks\.(Stop|SubagentStop)\]\]\r?\n.*?^\[\[hooks\.\1\.hooks\]\]\r?\ntype = ""command""\r?\ncommand = ""[^""]*(agent_toast|codex-toast|notify-hook)[^""]*""\r?\n",
                    "");
                if (o.Enabled)
                {
                    string cmd = ConfigSafeExePath() + " --agent codex";
                    string region = "# >>> agent_toast >>>\n\n[[hooks.Stop]]\nmatcher = \"\"\n\n[[hooks.Stop.hooks]]\ntype = \"command\"\ncommand = \"" + cmd + "\"\n";
                    if (o.SubEnabled)
                    {
                        region += "\n[[hooks.SubagentStop]]\nmatcher = \"\"\n\n[[hooks.SubagentStop.hooks]]\ntype = \"command\"\ncommand = \"" + cmd + "\"\n";
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

        // ---- claude: merge hooks into ~/.claude/settings.json ----
        static bool ApplyClaude(AgentOptions o)
        {
            try
            {
                string dir = Path.Combine(Home(), ".claude");
                Directory.CreateDirectory(dir);
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
                TextFile.Write(path, ser.Serialize(root));
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
    }
}