// agent_toast settings GUI.
using System;
using System.Diagnostics;
using System.IO;
using System.Drawing;
using System.Windows.Forms;

namespace AgentToast
{
    public class SettingsForm : Form
    {
        private static readonly string[] AgentIds = { "codex", "claude", "opencode" };
        private static readonly string[] AgentNames = { "Codex", "Claude Code", "OpenCode (预留)" };

        private MacTabs tabs;
        private string currentAgent = "codex";

        private CheckBox chkEnabled, chkSubEnabled;
        private ComboBox cmbStyle, cmbSound, cmbSubStyle, cmbSubSound;
        private TextBox txtMainText;
        private NumericUpDown numDuration;
        private Label lblStatus;

        private AppConfig cfg;
        private NotifyIcon trayIcon;
        private bool reallyExit = false;

        public SettingsForm()
        {
            this.Text = "agent_toast 设置";
            this.Size = new Size(520, 618);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.BackColor = Color.FromArgb(245, 245, 247);

            cfg = ConfigStore.Load();
            // --- tray: minimize -> taskbar, close (X) -> tray, real exit via tray menu ---
            trayIcon = new NotifyIcon();
            try { trayIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { trayIcon.Icon = SystemIcons.Application; }
            trayIcon.Text = "agent_toast";
            trayIcon.Visible = true;
            trayIcon.DoubleClick += (s, e) => ShowFromTray();
            var trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("打开设置", null, (s, e) => ShowFromTray());
            trayMenu.Items.Add("退出", null, (s, e) => ExitApp());
            trayIcon.ContextMenuStrip = trayMenu;

            // --- custom title bar: taller than native caption, holds icon + min/close ---
            Panel titleBar = new Panel();
            titleBar.Location = new Point(0, 0);
            titleBar.Size = new Size(520, 44);
            titleBar.BackColor = Color.FromArgb(245, 245, 247);
            titleBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.Controls.Add(titleBar);

            Panel barLine = new Panel();
            barLine.Location = new Point(0, 43);
            barLine.Size = new Size(520, 1);
            barLine.BackColor = Color.FromArgb(224, 224, 228);
            barLine.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            titleBar.Controls.Add(barLine);

            PictureBox tIcon = new PictureBox();
            tIcon.Location = new Point(10, 8);
            tIcon.Size = new Size(28, 28);
            tIcon.SizeMode = PictureBoxSizeMode.Zoom;
            try { tIcon.Image = Icon.ExtractAssociatedIcon(Application.ExecutablePath).ToBitmap(); } catch { }
            tIcon.MouseDown += TitleBarMouseDown;
            tIcon.DoubleClick += (s, e) => { this.WindowState = FormWindowState.Minimized; };
            titleBar.Controls.Add(tIcon);

            Label tLabel = new Label();
            tLabel.Text = "agent_toast 设置";
            tLabel.Location = new Point(46, 0);
            tLabel.Size = new Size(340, 44);
            tLabel.TextAlign = ContentAlignment.MiddleLeft;
            tLabel.Font = new Font("Segoe UI", 10f);
            tLabel.MouseDown += TitleBarMouseDown;
            tLabel.DoubleClick += (s, e) => { this.WindowState = FormWindowState.Minimized; };
            titleBar.Controls.Add(tLabel);

            titleBar.MouseDown += TitleBarMouseDown;
            titleBar.DoubleClick += (s, e) => { this.WindowState = FormWindowState.Minimized; };

            Button minBtn = MakeTitleButton("–", 404, titleBar);
            minBtn.Font = new Font("Segoe UI", 14f);
            minBtn.Click += (s, e) => { this.WindowState = FormWindowState.Minimized; };

            Button closeBtn = MakeTitleButton("×", 444, titleBar);
            closeBtn.Font = new Font("Segoe UI", 12f);
            closeBtn.Click += (s, e) => this.Close();
            closeBtn.MouseEnter += (s, e) => { closeBtn.BackColor = Color.FromArgb(232, 17, 35); closeBtn.ForeColor = Color.White; };
            closeBtn.MouseLeave += (s, e) => { closeBtn.BackColor = Color.Transparent; closeBtn.ForeColor = Color.FromArgb(60, 60, 64); };

            // --- agent selector (macOS style tabs) ---
            tabs = new MacTabs();
            tabs.Location = new Point(16, 56);
            tabs.Size = new Size(472, 34);
            tabs.AddTab("codex", "Codex");
            tabs.AddTab("claude", "Claude Code");
            tabs.AddTab("opencode", "OpenCode");
            tabs.SelectTab(currentAgent);
            tabs.SelectedIndexChanged += (s, e) =>
            {
                SaveUiToOptions(currentAgent);
                currentAgent = tabs.SelectedId;
                LoadUiFromOptions(currentAgent);
            };
            this.Controls.Add(tabs);
            // --- main task group ---
            GroupBox gbMain = new GroupBox();
            gbMain.Text = "主任务提示";
            gbMain.Location = new Point(16, 116);
            gbMain.Size = new Size(472, 176);

            chkEnabled = new CheckBox();
            chkEnabled.Text = "启用主任务完成提示";
            chkEnabled.Location = new Point(14, 24);
            chkEnabled.AutoSize = true;
            chkEnabled.CheckedChanged += (s, e) => UpdateEnabledState();

            cmbStyle = MakeCombo(StyleDef.All, new Point(100, 58));
            cmbSound = MakeCombo(null, new Point(100, 86));

            Label l1 = new Label(); l1.Text = "弹窗样式："; l1.Location = new Point(14, 62); l1.AutoSize = true;
            Label l2 = new Label(); l2.Text = "提示音：";   l2.Location = new Point(14, 90); l2.AutoSize = true;

            gbMain.Controls.Add(chkEnabled);
            gbMain.Controls.Add(l1); gbMain.Controls.Add(l2);
            gbMain.Controls.Add(cmbStyle); gbMain.Controls.Add(cmbSound);

            // --- subagent group ---
            GroupBox gbSub = new GroupBox();
            gbSub.Text = "子 agent 提示";
            gbSub.Location = new Point(16, 302);
            gbSub.Size = new Size(472, 120);

            chkSubEnabled = new CheckBox();
            chkSubEnabled.Text = "启用子 agent 完成提示";
            chkSubEnabled.Location = new Point(14, 24);
            chkSubEnabled.AutoSize = true;
            chkSubEnabled.CheckedChanged += (s, e) => UpdateEnabledState();

            cmbSubStyle = MakeCombo(StyleDef.All, new Point(100, 58));
            cmbSubSound = MakeCombo(null, new Point(100, 86));

            Button btnImport = new Button();
            btnImport.Text = "\u5bfc\u5165...";
            btnImport.Location = new Point(330, 82);
            btnImport.Size = new Size(72, 26);
            btnImport.Click += ImportClicked;
            gbMain.Controls.Add(btnImport);

            Label l5 = new Label();
            l5.Text = "完成文字：";
            l5.Location = new Point(14, 118);
            l5.AutoSize = true;

            txtMainText = new TextBox();
            txtMainText.Location = new Point(100, 114);
            txtMainText.Size = new Size(220, 24);
            txtMainText.MaxLength = 6;

            gbMain.Controls.Add(l5);
            gbMain.Controls.Add(txtMainText);
            Label l6 = new Label(); l6.Text = "显示时长："; l6.Location = new Point(14, 146); l6.AutoSize = true;
            numDuration = new NumericUpDown();
            numDuration.Location = new Point(100, 142);
            numDuration.Size = new Size(60, 24);
            numDuration.Minimum = 1; numDuration.Maximum = 60; numDuration.Value = 2;
            Label l7 = new Label(); l7.Text = "秒"; l7.Location = new Point(166, 146); l7.AutoSize = true;
            gbMain.Controls.Add(l6); gbMain.Controls.Add(numDuration); gbMain.Controls.Add(l7);

            Label l3 = new Label(); l3.Text = "弹窗样式："; l3.Location = new Point(14, 62); l3.AutoSize = true;
            Label l4 = new Label(); l4.Text = "提示音：";   l4.Location = new Point(14, 90); l4.AutoSize = true;

            gbSub.Controls.Add(chkSubEnabled);
            gbSub.Controls.Add(l3); gbSub.Controls.Add(l4);
            gbSub.Controls.Add(cmbSubStyle); gbSub.Controls.Add(cmbSubSound);

            // --- buttons ---
            Button btnOk = new Button();
            btnOk.Text = "确定";
            btnOk.Location = new Point(16, 438);
            btnOk.Size = new Size(110, 32);
            btnOk.Click += (s, e) => { SaveUiToOptions(currentAgent); ConfigStore.Save(cfg); SetStatus("设置已保存（未写入 agent 配置）"); };

            Button btnApply = new Button();
            btnApply.Text = "应用";
            btnApply.Location = new Point(138, 438);
            btnApply.Size = new Size(110, 32);
            btnApply.Click += ApplyClicked;

            Button btnTest = new Button();
            btnTest.Text = "测试";
            btnTest.Location = new Point(260, 438);
            btnTest.Size = new Size(110, 32);
            btnTest.Click += TestClicked;

            Button btnCancel = new Button();
            btnCancel.Text = "取消提示";
            btnCancel.Location = new Point(382, 438);
            btnCancel.Size = new Size(106, 32);
            btnCancel.Click += CancelClicked;

            lblStatus = new Label();
            lblStatus.Location = new Point(16, 490);
            lblStatus.Size = new Size(472, 60);
            lblStatus.ForeColor = Color.FromArgb(90, 90, 90);
            lblStatus.Text = "提示：应用 = 保存偏好并自动写入该 agent 的配置文件";

            this.Controls.Add(gbMain);
            this.Controls.Add(gbSub);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnApply);
            this.Controls.Add(btnTest);
            this.Controls.Add(btnCancel);
            this.Controls.Add(lblStatus);

            LoadUiFromOptions(currentAgent);
        }

