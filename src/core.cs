// agent_toast core: toast window, style/sound engines, per-agent settings storage.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Media;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace AgentToast
{
    // ---------------- styles ----------------
    public class StyleDef
    {
        public string Id;
        public string Name;
        public Color BackColor;
        public Color TitleColor;
        public Color TextColor;
        public Color AccentColor;
        public bool ShowAccent;
        public int Width;
        public int Height;

        public override string ToString() { return Name; }

        public static readonly List<StyleDef> All = new List<StyleDef>
        {
            new StyleDef { Id = "steam",   Name = "Steam 深色", BackColor = Color.FromArgb(23,26,33),  TitleColor = Color.FromArgb(102,192,244), TextColor = Color.FromArgb(220,220,220), AccentColor = Color.FromArgb(102,192,244), ShowAccent = true,  Width = 360, Height = 90 },
            new StyleDef { Id = "light",   Name = "浅色清爽",   BackColor = Color.FromArgb(250,250,250), TitleColor = Color.FromArgb(46,125,50),  TextColor = Color.FromArgb(40,40,40),   AccentColor = Color.FromArgb(76,175,80),  ShowAccent = true,  Width = 360, Height = 90 },
            new StyleDef { Id = "minimal", Name = "极简暗色",   BackColor = Color.FromArgb(30,30,30),  TitleColor = Color.FromArgb(200,200,200), TextColor = Color.FromArgb(160,160,160), AccentColor = Color.Black,                 ShowAccent = false, Width = 300, Height = 70 },
        };

        public static StyleDef Find(string id)
        {
            foreach (var s in All) if (s.Id == id) return s;
            return All[0];
        }
    }

    // ---------------- encoding-safe file IO ----------------
    // Files we own are written as UTF-8 with BOM. On read, prefer UTF-8 but
    // fall back to the system codepage for legacy files written before this fix.
    public static class TextFile
    {
        static readonly System.Text.UTF8Encoding Utf8Strict = new System.Text.UTF8Encoding(false, true);
        static readonly System.Text.Encoding Utf8Bom = new System.Text.UTF8Encoding(true);

        public static string Decode(byte[] bytes)
        {
            string s;
            try { s = Utf8Strict.GetString(bytes); }
            catch { s = System.Text.Encoding.Default.GetString(bytes); }
            if (s.Length > 0 && s[0] == '\uFEFF') s = s.Substring(1); // strip BOM
            return s;
        }

        public static string Read(string path) { return Decode(File.ReadAllBytes(path)); }

        public static void Write(string path, string text) { File.WriteAllText(path, text, Utf8Bom); }
    }

    // ---------------- task name from transcript ----------------
    public static class TaskName
    {
        // Extract the last real user message from a Codex rollout JSONL file.
        public static string FromTranscript(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                string last = null;
                var ser = new JavaScriptSerializer();
                // The agent may still hold the transcript file open: read with share flags.
                string raw;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var ms = new MemoryStream())
                {
                    fs.CopyTo(ms);
                    raw = TextFile.Decode(ms.ToArray());
                }
                // Decode as UTF-8 when valid, else fall back to the system codepage.
                string[] lines = raw.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
                foreach (string line in lines)
                {
                    if (line.IndexOf("response_item") < 0) continue;
                    if (line.IndexOf("\"user\"") < 0) continue;
                    try
                    {
                        var obj = ser.Deserialize<Dictionary<string, object>>(line);
                        object payloadObj;
                        if (!obj.TryGetValue("payload", out payloadObj)) continue;
                        var payload = payloadObj as Dictionary<string, object>;
                        if (payload == null) continue;
                        object roleObj;
                        if (!payload.TryGetValue("role", out roleObj)) continue;
                        if (!"user".Equals(roleObj as string)) continue;
                        object contentObj;
                        if (!payload.TryGetValue("content", out contentObj)) continue;
                        var content = contentObj as System.Collections.IList;
                        if (content == null || content.Count == 0) continue;
                        var c0 = content[0] as Dictionary<string, object>;
                        if (c0 == null) continue;
                        object textObj;
                        if (!c0.TryGetValue("text", out textObj)) continue;
                        string text = textObj as string;
                        if (string.IsNullOrEmpty(text)) continue;
                        if (text.StartsWith("<environment_context>")) continue;
                        last = text;
                    }
                    catch { }
                }
                if (string.IsNullOrEmpty(last)) return null;
                last = last.Replace("\r", " ").Replace("\n", " ").Trim();
                if (last.Length > 20) last = last.Substring(0, 20) + "...";
                return last;
            }
            catch { return null; }
        }
    }
    // ---------------- sounds ----------------
    public class SoundDef
    {
        public string Id;
        public string Name;

        public override string ToString() { return Name; }

        public static readonly List<SoundDef> All = new List<SoundDef>
        {
            new SoundDef { Id = "none",        Name = "无" },
            new SoundDef { Id = "asterisk",    Name = "叮 (系统提示)" },
            new SoundDef { Id = "beep",        Name = "哔 (蜂鸣)" },
            new SoundDef { Id = "exclamation", Name = "感叹号" },
        };
        public static string NameOf(string id)
        {
            foreach (var s in All) if (s.Id == id) return s.Name;
            return id;
        }
    }

    // Custom user-imported sounds, stored as .wav/.mp3 files in "sounds/" next to the exe.
    public static class CustomSounds
    {
        public static string Dir()
        {
            string d = Path.Combine(ConfigStore.Dir(), "sounds");
            try { Directory.CreateDirectory(d); } catch { }
            return d;
        }

        public static List<SoundDef> List()
        {
            var list = new List<SoundDef>();
            try
            {
                foreach (var f in Directory.GetFiles(Dir(), "*.*"))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext != ".wav" && ext != ".mp3") continue;
                    list.Add(new SoundDef { Id = "custom:" + Path.GetFileName(f), Name = Path.GetFileName(f) + " (自定义)" });
                }
            }
            catch { }
            return list;
        }
    }

    
