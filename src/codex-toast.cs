// codex-toast: lightweight notification popup for coding agents.
// Features: steam-style toast window (auto dismiss), notification sound,
// parses agent hook JSON from stdin (Codex/Claude style), pure CLI fallback.
//
// Usage:
//   codex-toast.exe [title] [message] [--duration ms] [--no-sound] [--no-toast]
//   echo {"hook_event_name":"Stop"} | codex-toast.exe
//
// Optional debug log: set env var CODEX_TOAST_LOG to a file path.

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Media;
using System.Windows.Forms;

public class CodexToast : Form
{
    public CodexToast(string title, string message, int durationMs)
    {
        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.Manual;
        this.ShowInTaskbar = false;
        this.TopMost = true;
        this.Size = new Size(360, 90);
        this.BackColor = Color.FromArgb(23, 26, 33);
        this.Opacity = 0;

        // Stack above other instances of this popup (steam-like behaviour).
        int stackOffset = 0;
        try
        {
            int others = Process.GetProcessesByName("codex-toast").Length - 1;
            if (others > 0) stackOffset = others * (this.Height + 8);
        }
        catch { }

        Rectangle wa = Screen.PrimaryScreen.WorkingArea;
        this.Location = new Point(wa.Right - this.Width - 12, wa.Bottom - this.Height - 12 - stackOffset);

        // Rounded corners.
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
        titleLbl.ForeColor = Color.FromArgb(102, 192, 244);
        titleLbl.AutoSize = false;
        titleLbl.Bounds = new Rectangle(14, 10, this.Width - 28, 24);

        Label msgLbl = new Label();
        msgLbl.Text = message;
        msgLbl.Font = new Font("Segoe UI", 9.5f);
        msgLbl.ForeColor = Color.FromArgb(220, 220, 220);
        msgLbl.AutoSize = false;
        msgLbl.Bounds = new Rectangle(14, 38, this.Width - 28, this.Height - 48);

        Panel accent = new Panel();
        accent.BackColor = Color.FromArgb(102, 192, 244);
        accent.Bounds = new Rectangle(0, 0, 4, this.Height);

        this.Controls.Add(titleLbl);
        this.Controls.Add(msgLbl);
        this.Controls.Add(accent);

        // Fade in.
        Timer fadeIn = new Timer();
        fadeIn.Interval = 15;
        fadeIn.Tick += (s, e) =>
        {
            this.Opacity += 0.12;
            if (this.Opacity >= 1) { this.Opacity = 1; fadeIn.Stop(); }
        };
        fadeIn.Start();

        // Life timer -> fade out -> close.
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
                if (this.Opacity <= 0) { this.Close(); }
            };
            fadeOut.Start();
        };
        life.Start();

        this.Click += (s, e) => this.Close();
    }

    private static void Log(string line)
    {
        string path = Environment.GetEnvironmentVariable("CODEX_TOAST_LOG");
        if (string.IsNullOrEmpty(path)) return;
        try { File.AppendAllText(path, DateTime.Now.ToString("o") + " " + line + "\n"); }
        catch { }
    }

    [STAThread]
    public static int Main(string[] args)
    {
        string title = null;
        string message = null;
        int durationMs = 2000;
        bool playSound = true;
        bool showToast = true;

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (a == "--duration" && i + 1 < args.Length) { int.TryParse(args[++i], out durationMs); }
            else if (a == "--no-sound") { playSound = false; }
            else if (a == "--no-toast") { showToast = false; }
            else if (title == null) { title = a; }
            else if (message == null) { message = a; }
        }

        // Read hook JSON from stdin only when it is piped (never block on a tty).
        string hookEvent = null;
        if (Console.IsInputRedirected)
        {
            try
            {
                string json = Console.In.ReadToEnd();
                if (!string.IsNullOrEmpty(json))
                {
                    if (json.Contains("SubagentStop")) hookEvent = "SubagentStop";
                    else if (json.Contains("\"Stop\"")) hookEvent = "Stop";
                }
            }
            catch { }
        }

        // Defaults per event (Chinese text kept as unicode escapes to survive any source encoding).
        if (title == null)
        {
            if (hookEvent == "SubagentStop") { title = "Codex \u5b50agent"; message = "\u5b50\u4efb\u52a1\u5df2\u5b8c\u6210"; }
            else if (hookEvent == "Stop") { title = "Codex"; message = "\u4efb\u52a1\u5df2\u5b8c\u6210"; }
            else { title = "Codex"; message = "Task finished"; }
        }
        if (message == null) message = "Task finished";

        Log("event=" + hookEvent + " title=" + title + " message=" + message + " duration=" + durationMs);

        if (playSound)
        {
            try { SystemSounds.Asterisk.Play(); } catch { }
        }

        if (!showToast) return 0;

        Application.EnableVisualStyles();
        Application.Run(new CodexToast(title, message, durationMs));
        return 0;
    }
}