        private ComboBox MakeCombo(System.Collections.IList items, Point loc)
        {
            ComboBox cmb = new ComboBox();
            cmb.DropDownStyle = ComboBoxStyle.DropDownList;
            cmb.Location = loc;
            cmb.Size = new Size(220, 24);
            if (items != null) foreach (var it in items) cmb.Items.Add(it);
            cmb.DisplayMember = "Name";
            cmb.ValueMember = "Id";
            return cmb;
        }

        private void FillSoundCombo(ComboBox cmb, string selectedId)
        {
            cmb.Items.Clear();
            foreach (var s in SoundDef.All) cmb.Items.Add(s);
            foreach (var s in CustomSounds.List()) cmb.Items.Add(s);
            SelectById(cmb, selectedId);
        }

        private void ImportClicked(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "WAV \u97f3\u9891 (*.wav)|*.wav";
            dlg.Title = "\u9009\u62e9\u63d0\u793a\u97f3\u6587\u4ef6";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                string dir = CustomSounds.Dir();
                string name = Path.GetFileName(dlg.FileName);
                string dest = Path.Combine(dir, name);
                int n = 1;
                while (File.Exists(dest))
                {
                    dest = Path.Combine(dir,
                        Path.GetFileNameWithoutExtension(name) + "_" + n + Path.GetExtension(name));
                    n++;
                }
                File.Copy(dlg.FileName, dest);
                SaveUiToOptions(currentAgent);
                string newId = "custom:" + Path.GetFileName(dest);
                FillSoundCombo(cmbSound, newId);
                FillSoundCombo(cmbSubSound, cfg.For(currentAgent).SubSoundId);
                SetStatus("\u5df2\u5bfc\u5165\uff1a" + Path.GetFileName(dest) + "\n\n\u81ea\u5b9a\u4e49\u6587\u4ef6\u4f4d\u4e8e\uff1a" + dir);
            }
            catch (Exception ex) { SetStatus("\u5bfc\u5165\u5931\u8d25\uff1a" + ex.Message); }
        }