public static class SoundEngine
    {
        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        static extern int mciSendString(string command, System.Text.StringBuilder returnValue, int returnLength, IntPtr winHandle);

        // Play an mp3 through MCI (Windows decodes mp3 via DirectShow; works even when Media Foundation sources are unavailable).
        // A background thread closes the MCI device once playback stops so the long-lived GUI process does not leak devices.
        static void PlayMp3(string path)
        {
            string alias = "at" + System.Diagnostics.Process.GetCurrentProcess().Id +
                           Guid.NewGuid().ToString("N").Substring(0, 8);
            string open = "open \"" + path + "\" type mpegvideo alias " + alias;
            if (mciSendString(open, null, 0, IntPtr.Zero) != 0) { Dbg.Log("mci open failed: " + path); return; }
            Dbg.Log("mci playing: " + path);
            if (mciSendString("play " + alias, null, 0, IntPtr.Zero) != 0) return;
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var sb = new System.Text.StringBuilder(64);
                    for (int i = 0; i < 600; i++) // up to 60s
                    {
                        System.Threading.Thread.Sleep(100);
                        sb.Length = 0;
                        if (mciSendString("status " + alias + " mode", sb, 64, IntPtr.Zero) != 0) return; // device gone
                        if (sb.ToString().IndexOf("stopped", StringComparison.OrdinalIgnoreCase) >= 0) break;
                    }
                    mciSendString("close " + alias, null, 0, IntPtr.Zero);
                }
                catch { }
            });
        }


        public static void Play(string id)
        {
            if (string.IsNullOrEmpty(id) || id == "none") return;
            try
            {
                if (id.StartsWith("custom:"))
                {
                    string path = Path.Combine(CustomSounds.Dir(), id.Substring(7));
                    if (File.Exists(path))
                    {
                        if (Path.GetExtension(path).Equals(".mp3", StringComparison.OrdinalIgnoreCase))
                        {
                            PlayMp3(path);
                        }
                        else
                        {
                            var player = new SoundPlayer(path);
                            player.Play();
                        }
                    }
                    return;
                }
                if (id == "asterisk") SystemSounds.Asterisk.Play();
                else if (id == "exclamation") SystemSounds.Exclamation.Play();
                else if (id == "beep") System.Threading.Tasks.Task.Run(() =>
                {
                    try { Console.Beep(1200, 180); } catch { }
                });
            }
            catch { }
        }
    }

    // ---------------- per-agent options ----------------
    public class AgentOptions
    {
        public bool Enabled = false;          // master switch for this agent
        public string StyleId = "steam";      // main-task toast style
        public string SoundId = "asterisk";   // main-task sound
        public string TextMain = "任务已完成"; // main-task popup body, max 6 chars
        public bool SubEnabled = false;       // notify on subagent completion
        public string SubStyleId = "steam";
        public string SubSoundId = "none";
        public int DurationMs = 2000;    // toast display duration for this agent
    }

    public class AppConfig
    {
        public Dictionary<string, AgentOptions> Agents = new Dictionary<string, AgentOptions>();

        public AgentOptions For(string agent)
        {
            AgentOptions o;
            if (!Agents.TryGetValue(agent, out o)) { o = new AgentOptions(); Agents[agent] = o; }
            return o;
        }
    }

    public static class ConfigStore
    {
        public static string Dir()
        {
            try { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }
            catch { return "."; }
        }
        public static string Path_() { return Path.Combine(Dir(), "agent_toast.settings.json"); }

        public static AppConfig Load()
        {
            var cfg = new AppConfig();
            try
            {
                string p = Path_();
                if (!File.Exists(p)) return cfg;
                var ser = new JavaScriptSerializer();
                var root = ser.Deserialize<Dictionary<string, object>>(TextFile.Read(p));
                if (root == null || !root.ContainsKey("agents")) return cfg;
                var agents = root["agents"] as Dictionary<string, object>;
                if (agents == null) return cfg;
                foreach (var kv in agents)
                {
                    var d = kv.Value as Dictionary<string, object>;
                    if (d == null) continue;
                    var o = cfg.For(kv.Key);
                    o.Enabled    = GetBool(d, "enabled", o.Enabled);
                    o.StyleId    = GetStr(d, "style", o.StyleId);
                    o.SoundId    = GetStr(d, "sound", o.SoundId);
                    o.TextMain   = GetStr(d, "text", o.TextMain);
                    if (o.TextMain.Length > 6) o.TextMain = o.TextMain.Substring(0, 6);
                    o.SubEnabled = GetBool(d, "subEnabled", o.SubEnabled);
                    o.SubStyleId = GetStr(d, "subStyle", o.SubStyleId);
                    o.SubSoundId = GetStr(d, "subSound", o.SubSoundId);
                    o.DurationMs = GetInt(d, "durationMs", 2000);
                    if (o.DurationMs < 500 || o.DurationMs > 60000) o.DurationMs = 2000;
                }
            }
            catch { }
            return cfg;
        }

        public static void Save(AppConfig cfg)
        {
            var ser = new JavaScriptSerializer();
            var root = new Dictionary<string, object>();
            root["version"] = 1;
            var agents = new Dictionary<string, object>();
            root["agents"] = agents;
            foreach (var kv in cfg.Agents)
            {
                var d = new Dictionary<string, object>();
                d["enabled"] = kv.Value.Enabled;
                d["style"] = kv.Value.StyleId;
                d["sound"] = kv.Value.SoundId;
                d["text"] = kv.Value.TextMain;
                d["subEnabled"] = kv.Value.SubEnabled;
                d["subStyle"] = kv.Value.SubStyleId;
                d["subSound"] = kv.Value.SubSoundId;
                d["durationMs"] = kv.Value.DurationMs;
                agents[kv.Key] = d;
            }
            TextFile.Write(Path_(), ser.Serialize(root));
        }

        private static bool GetBool(Dictionary<string, object> d, string k, bool def)
        {
            object v;
            if (d.TryGetValue(k, out v))
            {
                if (v is bool) return (bool)v;
                bool b; if (bool.TryParse(v.ToString(), out b)) return b;
            }
            return def;
        }
        private static int GetInt(Dictionary<string, object> d, string k, int def)
        {
            object v;
            if (d.TryGetValue(k, out v))
            {
                int n; if (int.TryParse(v.ToString(), out n)) return n;
            }
            return def;
        }
        private static string GetStr(Dictionary<string, object> d, string k, string def)
        {
            object v;
            if (d.TryGetValue(k, out v) && v != null) return v.ToString();
            return def;
        }
    }

    // ---------------- debug log ----------------
    public static class Dbg
    {
        public static void Log(string line)
        {
            string p = Environment.GetEnvironmentVariable("AGENT_TOAST_LOG");
            if (string.IsNullOrEmpty(p)) return;
            try { File.AppendAllText(p, DateTime.Now.ToString("o") + " " + line + "\n"); }
            catch { }
        }
    }

    // ---------------- the toast window ----------------
    public class ToastForm : Form
    {
        public ToastForm(StyleDef style, string title, string message, int durationMs)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.Size = new Size(style.Width, style.Height);
            this.BackColor = style.BackColor;
            this.Opacity = 0;

            int stackOffset = 0;
            try
            {
                int popups = 0;
                foreach (var p in System.Diagnostics.Process.GetProcessesByName("agent_toast"))
                {
                    string t = "";
                    try { t = p.MainWindowTitle; } catch { }
                    if (t.IndexOf("agent_toast", StringComparison.OrdinalIgnoreCase) >= 0) continue; // settings GUI
                    popups++;
                }
                popups--; // exclude self
                if (popups > 0) stackOffset = popups * (this.Height + 8);
            }
            catch { }

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(wa.Right - this.Width - 12, wa.Bottom - this.Height - 12 - stackOffset);

            GraphicsPath path = new GraphicsPath();
            int r = 8;
            path.AddArc(0, 0, r * 2, r * 2, 180, 90);
            path.AddArc(this.Width - r * 2, 0, r * 2, r * 2, 270, 90);
            path.AddArc(this.Width - r * 2, this.Height - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(0, this.Height - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            this.Region = new Region(path);

            Label titleLbl = new Label();
            titleLbl.Text = title;
            titleLbl.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            titleLbl.ForeColor = style.TitleColor;
            titleLbl.AutoSize = false;
            titleLbl.Bounds = new Rectangle(14, 8, this.Width - 28, 24);

            Label msgLbl = new Label();
            msgLbl.Text = message;
            msgLbl.Font = new Font("Segoe UI", 9.5f);
            msgLbl.ForeColor = style.TextColor;
            msgLbl.AutoSize = false;
            msgLbl.Bounds = new Rectangle(14, 36, this.Width - 28, this.Height - 46);

            this.Controls.Add(titleLbl);
            this.Controls.Add(msgLbl);

            if (style.ShowAccent)
            {
                Panel accent = new Panel();
                accent.BackColor = style.AccentColor;
                accent.Bounds = new Rectangle(0, 0, 4, this.Height);
                this.Controls.Add(accent);
            }

            Timer fadeIn = new Timer();
            fadeIn.Interval = 15;
            fadeIn.Tick += (s, e) =>
            {
                this.Opacity += 0.12;
                if (this.Opacity >= 1) { this.Opacity = 1; fadeIn.Stop(); }
            };
            fadeIn.Start();

            Timer life = new Timer();
            life.Interval = durationMs;
            life.Tick += (s, e) =>
            {
                life.Stop();
                Timer fadeOut = new Timer();
                fadeOut.Interval = 15;
                fadeOut.Tick += (s2, e2) =>
                {
                    this.Opacity -= 0.08;
                    if (this.Opacity <= 0) this.Close();
                };
                fadeOut.Start();
            };
            life.Start();

            this.Click += (s, e) => this.Close();
            Dbg.Log("toast shown: " + title);
        }

        // Never activate / steal keyboard focus (typing in other apps must not be interrupted).
        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_NOACTIVATE = 0x08000000;
                const int WS_EX_TOOLWINDOW = 0x00000080;
                const int WS_EX_TOPMOST    = 0x00000008;
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
                return cp;
            }
        }
    }
}