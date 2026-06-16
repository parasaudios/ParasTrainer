using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace ParasTrainer
{
    public static class Theme
    {
        public static readonly Color BG_SIDEBAR = Color.FromArgb(8, 8, 20);
        public static readonly Color BG_SIDEBAR_CARD = Color.FromArgb(18, 18, 36);
        public static readonly Color BG_SIDEBAR_HOVER = Color.FromArgb(24, 24, 46);
        public static readonly Color BG_SIDEBAR_SEL = Color.FromArgb(30, 30, 55);
        public static readonly Color BG_MAIN = Color.FromArgb(11, 11, 25);
        public static readonly Color BG_HEADER = Color.FromArgb(14, 14, 32);
        public static readonly Color BG_CARD = Color.FromArgb(22, 22, 42);
        public static readonly Color BG_INPUT = Color.FromArgb(30, 30, 52);
        public static readonly Color BG_BTN_HOVER = Color.FromArgb(38, 38, 62);
        public static readonly Color TEXT_PRIMARY = Color.FromArgb(232, 232, 242);
        public static readonly Color TEXT_SECONDARY = Color.FromArgb(130, 130, 160);
        public static readonly Color TEXT_DISABLED = Color.FromArgb(70, 70, 95);
        public static readonly Color BORDER = Color.FromArgb(36, 36, 58);
        public static readonly Color BORDER_CARD = Color.FromArgb(42, 42, 68);
        public static readonly Color STATUS_RED = Color.FromArgb(220, 60, 60);
        public static readonly Color ACCENT_AMBER = Color.FromArgb(245, 158, 11);
        public static readonly Color ACCENT_GREEN = Color.FromArgb(74, 222, 128);
        public static readonly Color HK_BG = Color.FromArgb(30, 30, 52);
        public static readonly Color HK_BORDER = Color.FromArgb(52, 52, 78);
        public static readonly Color HK_TEXT = Color.FromArgb(105, 105, 135);
        public static readonly Color HK_EDIT_BG = Color.FromArgb(100, 30, 30);

        public const int ROW_H = 42;
        public const int PAD = 14;
        public const int CARD_W = 488;

        public static GraphicsPath RoundRect(float x, float y, float w, float h, float r)
        {
            GraphicsPath p = new GraphicsPath();
            p.AddArc(x, y, r * 2, r * 2, 180, 90);
            p.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
            p.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
            p.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    public class ToggleSwitch : Control
    {
        public event EventHandler Toggled;
        private bool _checked, _hovered;
        private Color _accentColor = Color.FromArgb(110, 90, 220);

        public Color AccentColor { get { return _accentColor; } set { _accentColor = value; Invalidate(); } }

        public bool Checked
        {
            get { return _checked; }
            set { if (_checked != value) { _checked = value; Invalidate(); } }
        }

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(44, 24);
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color track = _checked ? _accentColor
                : _hovered ? Color.FromArgb(58, 58, 85)
                : Color.FromArgb(42, 42, 66);
            using (SolidBrush b = new SolidBrush(track))
                g.FillPath(b, Theme.RoundRect(0, 0, Width - 1, Height - 1, Height / 2));
            if (_checked)
                using (Pen p = new Pen(Color.FromArgb(40, _accentColor), 3f))
                    g.DrawPath(p, Theme.RoundRect(0, 0, Width - 1, Height - 1, Height / 2));
            int thumbX = _checked ? Width - Height + 3 : 3;
            using (SolidBrush b = new SolidBrush(Color.White))
                g.FillEllipse(b, thumbX, 3, Height - 6, Height - 6);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        { _checked = !_checked; Invalidate(); if (Toggled != null) Toggled(this, EventArgs.Empty); }
        protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); }
    }

    public class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
    }

    public class PluginHost
    {
        public Color AccentColor;
        public Color AccentDimColor;
        public string IpcDir;
        public string CmdDir;

        public Dictionary<string, bool> FeatureStates = new Dictionary<string, bool>();
        public Dictionary<string, ToggleSwitch> ToggleSwitches = new Dictionary<string, ToggleSwitch>();
        public Dictionary<string, Label> FeatureLabels = new Dictionary<string, Label>();
        public Dictionary<string, Keys> HotkeyBindings = new Dictionary<string, Keys>();
        public Dictionary<string, Button> HotkeyButtons = new Dictionary<string, Button>();
        public HashSet<TextBox> DirtyInputs = new HashSet<TextBox>();

        public void SendCommand(string cmd)
        {
            try
            {
                Directory.CreateDirectory(CmdDir);
                string fn = "cmd_" + DateTime.UtcNow.Ticks + ".cmd";
                File.WriteAllText(Path.Combine(CmdDir, fn), cmd);
            }
            catch { }
        }

        public void SyncToggle(string feature, bool state)
        {
            if (!ToggleSwitches.ContainsKey(feature)) return;
            FeatureStates[feature] = state;
            ToggleSwitches[feature].Checked = state;
            if (FeatureLabels.ContainsKey(feature))
                FeatureLabels[feature].ForeColor = state ? AccentColor : Theme.TEXT_PRIMARY;
        }

        public int ColorSectionHeader(Panel parent, int y, string text, Color color)
        {
            Label lbl = new Label();
            lbl.Text = text;
            lbl.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            lbl.ForeColor = color;
            lbl.Location = new Point(Theme.PAD, y);
            lbl.AutoSize = true;
            parent.Controls.Add(lbl);
            return y + 28;
        }

        public int SectionHeader(Panel parent, int y, string text)
        {
            Label lbl = new Label();
            lbl.Text = text;
            lbl.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            lbl.ForeColor = Theme.TEXT_SECONDARY;
            lbl.Location = new Point(Theme.PAD + 2, y);
            lbl.AutoSize = true;
            parent.Controls.Add(lbl);
            return y + 20;
        }

        public Panel MakeCard(Panel parent, int y, int rowCount)
        {
            int h = Theme.ROW_H * rowCount;
            Panel card = new BufferedPanel();
            card.SetBounds(Theme.PAD, y, Theme.CARD_W, h);
            card.BackColor = Theme.BG_CARD;
            card.Paint += delegate(object s, PaintEventArgs e) {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen p = new Pen(Theme.BORDER_CARD))
                {
                    GraphicsPath path = Theme.RoundRect(0, 0, card.Width - 1, card.Height - 1, 6);
                    e.Graphics.DrawPath(p, path);
                    path.Dispose();
                }
            };
            parent.Controls.Add(card);
            return card;
        }

        public void Divider(Panel card, int y)
        {
            Panel d = new Panel();
            d.SetBounds(Theme.PAD, y, card.Width - Theme.PAD * 2, 1);
            d.BackColor = Theme.BORDER;
            card.Controls.Add(d);
        }

        public void ToggleRow(Panel card, int rowY, string feature, string label)
        {
            ToggleRow(card, rowY, feature, label, false);
        }

        public void ToggleRow(Panel card, int rowY, string feature, string label, bool defaultOn)
        {
            Label lbl = new Label();
            lbl.Text = label;
            lbl.Font = new Font("Segoe UI", 9.5f);
            lbl.ForeColor = defaultOn ? AccentColor : Theme.TEXT_PRIMARY;
            lbl.Location = new Point(Theme.PAD, rowY + 10);
            lbl.AutoSize = true;
            card.Controls.Add(lbl);

            ToggleSwitch sw = new ToggleSwitch();
            sw.AccentColor = AccentColor;
            sw.Checked = defaultOn;
            sw.Location = new Point(Theme.CARD_W - Theme.PAD - 44 - 54, rowY + 9);
            card.Controls.Add(sw);

            if (!FeatureStates.ContainsKey(feature))
                FeatureStates[feature] = defaultOn;
            ToggleSwitches[feature] = sw;
            FeatureLabels[feature] = lbl;

            PluginHost host = this;
            sw.Toggled += delegate {
                host.FeatureStates[feature] = sw.Checked;
                lbl.ForeColor = sw.Checked ? host.AccentColor : Theme.TEXT_PRIMARY;
                host.SendCommand("TOGGLE:" + feature);
            };

            HotkeyBtn(card, Theme.CARD_W - Theme.PAD - 46, rowY + 9, feature);
        }

        public TextBox ValueRow(Panel card, int rowY, string label, string defVal, string btnText, EventHandler onClick)
        {
            Label lbl = new Label();
            lbl.Text = label;
            lbl.Font = new Font("Segoe UI", 9f);
            lbl.ForeColor = Theme.TEXT_SECONDARY;
            lbl.Location = new Point(Theme.PAD + 18, rowY + 12);
            lbl.AutoSize = true;
            card.Controls.Add(lbl);

            TextBox tb = MakeInput(card, 130, rowY + 8, 80, defVal);

            Button btn = ActionBtn(card, 218, rowY + 7, btnText, 56);
            btn.Click += onClick;

            return tb;
        }

        public TextBox MakeInput(Panel parent, int x, int y, int w, string defVal)
        {
            TextBox tb = new TextBox();
            tb.SetBounds(x, y, w, 24);
            tb.BackColor = Theme.BG_INPUT;
            tb.ForeColor = Theme.TEXT_PRIMARY;
            tb.BorderStyle = BorderStyle.FixedSingle;
            tb.Font = new Font("Segoe UI", 9.5f);
            tb.Text = defVal;
            parent.Controls.Add(tb);
            return tb;
        }

        public Button ActionBtn(Panel parent, int x, int y, string text, int w)
        {
            Button btn = new Button();
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Theme.BORDER_CARD;
            btn.FlatAppearance.MouseOverBackColor = Theme.BG_BTN_HOVER;
            btn.SetBounds(x, y, w, 28);
            btn.Text = text;
            btn.Font = new Font("Segoe UI", 8.5f);
            btn.BackColor = Theme.BG_INPUT;
            btn.ForeColor = Theme.TEXT_PRIMARY;
            btn.Cursor = Cursors.Hand;
            parent.Controls.Add(btn);
            return btn;
        }

        public void HotkeyBtn(Panel card, int x, int y, string action)
        {
            Button btn = new Button();
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = Theme.HK_BORDER;
            btn.FlatAppearance.MouseOverBackColor = Theme.BG_BTN_HOVER;
            btn.SetBounds(x, y, 46, 24);
            btn.Font = new Font("Segoe UI", 7.5f);
            btn.BackColor = Theme.HK_BG;
            btn.ForeColor = Theme.HK_TEXT;
            btn.Cursor = Cursors.Hand;
            btn.Tag = action;
            btn.Text = FormatKey(HotkeyBindings.ContainsKey(action) ? HotkeyBindings[action] : Keys.None);
            PluginHost host = this;
            btn.Click += delegate { if (OnBindRequest != null) OnBindRequest(action, btn); };
            card.Controls.Add(btn);
            HotkeyButtons[action] = btn;
        }

        public Action<string, Button> OnBindRequest;

        public void AddSpacer(Panel parent, int y, int h)
        {
            Panel sp = new Panel();
            sp.SetBounds(0, y, 10, h);
            sp.BackColor = Color.Transparent;
            parent.Controls.Add(sp);
        }

        public static string FormatKey(Keys key)
        {
            if (key == Keys.None) return "---";
            string n = key.ToString();
            if (n.StartsWith("F") && n.Length <= 3) return n;
            if (n == "Pause") return "Pause";
            if (n.Length > 5) return n.Substring(0, 5);
            return n;
        }
    }

    public abstract class GamePlugin
    {
        public PluginHost Host;

        public abstract string GameName { get; }
        public abstract string ShortName { get; }
        public abstract string SteamId { get; }
        public abstract string ProcessName { get; }
        public abstract Color AccentColor { get; }
        public abstract Color AccentDimColor { get; }
        public abstract string IconPath { get; }
        public abstract string IpcDirectory { get; }
        public abstract string PluginVersion { get; }

        public abstract void InitDefaultHotkeys(Dictionary<string, Keys> bindings);
        public abstract void BuildPanel(Panel container);
        public abstract void ParseStatus(string key, string value);
        public abstract void ExecuteHotkey(string action);

        public virtual void OnConnected() { }
        public virtual void OnDisconnected() { }
        public virtual void OnTick() { }
        public virtual List<string> SaveExtraState() { return new List<string>(); }
        public virtual void LoadExtraState(Dictionary<string, string> data) { }
    }
}
