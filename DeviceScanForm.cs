using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace GameAudioSwitcher
{
    /// <summary>
    /// 「扫描音频输出设备」窗口（面向新用户）：
    ///   打开即自动扫描本机全部渲染端点，并按设备名关键字自动推定「耳机 / 扬声器」；
    ///   用户可选中任意端点手动指派，或点「测试切换到所选设备」当场验证；
    ///   最后「保存并应用」写回 config.ini 并对当前运行的程序立即生效，无需重启。
    /// 目的：新用户不必再去「系统声音设置」里手工抄写设备名称。
    /// </summary>
    internal class DeviceScanForm : Form
    {
        private ListView _list;
        private CheckBox _onlyActive;
        private Label _infoLabel;
        private Label _statusLabel;
        private TextBox _headphoneBox;
        private TextBox _speakerBox;
        private Button _btnRescan;
        private Button _btnAuto;
        private Button _btnSetHeadphone;
        private Button _btnSetSpeaker;

        private List<AudioDeviceInfo> _devices = new List<AudioDeviceInfo>();

        private string _headphone = "";
        private string _speaker = "";
        private string _saveNotes = "";

        /// <summary>用户确认后的耳机设备名（DialogResult.OK 时有效）。</summary>
        public string HeadphoneName { get { return _headphone; } }

        /// <summary>用户确认后的扬声器设备名（DialogResult.OK 时有效）。</summary>
        public string SpeakerName { get { return _speaker; } }

        /// <summary>保存时的非阻断性提示（如设备当前未插入），供调用方写入日志。</summary>
        public string SaveNotes { get { return _saveNotes; } }

        public DeviceScanForm(string currentHeadphone, string currentSpeaker)
        {
            BuildUi();
            _headphoneBox.Text = currentHeadphone == null ? "" : currentHeadphone.Trim();
            _speakerBox.Text = currentSpeaker == null ? "" : currentSpeaker.Trim();

            // 构造阶段先扫一次，窗口出现时列表已就绪，避免先空后跳
            _devices = AudioCore.ScanRenderDevices();
            ReloadList(null);

            // 仅当配置中的名称在本机端点里完全找不到时才自动改写；
            // 设备只是「未插入 / 已拔出」不算异常，此时保留原有配置不动。
            bool hpMissing = _headphoneBox.Text.Length == 0 || FindScanned(_headphoneBox.Text) == null;
            bool spMissing = _speakerBox.Text.Length == 0 || FindScanned(_speakerBox.Text) == null;
            if (hpMissing || spMissing)
            {
                AutoDetect(true);
                SetStatus("配置中的设备在本机未找到，已自动重新识别，请确认后点「保存并应用」。");
            }
            else
            {
                SetStatus("点「自动识别」可重新推断；也可在上方列表选中一行后点「设为耳机 / 设为扬声器」。");
            }
        }

        // ============================== 界面 ==============================

        private void BuildUi()
        {
            this.Text = "扫描音频输出设备";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(660, 548);
            this.Font = new Font("Microsoft YaHei UI", 9F);
            this.BackColor = Color.White;

            try { this.Icon = AppIcon.LoadForForm(); }
            catch { }

            Label tip = new Label();
            tip.Text = "已自动扫描本机所有音频输出端点。选中一行后点「设为耳机 / 设为扬声器」即可完成配置；" +
                       "「自动识别」按设备名关键字一键推定。首次配置建议保存后用「测试切换」验证。";
            tip.SetBounds(16, 12, 628, 40);
            tip.ForeColor = Color.FromArgb(90, 90, 90);
            tip.Font = new Font("Microsoft YaHei UI", 8.5F);

            _onlyActive = new CheckBox();
            _onlyActive.Text = "只显示可用设备";
            _onlyActive.Checked = true;
            _onlyActive.SetBounds(16, 56, 180, 22);
            _onlyActive.CheckedChanged += delegate { ReloadList(SelectedName()); };

            _list = new ListView();
            _list.SetBounds(16, 82, 628, 276);
            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.MultiSelect = false;
            _list.HideSelection = false;
            _list.GridLines = false;
            _list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            _list.ShowItemToolTips = true;
            _list.Columns.Add("设备名称（Windows 声音设置中的名称）", 420);
            _list.Columns.Add("状态", 90);
            _list.Columns.Add("当前默认", 100);

            _btnRescan = new Button();
            _btnRescan.Text = "重新扫描";
            _btnRescan.SetBounds(16, 366, 96, 30);
            _btnRescan.Click += delegate
            {
                _devices = AudioCore.ScanRenderDevices();
                ReloadList(null);
                SetStatus("重新扫描完成：" + _devices.Count + " 个端点。");
            };

            _btnAuto = new Button();
            _btnAuto.Text = "自动识别";
            _btnAuto.SetBounds(118, 366, 96, 30);
            _btnAuto.Click += delegate { AutoDetect(true); };

            _infoLabel = new Label();
            _infoLabel.SetBounds(224, 371, 420, 22);
            _infoLabel.ForeColor = Color.FromArgb(120, 120, 120);
            _infoLabel.Font = new Font("Microsoft YaHei UI", 8.5F);

            Label hpLabel = new Label();
            hpLabel.Text = "耳机：";
            hpLabel.SetBounds(16, 408, 60, 24);

            _headphoneBox = new TextBox();
            _headphoneBox.SetBounds(78, 404, 410, 26);
            _headphoneBox.BackColor = Color.FromArgb(245, 247, 250);
            _headphoneBox.BorderStyle = BorderStyle.FixedSingle;

            _btnSetHeadphone = new Button();
            _btnSetHeadphone.Text = "设为耳机";
            _btnSetHeadphone.SetBounds(496, 403, 148, 28);
            _btnSetHeadphone.Click += delegate { AssignSelected(true); };

            Label spLabel = new Label();
            spLabel.Text = "扬声器：";
            spLabel.SetBounds(16, 440, 60, 24);

            _speakerBox = new TextBox();
            _speakerBox.SetBounds(78, 436, 410, 26);
            _speakerBox.BackColor = Color.FromArgb(245, 247, 250);
            _speakerBox.BorderStyle = BorderStyle.FixedSingle;

            _btnSetSpeaker = new Button();
            _btnSetSpeaker.Text = "设为扬声器";
            _btnSetSpeaker.SetBounds(496, 435, 148, 28);
            _btnSetSpeaker.Click += delegate { AssignSelected(false); };

            _statusLabel = new Label();
            _statusLabel.SetBounds(16, 472, 628, 20);
            _statusLabel.ForeColor = Color.FromArgb(120, 120, 120);
            _statusLabel.Font = new Font("Microsoft YaHei UI", 8.5F);

            Button btnTest = new Button();
            btnTest.Text = "测试切换到所选设备";
            btnTest.SetBounds(16, 500, 170, 32);
            btnTest.Click += delegate { TestSwitch(); };

            Button btnSave = new Button();
            btnSave.Text = "保存并应用";
            btnSave.SetBounds(434, 500, 100, 32);
            btnSave.Click += delegate { SaveAndClose(); };

            Button btnCancel = new Button();
            btnCancel.Text = "取消";
            btnCancel.SetBounds(544, 500, 100, 32);
            btnCancel.DialogResult = DialogResult.Cancel;

            this.Controls.Add(tip);
            this.Controls.Add(_onlyActive);
            this.Controls.Add(_list);
            this.Controls.Add(_btnRescan);
            this.Controls.Add(_btnAuto);
            this.Controls.Add(_infoLabel);
            this.Controls.Add(hpLabel);
            this.Controls.Add(_headphoneBox);
            this.Controls.Add(_btnSetHeadphone);
            this.Controls.Add(spLabel);
            this.Controls.Add(_speakerBox);
            this.Controls.Add(_btnSetSpeaker);
            this.Controls.Add(_statusLabel);
            this.Controls.Add(btnTest);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnCancel);

            this.CancelButton = btnCancel;
        }

        // ============================== 列表 ==============================

        private void ReloadList(string keepSelectionName)
        {
            _list.BeginUpdate();
            _list.Items.Clear();

            int activeTotal = 0;
            foreach (AudioDeviceInfo d in _devices) if (d.IsActive) activeTotal++;

            foreach (AudioDeviceInfo d in _devices)
            {
                if (_onlyActive.Checked && !d.IsActive) continue;

                ListViewItem item = new ListViewItem(d.Name);
                item.SubItems.Add(d.StateText);
                item.SubItems.Add(d.IsDefault ? "是" : "");
                item.Tag = d;
                if (!d.IsActive) item.ForeColor = Color.FromArgb(155, 155, 155);

                string tipText = "端点名称：" + d.BaseName;
                if (d.Id != null && d.Id.Length > 0) tipText += "\n端点 ID：" + d.Id;
                tipText += "\n状态：" + d.StateText + (d.IsDefault ? "\n当前系统默认输出设备" : "");
                item.ToolTipText = tipText;

                _list.Items.Add(item);
                if (keepSelectionName != null &&
                    d.Name.Equals(keepSelectionName, StringComparison.OrdinalIgnoreCase))
                {
                    item.Selected = true;
                    item.Focused = true;
                }
            }
            _list.EndUpdate();

            _infoLabel.Text = "共扫描到 " + _devices.Count + " 个端点，其中可用 " + activeTotal + " 个";
        }

        /// <summary>取当前选中行对应的设备，未选中返回 null。</summary>
        private AudioDeviceInfo SelectedDevice()
        {
            if (_list.SelectedItems.Count == 0) return null;
            return _list.SelectedItems[0].Tag as AudioDeviceInfo;
        }

        private string SelectedName()
        {
            AudioDeviceInfo d = SelectedDevice();
            return d == null ? null : d.Name;
        }

        /// <summary>把选中设备填入耳机或扬声器输入框。</summary>
        private void AssignSelected(bool asHeadphone)
        {
            AudioDeviceInfo d = SelectedDevice();
            if (d == null)
            {
                SetStatus("请先在上方列表中选中一个设备。");
                return;
            }

            string other;
            if (asHeadphone)
            {
                _headphoneBox.Text = d.Name;
                other = _speakerBox.Text.Trim();
            }
            else
            {
                _speakerBox.Text = d.Name;
                other = _headphoneBox.Text.Trim();
            }

            if (other.Length > 0 && other.Equals(d.Name, StringComparison.OrdinalIgnoreCase))
            {
                SetStatus("已填入「" + d.Name + "」，但耳机与扬声器当前是同一个设备，请改选另一个。");
                return;
            }

            string suffix = d.IsActive ? "" : "（注意：该设备当前状态为「" + d.StateText + "」，不可用）";
            SetStatus((asHeadphone ? "耳机" : "扬声器") + " 已设为「" + d.Name + "」" + suffix);
        }

        // ============================== 自动识别 ==============================

        private void AutoDetect(bool announce)
        {
            string hp;
            string sp;
            AudioCore.AutoDetectPair(_devices, out hp, out sp);

            if (hp != null) _headphoneBox.Text = hp;
            if (sp != null) _speakerBox.Text = sp;

            if (announce)
            {
                SetStatus("自动识别结果 —— 耳机：" + (hp == null ? "（未找到候选，请手动指定）" : hp) +
                          "；扬声器：" + (sp == null ? "（未找到候选，请手动指定）" : sp));
            }
        }

        // ============================== 测试切换 ==============================

        private void TestSwitch()
        {
            AudioDeviceInfo d = SelectedDevice();
            if (d == null)
            {
                SetStatus("请先在上方列表中选中要测试的设备。");
                return;
            }

            if (!d.IsActive)
            {
                MessageBox.Show(this,
                    "所选设备「" + d.Name + "」当前状态为「" + d.StateText + "」，无法切换。\n" +
                    "请确认设备已连接并在系统声音设置中处于启用状态。",
                    "测试切换", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Cursor = Cursors.WaitCursor;
            bool ok = AudioCore.SetDefaultDevice(d.Name);
            Cursor = Cursors.Default;

            if (ok)
            {
                _devices = AudioCore.ScanRenderDevices();
                ReloadList(d.Name);
                SetStatus("测试成功：系统默认输出已切换到「" + d.Name + "」。");
            }
            else
            {
                SetStatus("测试失败：无法切换到「" + d.Name + "」，请检查设备是否可用。");
                MessageBox.Show(this,
                    "切换到「" + d.Name + "」失败。\n\n可能原因：设备已被拔出、被其它程序独占，或音频服务（Audiosrv）未运行。",
                    "测试切换", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ============================== 保存 ==============================

        private void SaveAndClose()
        {
            string hp = _headphoneBox.Text.Trim();
            string sp = _speakerBox.Text.Trim();

            if (hp.Length == 0 || sp.Length == 0)
            {
                MessageBox.Show(this, "请分别指定「耳机」与「扬声器」对应的设备。\n"
                    + "可选中列表中的设备后点「设为耳机 / 设为扬声器」，或点「自动识别」。",
                    "扫描音频输出设备", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (hp.Equals(sp, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "耳机与扬声器不能是同一个设备，请改选另一个。",
                    "扫描音频输出设备", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<string> warnings = new List<string>();
            List<string> notes = new List<string>();
            AppendDeviceIssue(warnings, notes, hp, "耳机");
            AppendDeviceIssue(warnings, notes, sp, "扬声器");

            if (warnings.Count > 0)
            {
                string text = "以下设备存在问题，保存后可能导致自动切换无效：\n\n· "
                    + string.Join("\n· ", warnings.ToArray())
                    + "\n\n仍要保存吗？";
                if (MessageBox.Show(this, text, "扫描音频输出设备",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            _saveNotes = notes.Count == 0 ? "" : string.Join("；", notes.ToArray());
            _headphone = hp;
            _speaker = sp;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        /// <summary>
        /// 校验单个设备：名称在扫描结果中完全不存在、或端点被系统禁用 → 警告（需二次确认）；
        /// 仅「未插入 / 不存在」→ 只作提示（配置耳机时没插耳机是常态，不该拦）。
        /// </summary>
        private void AppendDeviceIssue(List<string> warnings, List<string> notes, string name, string role)
        {
            AudioDeviceInfo d = FindScanned(name);
            if (d == null)
            {
                warnings.Add(role + "「" + name + "」未出现在本次扫描结果中，名称可能不一致");
                return;
            }
            if ((d.State & (int)DevState.DISABLED) != 0)
            {
                warnings.Add(role + "「" + name + "」在系统中已被禁用，请在声音设置中启用");
                return;
            }
            if (!d.IsActive)
                notes.Add(role + "「" + name + "」当前" + d.StateText + "，接上后会自动生效");
        }

        // ============================== 匹配辅助 ==============================

        /// <summary>在扫描结果中按「原样名 / 归一化名」查找设备，未找到返回 null。</summary>
        private AudioDeviceInfo FindScanned(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string target = AudioCore.NormalizeDeviceName(name);
            foreach (AudioDeviceInfo d in _devices)
            {
                if (d.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return d;
                if (d.BaseName.Equals(target, StringComparison.OrdinalIgnoreCase)) return d;
            }
            return null;
        }

        private void SetStatus(string text)
        {
            if (_statusLabel != null) _statusLabel.Text = text;
        }
    }
}