        private void LoadUiFromOptions(string agent)
        {
            AgentOptions o;
            if (!cfg.Agents.TryGetValue(agent, out o)) o = new AgentOptions();
            chkEnabled.Checked = o.Enabled;
            chkSubEnabled.Checked = o.SubEnabled;
            SelectById(cmbStyle, o.StyleId);
            FillSoundCombo(cmbSound, o.SoundId);
            txtMainText.Text = o.TextMain;
            numDuration.Value = Math.Max(1, Math.Min(60, o.DurationMs / 1000));
            SelectById(cmbSubStyle, o.SubStyleId);
            FillSoundCombo(cmbSubSound, o.SubSoundId);
            UpdateEnabledState();
        }

        private void SaveUiToOptions(string agent)
        {
            AgentOptions o = cfg.For(agent);
            o.Enabled = chkEnabled.Checked;
            o.SubEnabled = chkSubEnabled.Checked;
            o.StyleId = (cmbStyle.SelectedItem as StyleDef).Id;
            o.SoundId = (cmbSound.SelectedItem as SoundDef).Id;
            o.TextMain = txtMainText.Text.Trim();
            o.DurationMs = (int)numDuration.Value * 1000;
            o.SubStyleId = (cmbSubStyle.SelectedItem as StyleDef).Id;
            o.SubSoundId = (cmbSubSound.SelectedItem as SoundDef).Id;
        }

