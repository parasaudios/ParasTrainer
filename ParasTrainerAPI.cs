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
        // WeMod/WAND-style dark palette: near-black shell, slightly-lifted card surfaces,
        // teal accent. Colours sampled from the approved redesign mockup.
        public static readonly Color BG_SIDEBAR = Color.FromArgb(18, 19, 24);      // #121318
        public static readonly Color BG_SIDEBAR_CARD = Color.FromArgb(27, 30, 38); // row rest (transparent-ish)
        public static readonly Color BG_SIDEBAR_HOVER = Color.FromArgb(31, 34, 42);
        public static readonly Color BG_SIDEBAR_SEL = Color.FromArgb(32, 36, 45);   // #20242d
        public static readonly Color BG_MAIN = Color.FromArgb(21, 22, 27);         // #15161b
        public static readonly Color BG_HEADER = Color.FromArgb(27, 29, 36);       // #1b1d24 (bars/footer)
        public static readonly Color BG_CARD = Color.FromArgb(27, 30, 38);         // #1b1e26
        public static readonly Color BG_INPUT = Color.FromArgb(38, 42, 51);        // #262a33
        public static readonly Color BG_BTN_HOVER = Color.FromArgb(48, 53, 64);
        public static readonly Color BG_ROW_HOVER = Color.FromArgb(33, 37, 46);
        public static readonly Color TEXT_PRIMARY = Color.FromArgb(233, 235, 239); // #e9ebef
        public static readonly Color TEXT_SECONDARY = Color.FromArgb(153, 160, 172); // #99a0ac
        public static readonly Color TEXT_HEADING = Color.FromArgb(195, 201, 212); // #c3c9d4 (category labels)
        public static readonly Color TEXT_DISABLED = Color.FromArgb(107, 114, 128); // #6b7280
        public static readonly Color BORDER = Color.FromArgb(35, 39, 47);          // #23272f (row dividers)
        public static readonly Color BORDER_CARD = Color.FromArgb(44, 48, 58);
        public static readonly Color STATUS_RED = Color.FromArgb(227, 91, 91);     // #e35b5b
        public static readonly Color ACCENT_AMBER = Color.FromArgb(245, 173, 70);
        public static readonly Color ACCENT_GREEN = Color.FromArgb(45, 212, 191);  // teal (connected/on)
        public static readonly Color HK_BG = Color.FromArgb(38, 42, 51);           // #262a33
        public static readonly Color HK_BORDER = Color.FromArgb(56, 60, 71);
        public static readonly Color HK_TEXT = Color.FromArgb(139, 147, 160);      // #8b93a0
        public static readonly Color HK_EDIT_BG = Color.FromArgb(120, 48, 48);
        public static readonly Color ICON_MUTED = Color.FromArgb(78, 84, 95);      // (i)/pin glyphs at rest

        public const int ROW_H = 46;
        public const int PAD = 14;
        // Leaves room for window borders + the AutoScroll vertical scrollbar so the
        // right-edge row glyphs (hotkey pill / pin) and header chevron never clip.
        public const int CARD_W = 460;

        // Segoe MDL2 Assets glyphs (present on Win10/11) — used for category, row and chrome icons.
        public const string GLYPH_CHEVRON_DOWN = "";
        public const string GLYPH_INFO = "";
        public const string GLYPH_PIN = "";
        public const string GLYPH_ADD = "";
        public const string GLYPH_REMOVE = "";
        public const string ICON_FONT = "Segoe MDL2 Assets";

        public static Font Glyphs(float size)
        {
            try { return new Font(ICON_FONT, size); }
            catch { return new Font("Segoe UI", size); }
        }

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
        private float _thumbPos;
        private float _colorBlend;
        private Timer _animTimer;
        private DateTime _lastClickTime = DateTime.MinValue;

        public Color AccentColor { get { return _accentColor; } set { _accentColor = value; Invalidate(); } }
        public bool RecentlyClicked { get { return (DateTime.Now - _lastClickTime).TotalMilliseconds < 1500; } }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    StartAnimation();
                }
            }
        }

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(40, 22);
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
            _thumbPos = 0f;
            _colorBlend = 0f;
            _animTimer = new Timer();
            _animTimer.Interval = 12;
            _animTimer.Tick += delegate { AnimStep(); };
        }

        private void StartAnimation()
        {
            _animTimer.Start();
        }

        private void AnimStep()
        {
            float target = _checked ? 1f : 0f;
            float diff = target - _thumbPos;
            if (Math.Abs(diff) < 0.02f)
            {
                _thumbPos = target;
                _colorBlend = target;
                _animTimer.Stop();
            }
            else
            {
                _thumbPos += diff * 0.28f;
                _colorBlend += (target - _colorBlend) * 0.22f;
            }
            Invalidate();
        }

        private static Color LerpColor(Color a, Color b, float t)
        {
            if (t <= 0f) return a;
            if (t >= 1f) return b;
            return Color.FromArgb(
                a.A + (int)((b.A - a.A) * t),
                a.R + (int)((b.R - a.R) * t),
                a.G + (int)((b.G - a.G) * t),
                a.B + (int)((b.B - a.B) * t));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color offColor = _hovered ? Color.FromArgb(60, 66, 78) : Color.FromArgb(52, 58, 69);
            Color track = LerpColor(offColor, _accentColor, _colorBlend);
            using (SolidBrush b = new SolidBrush(track))
                g.FillPath(b, Theme.RoundRect(0, 0, Width - 1, Height - 1, Height / 2));
            if (_colorBlend > 0.05f)
                using (Pen p = new Pen(Color.FromArgb((int)(40 * _colorBlend), _accentColor), 3f))
                    g.DrawPath(p, Theme.RoundRect(0, 0, Width - 1, Height - 1, Height / 2));
            float thumbD = Height - 6;
            float minX = 3f;
            float maxX = Width - Height + 3f;
            float thumbX = minX + (maxX - minX) * _thumbPos;
            // thumb: muted grey when off, white when on
            Color thumb = LerpColor(Color.FromArgb(174, 180, 191), Color.White, _colorBlend);
            using (SolidBrush b = new SolidBrush(thumb))
                g.FillEllipse(b, thumbX, 3, thumbD, thumbD);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            _checked = !_checked;
            _lastClickTime = DateTime.Now;
            StartAnimation();
            if (Toggled != null) Toggled(this, EventArgs.Empty);
        }
        protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _animTimer != null) { _animTimer.Stop(); _animTimer.Dispose(); }
            base.Dispose(disposing);
        }
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
        public Dictionary<TextBox, DateTime> AppliedAt = new Dictionary<TextBox, DateTime>();

        public void SyncInput(TextBox tb, string statusVal)
        {
            if (!DirtyInputs.Contains(tb)) { tb.Text = statusVal; return; }
            DateTime applied;
            bool wasApplied = AppliedAt.TryGetValue(tb, out applied);
            if (wasApplied && tb.Text.Trim() == statusVal.Trim())
            { DirtyInputs.Remove(tb); AppliedAt.Remove(tb); }
            else if (wasApplied && (DateTime.Now - applied).TotalSeconds > 3)
            { DirtyInputs.Remove(tb); AppliedAt.Remove(tb); tb.Text = statusVal; }
        }

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
            ToggleSwitch sw = ToggleSwitches[feature];
            if (sw.RecentlyClicked && sw.Checked != state) return;
            FeatureStates[feature] = state;
            sw.Checked = state;
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
            // WAND-style category header: accent glyph + spaced caps label + collapse chevron,
            // sitting ABOVE its card (the card is drawn by the next MakeCard call).
            Label icon = new Label();
            icon.Text = SectionGlyph(text);
            icon.Font = Theme.Glyphs(11f);
            icon.ForeColor = AccentColor;
            icon.AutoSize = true;
            icon.BackColor = Color.Transparent;
            icon.Location = new Point(Theme.PAD + 2, y + 2);
            parent.Controls.Add(icon);

            Label lbl = new Label();
            lbl.Text = text;
            lbl.UseMnemonic = false;   // keep literal '&' (e.g. "RESPAWN & TELEPORTER")
            lbl.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lbl.ForeColor = Theme.TEXT_HEADING;
            lbl.Location = new Point(Theme.PAD + 26, y + 3);
            lbl.AutoSize = true;
            parent.Controls.Add(lbl);

            Label chev = new Label();
            chev.Text = Theme.GLYPH_CHEVRON_DOWN;
            chev.Font = Theme.Glyphs(9f);
            chev.ForeColor = Theme.TEXT_DISABLED;
            chev.AutoSize = true;
            chev.BackColor = Color.Transparent;
            chev.Location = new Point(Theme.PAD + Theme.CARD_W - 22, y + 4);
            parent.Controls.Add(chev);

            return y + 30;
        }

        // Map a category title to a Segoe MDL2 Assets glyph for its header icon.
        static string SectionGlyph(string title)
        {
            string t = title.ToUpperInvariant();
            if (t.Contains("COMBAT")) return "";                               // lightning
            if (t.Contains("BUFF STACK")) return "";                            // star (filled)
            if (t.Contains("APPLY BUFF")) return "";                            // star (outline)
            if (t.Contains("MOVEMENT")) return "";                              // play / go
            if (t.Contains("VACUUM")) return "";                                // download (pull-in)
            if (t.Contains("MULTIPLIER") || t.Contains("EFFECT")) return "";    // tuning
            if (t.Contains("RESPAWN") || t.Contains("TELEPORT")) return "";     // refresh
            if (t.Contains("LUNAR")) return "";                                 // shop/coin
            if (t.Contains("DLC") || t.Contains("HALLOWED")) return "";         // star
            if (t.Contains("ESP") || t.Contains("MAP")) return "";              // search
            if (t.Contains("HOST TOGGLE")) return "";                           // gear
            if (t.Contains("ITEM BOOST")) return "";                            // add
            if (t.Contains("MONEY")) return "";                                 // download
            if (t.Contains("ACTION")) return "";                                // lightning
            if (t.Contains("GIVE ITEM")) return "";                             // shop
            if (t.Contains("TIER")) return "";                                  // filter
            if (t.Contains("CONSOLE") || t.Contains("SERVER")) return "";       // keypad
            return "";                                                          // gear (default)
        }

        // Small MDL2 glyph as a transparent label.
        public Label GlyphLabel(Panel parent, int x, int y, string glyph, float size, Color color)
        {
            Label l = new Label();
            l.Text = glyph;
            l.Font = Theme.Glyphs(size);
            l.ForeColor = color;
            l.AutoSize = true;
            l.BackColor = Color.Transparent;
            l.Location = new Point(x, y);
            parent.Controls.Add(l);
            return l;
        }

        public Panel MakeCard(Panel parent, int y, int rowCount)
        {
            int h = Theme.ROW_H * rowCount;
            Panel card = new BufferedPanel();
            card.SetBounds(Theme.PAD, y, Theme.CARD_W, h);
            card.BackColor = Theme.BG_CARD;
            // Flat elevated surface with soft rounded corners (clip to a rounded region so the
            // child rows can fill edge-to-edge without covering a drawn border).
            try { using (GraphicsPath gp = Theme.RoundRect(0, 0, Theme.CARD_W, h, 9)) card.Region = new Region(gp); }
            catch { }
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
            // Each row is its own panel so it can highlight on hover (WeMod/WAND feel) and carry
            // a hairline bottom divider.
            BufferedPanel row = new BufferedPanel();
            row.SetBounds(0, rowY, card.Width, Theme.ROW_H);
            row.BackColor = Theme.BG_CARD;
            row.Paint += delegate(object s, PaintEventArgs e) {
                using (Pen p = new Pen(Theme.BORDER))
                    e.Graphics.DrawLine(p, Theme.PAD, row.Height - 1, row.Width - Theme.PAD, row.Height - 1);
            };
            card.Controls.Add(row);

            Label lbl = new Label();
            lbl.Text = label;
            lbl.Font = new Font("Segoe UI", 10f);
            lbl.ForeColor = defaultOn ? AccentColor : Theme.TEXT_PRIMARY;
            lbl.AutoSize = true;
            lbl.BackColor = Color.Transparent;
            lbl.Location = new Point(Theme.PAD, (Theme.ROW_H - 19) / 2);
            row.Controls.Add(lbl);

            // (i) info glyph right after the label (decorative affordance, matches mockup)
            int lblW = TextRenderer.MeasureText(label, lbl.Font).Width;
            Label info = GlyphLabel(row, Theme.PAD + lblW + 4, (Theme.ROW_H - 16) / 2, Theme.GLYPH_INFO, 8.5f, Theme.ICON_MUTED);

            // right cluster (right→left): pin · hotkey pill · toggle
            int hkW = 46, swW = 40, pinW = 18;
            int pinX = row.Width - Theme.PAD - pinW;
            int hkX = pinX - 6 - hkW;
            int swX = hkX - 10 - swW;

            Label pin = GlyphLabel(row, pinX, (Theme.ROW_H - 16) / 2, Theme.GLYPH_PIN, 9.5f, Theme.ICON_MUTED);
            pin.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            ToggleSwitch sw = new ToggleSwitch();
            sw.AccentColor = AccentColor;
            sw.Checked = defaultOn;
            sw.Location = new Point(swX, (Theme.ROW_H - sw.Height) / 2);
            sw.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            row.Controls.Add(sw);

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

            HotkeyBtn(row, hkX, (Theme.ROW_H - 24) / 2, feature);
            Button hk = HotkeyButtons.ContainsKey(feature) ? HotkeyButtons[feature] : null;
            if (hk != null) hk.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            EventHandler onEnter = delegate { row.BackColor = Theme.BG_ROW_HOVER; };
            EventHandler onLeave = delegate {
                if (!row.ClientRectangle.Contains(row.PointToClient(Cursor.Position)))
                    row.BackColor = Theme.BG_CARD;
            };
            row.MouseEnter += onEnter; row.MouseLeave += onLeave;
            lbl.MouseEnter += onEnter; lbl.MouseLeave += onLeave;
            info.MouseEnter += onEnter; info.MouseLeave += onLeave;
            pin.MouseEnter += onEnter; pin.MouseLeave += onLeave;
            sw.MouseEnter += onEnter; sw.MouseLeave += onLeave;
            if (hk != null) { hk.MouseEnter += onEnter; hk.MouseLeave += onLeave; }
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
            PluginHost host = this;
            tb.TextChanged += delegate { host.DirtyInputs.Add(tb); };

            Button btn = ActionBtn(card, 218, rowY + 7, btnText, 56);
            btn.Click += onClick;
            btn.Click += delegate { host.AppliedAt[tb] = DateTime.Now; };

            return tb;
        }

        // WAND-style numeric stepper row: label on the left, [−] [editable value] [+] on the right.
        // Commits on +/−, Enter or focus-loss (no separate Apply button). Returns the value box
        // so ParseStatus can keep it in sync via SyncInput.
        public TextBox StepperRow(Panel card, int rowY, string label, string defVal, int min, int max, int step, Action<int> commit)
        {
            Label lbl = new Label();
            lbl.Text = label;
            lbl.Font = new Font("Segoe UI", 9.5f);
            lbl.ForeColor = Theme.TEXT_SECONDARY;
            lbl.AutoSize = true;
            lbl.BackColor = Color.Transparent;
            lbl.Location = new Point(Theme.PAD + 2, rowY + (Theme.ROW_H - 18) / 2);
            card.Controls.Add(lbl);

            int btnSz = 26, valW = 46, gap = 3;
            int plusX = card.Width - Theme.PAD - btnSz;
            int valX = plusX - gap - valW;
            int minusX = valX - gap - btnSz;
            int cy = rowY + (Theme.ROW_H - btnSz) / 2;

            TextBox tb = new TextBox();
            tb.SetBounds(valX, rowY + (Theme.ROW_H - 20) / 2, valW, 20);
            tb.BackColor = Theme.BG_INPUT;
            tb.ForeColor = Theme.TEXT_PRIMARY;
            tb.BorderStyle = BorderStyle.None;
            tb.Font = new Font("Segoe UI", 10f);
            tb.TextAlign = HorizontalAlignment.Center;
            tb.Text = defVal;
            card.Controls.Add(tb);

            PluginHost host = this;
            Action commitNow = delegate {
                int v;
                if (int.TryParse(tb.Text, out v))
                {
                    v = Math.Max(min, Math.Min(max, v));
                    if (v.ToString() != tb.Text) tb.Text = v.ToString();
                    commit(v);
                    host.AppliedAt[tb] = DateTime.Now;
                }
            };
            tb.TextChanged += delegate { host.DirtyInputs.Add(tb); };
            tb.Leave += delegate { commitNow(); };
            tb.KeyDown += delegate(object s2, KeyEventArgs e2) {
                if (e2.KeyCode == Keys.Enter) { e2.SuppressKeyPress = true; commitNow(); }
            };

            StepBtn(card, minusX, cy, btnSz, Theme.GLYPH_REMOVE, delegate { Bump(tb, -step, min, max, commitNow); });
            StepBtn(card, plusX, cy, btnSz, Theme.GLYPH_ADD, delegate { Bump(tb, step, min, max, commitNow); });

            return tb;
        }

        static void Bump(TextBox tb, int delta, int min, int max, Action commit)
        {
            int v; if (!int.TryParse(tb.Text, out v)) v = min;
            v = Math.Max(min, Math.Min(max, v + delta));
            tb.Text = v.ToString();
            commit();
        }

        void StepBtn(Panel parent, int x, int y, int sz, string glyph, EventHandler onClick)
        {
            Button b = new Button();
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Theme.BG_BTN_HOVER;
            b.SetBounds(x, y, sz, sz);
            try { using (GraphicsPath gp = Theme.RoundRect(0, 0, sz, sz, 6)) b.Region = new Region(gp); } catch { }
            b.BackColor = Theme.BG_INPUT;
            b.ForeColor = Theme.TEXT_HEADING;
            b.Font = Theme.Glyphs(8.5f);
            b.Text = glyph;
            b.Cursor = Cursors.Hand;
            b.Click += onClick;
            parent.Controls.Add(b);
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
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Theme.BG_BTN_HOVER;
            btn.SetBounds(x, y, w, 28);
            try { using (GraphicsPath gp = Theme.RoundRect(0, 0, w, 28, 7)) btn.Region = new Region(gp); } catch { }
            btn.Text = text;
            btn.Font = new Font("Segoe UI", 9f);
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
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Theme.BG_BTN_HOVER;
            btn.SetBounds(x, y, 46, 24);
            try { using (GraphicsPath gp = Theme.RoundRect(0, 0, 46, 24, 6)) btn.Region = new Region(gp); } catch { }
            btn.Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
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
