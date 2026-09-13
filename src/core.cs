// codex-toast core: toast window, style/sound engines, per-agent settings storage.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Media;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace CodexToast
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

    // ---------------- sounds ----------------
    public class SoundDef
    {
        public string Id;
        public string Name;
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

    public static class SoundEngine
    {
        public static void Play(string id)
        {
            if (string.IsNullOrEmpty(id) || id == "none") return;
            try
            {
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
        public bool SubEnabled = false;       // notify on subagent completion
        public string SubStyleId = "steam";
        public string SubSoundId = "none";
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
        public static string Path_() { return Path.Combine(Dir(), "codex-toast.settings.json"); }

        public static AppConfig Load()
        {
            var cfg = new AppConfig();
            try
            {
                string p = Path_();
                if (!File.Exists(p)) return cfg;
                var ser = new JavaScriptSerializer();
                var root = ser.Deserialize<Dictionary<string, object>>(File.ReadAllText(p));
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
                    o.SubEnabled = GetBool(d, "subEnabled", o.SubEnabled);
                    o.SubStyleId = GetStr(d, "subStyle", o.SubStyleId);
                    o.SubSoundId = GetStr(d, "subSound", o.SubSoundId);
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
                d["subEnabled"] = kv.Value.SubEnabled;
                d["subStyle"] = kv.Value.SubStyleId;
                d["subSound"] = kv.Value.SubSoundId;
                agents[kv.Key] = d;
            }
            File.WriteAllText(Path_(), ser.Serialize(root));
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
            string p = Environment.GetEnvironmentVariable("CODEX_TOAST_LOG");
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
                int others = System.Diagnostics.Process.GetProcessesByName("codex-toast").Length - 1;
                if (others > 0) stackOffset = others * (this.Height + 8);
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
        }
    }
}