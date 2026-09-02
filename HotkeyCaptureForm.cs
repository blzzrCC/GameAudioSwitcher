using System;
using System.Drawing;
using System.Windows.Forms;

namespace GameAudioSwitcher
{
    /// <summary>
    /// 快捷键捕获弹窗：聚焦输入框后直接按下组合键即捕获并预览。
    /// 规则：需包含 Ctrl/Alt/Shift 之一，或单独使用 F1-F24 功能键；不允许无修饰键的普通键。
    /// 确定后通过 Modifiers / Key 属性返回组合键。
    /// </summary>
    internal class HotkeyCaptureForm : Form
    {
        private TextBox _captureBox;
        private Label _tipLabel;
        private Button _okButton;
        private Button _cancelButton;

        private HotkeyModifiers _modifiers = HotkeyModifiers.None;
        private Keys _key = Keys.None;

        public HotkeyModifiers Modifiers { get { return _modifiers; } }
        public Keys Key { get { return _key; } }

        public HotkeyCaptureForm()
        {
            BuildUi();
        }

        public void SetCurrent(HotkeyModifiers modifiers, Keys key)
        {
            _modifiers = modifiers;
            _key = key;
            _captureBox.Text = GlobalHotkey.Format(modifiers, key);
        }

        private void BuildUi()
        {
            this.Text = "设置切换快捷键";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(420, 190);
            this.Font = new Font("Microsoft YaHei UI", 9F);
            this.BackColor = Color.White;

            Label title = new Label();
            title.Text = "请点击下方输入框，然后直接按下组合键";
            title.SetBounds(16, 12, 388, 24);
            title.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);

            _captureBox = new TextBox();
            _captureBox.ReadOnly = true;
            _captureBox.TextAlign = HorizontalAlignment.Center;
            _captureBox.Font = new Font("Microsoft YaHei UI", 15F);
            _captureBox.BorderStyle = BorderStyle.FixedSingle;
            _captureBox.BackColor = Color.FromArgb(245, 247, 250);
            _captureBox.SetBounds(16, 44, 388, 44);
            _captureBox.Text = "（按下组合键）";
            _captureBox.KeyDown += OnCaptureKeyDown;
            _captureBox.KeyUp += OnCaptureKeyUp;

            _tipLabel = new Label();
            _tipLabel.Text = "规则：需包含 Ctrl / Alt / Shift 之一，或单独使用 F1-F24 功能键（避免误触）";
            _tipLabel.SetBounds(16, 94, 388, 34);
            _tipLabel.ForeColor = Color.FromArgb(120, 120, 120);
            _tipLabel.Font = new Font("Microsoft YaHei UI", 8F);

            _okButton = new Button();
            _okButton.Text = "确定";
            _okButton.SetBounds(230, 148, 84, 30);
            _okButton.DialogResult = DialogResult.OK;

            _cancelButton = new Button();
            _cancelButton.Text = "取消";
            _cancelButton.SetBounds(320, 148, 84, 30);
            _cancelButton.DialogResult = DialogResult.Cancel;

            this.Controls.Add(title);
            this.Controls.Add(_captureBox);
            this.Controls.Add(_tipLabel);
            this.Controls.Add(_okButton);
            this.Controls.Add(_cancelButton);

            this.AcceptButton = _okButton;
            this.CancelButton = _cancelButton;
            this.Shown += delegate { _captureBox.Focus(); };
        }

        private void OnCaptureKeyDown(object sender, KeyEventArgs e)
        {
            Keys code = e.KeyCode & Keys.KeyCode;

            // Esc：交给窗体走取消逻辑
            if (code == Keys.Escape)
            {
                e.Handled = false;
                e.SuppressKeyPress = false;
                return;
            }

            // 纯修饰键本身：等待真正的功能键
            if (code == Keys.ShiftKey || code == Keys.ControlKey || code == Keys.Menu ||
                code == Keys.LWin || code == Keys.RWin)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            bool hasModifier = (e.Modifiers & (Keys.Control | Keys.Alt | Keys.Shift)) != Keys.None;
            bool isFunctionKey = (code >= Keys.F1 && code <= Keys.F24);

            if (!hasModifier && !isFunctionKey)
            {
                _tipLabel.ForeColor = Color.FromArgb(200, 60, 60);
                _tipLabel.Text = "该组合无效：需包含 Ctrl / Alt / Shift 之一，或单独使用 F1-F24";
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            _modifiers = HotkeyModifiers.None;
            if ((e.Modifiers & Keys.Control) != Keys.None) _modifiers |= HotkeyModifiers.Ctrl;
            if ((e.Modifiers & Keys.Alt) != Keys.None) _modifiers |= HotkeyModifiers.Alt;
            if ((e.Modifiers & Keys.Shift) != Keys.None) _modifiers |= HotkeyModifiers.Shift;
            _key = code;

            _captureBox.Text = GlobalHotkey.Format(_modifiers, _key);
            _tipLabel.ForeColor = Color.FromArgb(120, 120, 120);
            _tipLabel.Text = "按下「确定」保存并立即生效；如需更改请重新按键。";

            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void OnCaptureKeyUp(object sender, KeyEventArgs e)
        {
            // 防止 Ctrl/Alt 松开后被系统当作菜单快捷键
            e.Handled = true;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (this.DialogResult == DialogResult.OK && _key == Keys.None)
            {
                MessageBox.Show(this, "请先按下组合键（需包含 Ctrl/Alt/Shift，或单独使用 F1-F24）。",
                    "设置切换快捷键", MessageBoxButtons.OK, MessageBoxIcon.Information);
                e.Cancel = true;
                return;
            }
            base.OnFormClosing(e);
        }
    }
}