        private void SelectById(ComboBox cmb, string id)
        {
            for (int i = 0; i < cmb.Items.Count; i++)
            {
                var item = cmb.Items[i];
                string itemId = (item is StyleDef) ? ((StyleDef)item).Id : ((SoundDef)item).Id;
                if (itemId == id) { cmb.SelectedIndex = i; return; }
            }
            if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
        }

        private void UpdateEnabledState()
        {
            cmbStyle.Enabled = cmbSound.Enabled = chkEnabled.Checked;
            cmbSubStyle.Enabled = cmbSubSound.Enabled = chkSubEnabled.Checked;
        }

        private void ApplyClicked(object sender, EventArgs e)
        {
            SaveUiToOptions(currentAgent);
            ConfigStore.Save(cfg);
            if (currentAgent == "opencode")
            {
                SetStatus("OpenCode 支持即将推出：偏好已保存，未写入配置。");
                return;
            }
            bool ok = AgentWriter.Apply(currentAgent);
            SetStatus(ok ? "已应用：" + currentAgent + " 的配置文件已更新，新会话生效。"
                         : "应用失败，请查看 " + ConfigStore.Path_() + " 附近日志。");
        }

        private void TestClicked(object sender, EventArgs e)
        {
            string exe = AgentWriter.ExePath();
            string style = (cmbStyle.SelectedItem as StyleDef).Id;
            string sound = (cmbSound.SelectedItem as SoundDef).Id;
            string title = currentAgent == "codex" ? "Codex \u6d4b\u8bd5" : "agent_toast \u6d4b\u8bd5";
            try
            {
                Process.Start(exe, "\"" + title + "\" \"\u6d4b\u8bd5\u5f39\u7a97\"" + " --duration " + ((int)numDuration.Value * 1000) + " --style " + style + " --sound " + sound);
            }
            catch (Exception ex) { SetStatus("\u6d4b\u8bd5\u5931\u8d25\uff1a" + ex.Message); }
        }

        private void CancelClicked(object sender, EventArgs e)
        {
            AgentOptions o = cfg.For(currentAgent);
            o.Enabled = false;
            o.SubEnabled = false;
            ConfigStore.Save(cfg);
            if (currentAgent != "opencode") AgentWriter.Apply(currentAgent);
            MessageBox.Show(this, currentAgent + " \u7684\u63d0\u793a\u5df2\u5173\u95ed\uff0c\u76f8\u5173\u914d\u7f6e\u5df2\u79fb\u9664\u3002",
                "agent_toast", MessageBoxButtons.OK, MessageBoxIcon.Information);
            reallyExit = true;
            this.Close();
        }

        // --- custom title bar helpers ---
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

        private void TitleBarMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, 0xA1, (IntPtr)0x2, IntPtr.Zero); // WM_NCLBUTTONDOWN, HTCAPTION
            }
        }

        private Button MakeTitleButton(string text, int x, Panel bar)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(x, 4);
            b.Size = new Size(36, 36);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = Color.Transparent;
            b.ForeColor = Color.FromArgb(60, 60, 64);
            b.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            b.MouseEnter += (s, e) => { if (b.BackColor != Color.FromArgb(232, 17, 35)) b.BackColor = Color.FromArgb(229, 229, 234); };
            b.MouseLeave += (s, e) => { if (b.ForeColor != Color.White) b.BackColor = Color.Transparent; };
            bar.Controls.Add(b);
            return b;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Alt | Keys.F4)) { this.Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // --- tray behaviors ---
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!reallyExit)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }
            base.OnClosing(e);
        }

        private void HideToTray()
        {
            Hide();
            trayIcon.ShowBalloonTip(1200, "agent_toast", "已缩小到托盘，双击图标恢复设置界面。", ToolTipIcon.Info);
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void ExitApp()
        {
            reallyExit = true;
            if (trayIcon != null) trayIcon.Visible = false;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && trayIcon != null) { trayIcon.Dispose(); trayIcon = null; }
            base.Dispose(disposing);
        }

        private void SetStatus(string text)
        {
            lblStatus.Text = text + "\n\n设置文件：" + ConfigStore.Path_();
        }
    }
}