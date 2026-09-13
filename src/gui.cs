// codex-toast settings GUI.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace CodexToast
{
    public class SettingsForm : Form
    {
        private static readonly string[] AgentIds = { "codex", "claude", "opencode" };
        private static readonly string[] AgentNames = { "Codex", "Claude Code", "OpenCode (预留)" };

        private RadioButton[] agentRads = new RadioButton[3];
        private string currentAgent = "codex";

        private CheckBox chkEnabled, chkSubEnabled;
        private ComboBox cmbStyle, cmbSound, cmbSubStyle, cmbSubSound;
        private TextBox txtMainText;
        private Label lblStatus;

        private AppConfig cfg;

        public SettingsForm()
        {
            this.Text = "codex-toast 设置";
            this.Size = new Size(520, 550);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            cfg = ConfigStore.Load();

            // --- agent selector ---
            Label lblAgent = new Label();
            lblAgent.Text = "选择 Agent：";
            lblAgent.Location = new Point(16, 16);
            lblAgent.AutoSize = true;

            for (int i = 0; i < 3; i++)
            {
                var rad = new RadioButton();
                rad.Text = AgentNames[i];
                rad.Tag = AgentIds[i];
                rad.Location = new Point(20 + i * 160, 40);
                rad.AutoSize = true;
                if (AgentIds[i] == currentAgent) rad.Checked = true;
                rad.CheckedChanged += AgentChanged;
                agentRads[i] = rad;
                this.Controls.Add(rad);
            }

            // --- main task group ---
            GroupBox gbMain = new GroupBox();
            gbMain.Text = "主任务提示";
            gbMain.Location = new Point(16, 74);
            gbMain.Size = new Size(472, 150);

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
            gbSub.Location = new Point(16, 234);
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

            Label l3 = new Label(); l3.Text = "弹窗样式："; l3.Location = new Point(14, 62); l3.AutoSize = true;
            Label l4 = new Label(); l4.Text = "提示音：";   l4.Location = new Point(14, 90); l4.AutoSize = true;

            gbSub.Controls.Add(chkSubEnabled);
            gbSub.Controls.Add(l3); gbSub.Controls.Add(l4);
            gbSub.Controls.Add(cmbSubStyle); gbSub.Controls.Add(cmbSubSound);

            // --- buttons ---
            Button btnOk = new Button();
            btnOk.Text = "确定";
            btnOk.Location = new Point(16, 370);
            btnOk.Size = new Size(110, 32);
            btnOk.Click += (s, e) => { SaveUiToOptions(currentAgent); ConfigStore.Save(cfg); SetStatus("设置已保存（未写入 agent 配置）"); };

            Button btnApply = new Button();
            btnApply.Text = "应用";
            btnApply.Location = new Point(138, 370);
            btnApply.Size = new Size(110, 32);
            btnApply.Click += ApplyClicked;

            Button btnTest = new Button();
            btnTest.Text = "测试";
            btnTest.Location = new Point(260, 370);
            btnTest.Size = new Size(110, 32);
            btnTest.Click += TestClicked;

            Button btnCancel = new Button();
            btnCancel.Text = "取消提示";
            btnCancel.Location = new Point(382, 370);
            btnCancel.Size = new Size(106, 32);
            btnCancel.Click += CancelClicked;

            lblStatus = new Label();
            lblStatus.Location = new Point(16, 422);
            lblStatus.Size = new Size(472, 60);
            lblStatus.ForeColor = Color.FromArgb(90, 90, 90);
            lblStatus.Text = "提示：应用 = 保存偏好并自动写入该 agent 的配置文件";

            this.Controls.Add(lblAgent);
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

        private void AgentChanged(object sender, EventArgs e)
        {
            var rad = sender as RadioButton;
            if (rad == null || !rad.Checked) return;
            SaveUiToOptions(currentAgent);          // persist edits of previous agent
            currentAgent = rad.Tag.ToString();
            LoadUiFromOptions(currentAgent);
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
            string title = currentAgent == "codex" ? "Codex \u6d4b\u8bd5" : "codex-toast \u6d4b\u8bd5";
            try
            {
                Process.Start(exe, "\"" + title + "\" \"\u6d4b\u8bd5\u5f39\u7a97\" 2500 --style " + style + " --sound " + sound);
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
                "codex-toast", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.Close();
        }

        private void SetStatus(string text)
        {
            lblStatus.Text = text + "\n\n设置文件：" + ConfigStore.Path_();
        }
    }
}