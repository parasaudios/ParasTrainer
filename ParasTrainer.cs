using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ParasTrainer
{
    public class MainForm : Form
    {
        const int SIDEBAR_W = 210;
        const int HEADER_H = 130;
        const int FOOTER_H = 34;
        const int FORM_W = 730;
        const int FORM_H = 920;

        const string APP_VERSION = "2.1";
        const string GITHUB_OWNER = "parasaudios";
        const string GITHUB_REPO = "ParasTrainer";

        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        const int WM_HOTKEY = 0x0312;

        GamePlugin[] plugins;
        PluginHost[] hosts;
        string[] pluginDllPaths;
        int selectedGame = -1;
        Icon[] gameIcons;

        bool[] isConnected;
        bool[] isLaunching;
        DateTime[] launchTime;
        int[] patchesOk;
        int[] patchesFail;
        string[] coreVersion;

        Panel sidebarPanel;
        Panel[] sidebarCards;
        Label[] sidebarNameLabels;
        Panel[] sidebarDots;
        Label[] sidebarStatusLabels;
        Label sidebarTitle;

        Panel headerPanel;
        Label hdrTitle, hdrVersion, hdrSubtitle;
        Panel hdrStatusDot;
        Label hdrStatusLabel, hdrStatusDetail;
        Button hdrPlayBtn;

        Panel contentHost;
        Panel[] contentPanels;
        bool[] panelBuilt;

        Label footerLabel;
        Label reloadLabel;

        System.Windows.Forms.Timer statusPoll;
        int saveCounter;

        FileSystemWatcher pluginWatcher;
        HashSet<int> pendingReloads = new HashSet<int>();
        System.Windows.Forms.Timer reloadDebounce;

        Button updateBtn;
        Label updateStatus;

        public MainForm()
        {
            LoadPlugins();
            InitForm();
            BuildSidebar();
            BuildHeader();
            BuildFooter();
            BuildContentHost();

            int startGame = RestoreFormState();
            if (startGame < 0 || startGame >= plugins.Length) startGame = 0;
            if (plugins.Length > 0) SwitchToGame(startGame);

            statusPoll = new System.Windows.Forms.Timer();
            statusPoll.Interval = 500;
            statusPoll.Tick += delegate { PollAllGames(); };
            statusPoll.Start();

            SetupPluginWatcher();
        }

        void LoadPlugins()
        {
            var loadedPlugins = new List<GamePlugin>();
            var loadedPaths = new List<string>();
            var loadedNames = new HashSet<string>();

            try
            {
                foreach (Type t in Assembly.GetExecutingAssembly().GetTypes())
                {
                    if (t.IsAbstract || !typeof(GamePlugin).IsAssignableFrom(t)) continue;
                    try
                    {
                        GamePlugin gp = (GamePlugin)Activator.CreateInstance(t);
                        loadedPlugins.Add(gp);
                        loadedPaths.Add("");
                        loadedNames.Add(gp.ShortName);
                    }
                    catch { }
                }
            }
            catch { }

            string pluginDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
            if (Directory.Exists(pluginDir))
            {
                string[] dlls = Directory.GetFiles(pluginDir, "*.dll");
                Array.Sort(dlls);
                foreach (string dll in dlls)
                {
                    GamePlugin gp = LoadPluginFromDll(dll);
                    if (gp != null && !loadedNames.Contains(gp.ShortName))
                    {
                        loadedPlugins.Add(gp);
                        loadedPaths.Add(dll);
                        loadedNames.Add(gp.ShortName);
                    }
                }
            }

            plugins = loadedPlugins.ToArray();
            pluginDllPaths = loadedPaths.ToArray();
            int n = plugins.Length;

            hosts = new PluginHost[n];
            gameIcons = new Icon[n];
            isConnected = new bool[n];
            isLaunching = new bool[n];
            launchTime = new DateTime[n];
            patchesOk = new int[n];
            patchesFail = new int[n];
            coreVersion = new string[n];
            contentPanels = new Panel[n];
            panelBuilt = new bool[n];

            for (int i = 0; i < n; i++)
            {
                coreVersion[i] = plugins[i].PluginVersion;
                GamePlugin p = plugins[i];
                string ipcDir = p.IpcDirectory;
                PluginHost host = new PluginHost();
                host.AccentColor = p.AccentColor;
                host.AccentDimColor = p.AccentDimColor;
                host.IpcDir = ipcDir;
                host.CmdDir = Path.Combine(ipcDir, "cmd");
                int idx = i;
                host.OnBindRequest = delegate(string action, Button btn) { StartBinding(idx, action, btn); };
                p.InitDefaultHotkeys(host.HotkeyBindings);
                p.Host = host;
                hosts[i] = host;

                try
                {
                    if (File.Exists(p.IconPath))
                        gameIcons[i] = new Icon(p.IconPath);
                }
                catch { }
            }
        }

        GamePlugin LoadPluginFromDll(string dllPath)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(dllPath);
                Assembly asm = Assembly.Load(bytes);
                foreach (Type t in asm.GetTypes())
                {
                    if (t.IsAbstract) continue;
                    if (typeof(GamePlugin).IsAssignableFrom(t))
                        return (GamePlugin)Activator.CreateInstance(t);
                }
            }
            catch { }
            return null;
        }

        void InitForm()
        {
            Text = "Paras Trainer";
            Size = new Size(FORM_W, FORM_H);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.BG_MAIN;
            DoubleBuffered = true;
            try { Icon = gameIcons.FirstOrDefault(ic => ic != null); } catch { }
        }

        void BuildSidebar()
        {
            sidebarPanel = new Panel();
            sidebarPanel.SetBounds(0, 0, SIDEBAR_W, FORM_H);
            sidebarPanel.BackColor = Theme.BG_SIDEBAR;
            Controls.Add(sidebarPanel);

            sidebarTitle = new Label();
            sidebarTitle.Text = "PARAS TRAINER";
            sidebarTitle.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
            sidebarTitle.ForeColor = Theme.TEXT_PRIMARY;
            sidebarTitle.Location = new Point(16, 18);
            sidebarTitle.AutoSize = true;
            sidebarPanel.Controls.Add(sidebarTitle);

            Label ver = new Label();
            ver.Text = "v" + APP_VERSION;
            ver.Font = new Font("Segoe UI", 8f);
            ver.ForeColor = Theme.TEXT_DISABLED;
            ver.Location = new Point(18, 44);
            ver.AutoSize = true;
            sidebarPanel.Controls.Add(ver);

            int n = plugins.Length;
            sidebarCards = new Panel[n];
            sidebarNameLabels = new Label[n];
            sidebarDots = new Panel[n];
            sidebarStatusLabels = new Label[n];

            int cardY = 70;
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                Panel card = new BufferedPanel();
                card.SetBounds(10, cardY, SIDEBAR_W - 20, 60);
                card.BackColor = Theme.BG_SIDEBAR_CARD;
                card.Cursor = Cursors.Hand;
                card.Paint += delegate(object s, PaintEventArgs e) {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (Pen p = new Pen(Theme.BORDER_CARD))
                    {
                        GraphicsPath path = Theme.RoundRect(0, 0, card.Width - 1, card.Height - 1, 8);
                        e.Graphics.DrawPath(p, path);
                        path.Dispose();
                    }
                    if (gameIcons[idx] != null)
                    {
                        try { e.Graphics.DrawIcon(gameIcons[idx], new Rectangle(10, 10, 40, 40)); } catch { }
                    }
                };
                card.MouseEnter += delegate { if (idx != selectedGame) card.BackColor = Theme.BG_SIDEBAR_HOVER; };
                card.MouseLeave += delegate { if (idx != selectedGame) card.BackColor = Theme.BG_SIDEBAR_CARD; };
                card.Click += delegate { SwitchToGame(idx); };
                sidebarPanel.Controls.Add(card);
                sidebarCards[i] = card;

                Label name = new Label();
                name.Text = plugins[i].GameName;
                name.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                name.ForeColor = Theme.TEXT_PRIMARY;
                name.Location = new Point(58, 10);
                name.Size = new Size(SIDEBAR_W - 90, 20);
                name.Click += delegate { SwitchToGame(idx); };
                name.Cursor = Cursors.Hand;
                card.Controls.Add(name);
                sidebarNameLabels[i] = name;

                Panel dot = new Panel();
                dot.SetBounds(58, 36, 8, 8);
                dot.BackColor = Theme.STATUS_RED;
                dot.Paint += delegate(object s, PaintEventArgs e) {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (SolidBrush b = new SolidBrush(((Panel)s).BackColor))
                        e.Graphics.FillEllipse(b, 0, 0, 7, 7);
                };
                card.Controls.Add(dot);
                sidebarDots[i] = dot;

                Label status = new Label();
                status.Text = "Offline";
                status.Font = new Font("Segoe UI", 8f);
                status.ForeColor = Theme.TEXT_DISABLED;
                status.Location = new Point(70, 33);
                status.AutoSize = true;
                status.Click += delegate { SwitchToGame(idx); };
                status.Cursor = Cursors.Hand;
                card.Controls.Add(status);
                sidebarStatusLabels[i] = status;

                cardY += 68;
            }

            updateBtn = new Button();
            updateBtn.FlatStyle = FlatStyle.Flat;
            updateBtn.FlatAppearance.BorderSize = 1;
            updateBtn.FlatAppearance.BorderColor = Theme.BORDER_CARD;
            updateBtn.FlatAppearance.MouseOverBackColor = Theme.BG_SIDEBAR_HOVER;
            updateBtn.SetBounds(10, FORM_H - 120, SIDEBAR_W - 20, 30);
            updateBtn.Text = "Check for Updates";
            updateBtn.Font = new Font("Segoe UI", 8.5f);
            updateBtn.BackColor = Theme.BG_SIDEBAR_CARD;
            updateBtn.ForeColor = Theme.TEXT_SECONDARY;
            updateBtn.Cursor = Cursors.Hand;
            updateBtn.Click += delegate { CheckForUpdate(); };
            sidebarPanel.Controls.Add(updateBtn);

            updateStatus = new Label();
            updateStatus.Text = "";
            updateStatus.Font = new Font("Segoe UI", 7.5f);
            updateStatus.ForeColor = Theme.TEXT_DISABLED;
            updateStatus.Location = new Point(10, FORM_H - 86);
            updateStatus.Size = new Size(SIDEBAR_W - 20, 16);
            updateStatus.TextAlign = ContentAlignment.MiddleCenter;
            sidebarPanel.Controls.Add(updateStatus);
        }

        void BuildHeader()
        {
            headerPanel = new Panel();
            headerPanel.SetBounds(SIDEBAR_W, 0, FORM_W - SIDEBAR_W, HEADER_H);
            headerPanel.BackColor = Theme.BG_HEADER;
            Controls.Add(headerPanel);

            hdrTitle = new Label();
            hdrTitle.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
            hdrTitle.ForeColor = Theme.TEXT_PRIMARY;
            hdrTitle.Location = new Point(20, 14);
            hdrTitle.AutoSize = true;
            headerPanel.Controls.Add(hdrTitle);

            hdrSubtitle = new Label();
            hdrSubtitle.Font = new Font("Segoe UI", 8.5f);
            hdrSubtitle.ForeColor = Theme.TEXT_SECONDARY;
            hdrSubtitle.Location = new Point(22, 42);
            hdrSubtitle.AutoSize = true;
            headerPanel.Controls.Add(hdrSubtitle);

            hdrVersion = new Label();
            hdrVersion.Font = new Font("Segoe UI", 8f);
            hdrVersion.ForeColor = Theme.TEXT_DISABLED;
            hdrVersion.AutoSize = true;
            headerPanel.Controls.Add(hdrVersion);

            hdrPlayBtn = new Button();
            hdrPlayBtn.FlatStyle = FlatStyle.Flat;
            hdrPlayBtn.FlatAppearance.BorderSize = 0;
            hdrPlayBtn.SetBounds(170, 62, 200, 40);
            hdrPlayBtn.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            hdrPlayBtn.Cursor = Cursors.Hand;
            hdrPlayBtn.Click += PlayClick;
            headerPanel.Controls.Add(hdrPlayBtn);

            hdrStatusDot = new Panel();
            hdrStatusDot.SetBounds(22, 74, 10, 10);
            hdrStatusDot.BackColor = Theme.STATUS_RED;
            hdrStatusDot.Paint += delegate(object s, PaintEventArgs e) {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush b = new SolidBrush(((Panel)s).BackColor))
                    e.Graphics.FillEllipse(b, 0, 0, 9, 9);
            };
            headerPanel.Controls.Add(hdrStatusDot);

            hdrStatusLabel = new Label();
            hdrStatusLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            hdrStatusLabel.ForeColor = Theme.TEXT_SECONDARY;
            hdrStatusLabel.Location = new Point(36, 72);
            hdrStatusLabel.AutoSize = true;
            headerPanel.Controls.Add(hdrStatusLabel);

            hdrStatusDetail = new Label();
            hdrStatusDetail.Font = new Font("Segoe UI", 8f);
            hdrStatusDetail.ForeColor = Theme.TEXT_DISABLED;
            hdrStatusDetail.Location = new Point(22, 96);
            hdrStatusDetail.AutoSize = true;
            headerPanel.Controls.Add(hdrStatusDetail);
        }

        void BuildFooter()
        {
            Panel fp = new Panel();
            fp.SetBounds(SIDEBAR_W, FORM_H - FOOTER_H - 39, FORM_W - SIDEBAR_W, FOOTER_H);
            fp.BackColor = Theme.BG_HEADER;
            Controls.Add(fp);

            footerLabel = new Label();
            footerLabel.Font = new Font("Segoe UI", 8f);
            footerLabel.ForeColor = Theme.TEXT_DISABLED;
            footerLabel.Location = new Point(14, 10);
            footerLabel.AutoSize = true;
            footerLabel.Text = "Click hotkey buttons to rebind";
            fp.Controls.Add(footerLabel);

            reloadLabel = new Label();
            reloadLabel.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            reloadLabel.ForeColor = Theme.ACCENT_AMBER;
            reloadLabel.Location = new Point(FORM_W - SIDEBAR_W - 180, 10);
            reloadLabel.AutoSize = true;
            reloadLabel.Text = "";
            reloadLabel.Cursor = Cursors.Hand;
            reloadLabel.Click += delegate { DoReload(); };
            fp.Controls.Add(reloadLabel);
        }

        void BuildContentHost()
        {
            contentHost = new Panel();
            contentHost.SetBounds(SIDEBAR_W, HEADER_H, FORM_W - SIDEBAR_W, FORM_H - HEADER_H - FOOTER_H - 39);
            contentHost.BackColor = Theme.BG_MAIN;
            Controls.Add(contentHost);
        }

        void SwitchToGame(int idx)
        {
            if (idx == selectedGame || idx < 0 || idx >= plugins.Length) return;

            UnregisterAllHotkeys();

            if (selectedGame >= 0 && contentPanels[selectedGame] != null)
                contentPanels[selectedGame].Visible = false;

            selectedGame = idx;

            if (gameIcons[idx] != null) Icon = gameIcons[idx];

            if (!panelBuilt[idx])
            {
                Panel cp = new BufferedPanel();
                cp.SetBounds(0, 0, contentHost.Width, contentHost.Height);
                cp.AutoScroll = true;
                cp.BackColor = Theme.BG_MAIN;
                contentHost.Controls.Add(cp);
                contentPanels[idx] = cp;

                plugins[idx].BuildPanel(cp);
                panelBuilt[idx] = true;
                LoadHotkeys(idx);
                LoadPanelState(idx);
            }

            contentPanels[idx].Visible = true;
            UpdateHeader(idx);
            UpdateSidebar();
            UpdateFooter(idx);
            RegisterGameHotkeys(idx);
            Text = plugins[idx].GameName + " — Paras Trainer";
        }

        void UpdateSidebar()
        {
            for (int i = 0; i < plugins.Length; i++)
            {
                sidebarCards[i].BackColor = i == selectedGame ? Theme.BG_SIDEBAR_SEL : Theme.BG_SIDEBAR_CARD;
                sidebarCards[i].Invalidate();

                if (isConnected[i])
                {
                    sidebarDots[i].BackColor = Theme.ACCENT_GREEN;
                    sidebarStatusLabels[i].Text = "Connected";
                    sidebarStatusLabels[i].ForeColor = Theme.ACCENT_GREEN;
                }
                else
                {
                    sidebarDots[i].BackColor = Theme.STATUS_RED;
                    sidebarStatusLabels[i].Text = "Offline";
                    sidebarStatusLabels[i].ForeColor = Theme.TEXT_DISABLED;
                }
                sidebarDots[i].Invalidate();
            }
        }

        void UpdateHeader(int idx)
        {
            Color accent = plugins[idx].AccentColor;
            Color accentDim = plugins[idx].AccentDimColor;
            hdrTitle.Text = plugins[idx].GameName.ToUpper() + " TRAINER";
            hdrTitle.ForeColor = accent;
            hdrSubtitle.Text = plugins[idx].GameName;
            hdrVersion.Text = coreVersion[idx].Length > 0 ? "v" + coreVersion[idx] : "";
            int subtitleW = TextRenderer.MeasureText(hdrSubtitle.Text, hdrSubtitle.Font).Width;
            hdrVersion.Location = new Point(22 + subtitleW, 43);

            if (isConnected[idx])
            {
                hdrPlayBtn.Text = "✓   CONNECTED";
                hdrPlayBtn.BackColor = Color.FromArgb(accent.R / 4, accent.G / 4, accent.B / 4);
                hdrPlayBtn.ForeColor = accent;
                hdrPlayBtn.FlatAppearance.MouseOverBackColor = hdrPlayBtn.BackColor;
                hdrPlayBtn.Cursor = Cursors.Default;
                hdrStatusDot.BackColor = Theme.ACCENT_GREEN;
                hdrStatusLabel.Text = "Connected";
                hdrStatusLabel.ForeColor = Theme.ACCENT_GREEN;
                hdrStatusDetail.Text = patchesOk[idx] + " patches active";
                hdrStatusDetail.ForeColor = accentDim;
            }
            else if (isLaunching[idx])
            {
                hdrPlayBtn.Text = "LAUNCHING...";
                hdrPlayBtn.BackColor = Color.FromArgb(70, 48, 6);
                hdrPlayBtn.ForeColor = Theme.ACCENT_AMBER;
                hdrPlayBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 48, 6);
                hdrPlayBtn.Cursor = Cursors.WaitCursor;
                hdrStatusDot.BackColor = Theme.ACCENT_AMBER;
                hdrStatusLabel.Text = "Waiting";
                hdrStatusLabel.ForeColor = Theme.ACCENT_AMBER;
                hdrStatusDetail.Text = "Starting game...";
            }
            else
            {
                hdrPlayBtn.Text = "▶   PLAY";
                hdrPlayBtn.BackColor = Color.FromArgb(accent.R / 2, accent.G / 2, accent.B / 2);
                hdrPlayBtn.ForeColor = Color.White;
                hdrPlayBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(
                    Math.Min(255, accent.R / 2 + 20), Math.Min(255, accent.G / 2 + 20), Math.Min(255, accent.B / 2 + 20));
                hdrPlayBtn.Cursor = Cursors.Hand;
                hdrStatusDot.BackColor = Theme.STATUS_RED;
                hdrStatusLabel.Text = "Not connected";
                hdrStatusLabel.ForeColor = Theme.TEXT_SECONDARY;
                hdrStatusDetail.Text = "";
            }
            hdrStatusDot.Invalidate();
        }

        void UpdateFooter(int idx)
        {
            string p = patchesFail[idx] > 0
                ? patchesOk[idx] + " OK, " + patchesFail[idx] + " failed"
                : (patchesOk[idx] > 0 ? patchesOk[idx] + " active" : "--");
            footerLabel.Text = "Click hotkey buttons to rebind  •  Patches: " + p;
        }

        void PlayClick(object sender, EventArgs e)
        {
            if (selectedGame < 0) return;
            if (isConnected[selectedGame] || isLaunching[selectedGame]) return;
            isLaunching[selectedGame] = true;
            launchTime[selectedGame] = DateTime.Now;
            UpdateHeader(selectedGame);
            try { Process.Start("steam://rungameid/" + plugins[selectedGame].SteamId); } catch { }
        }

        // ── Status Polling ──

        void PollAllGames()
        {
            for (int i = 0; i < plugins.Length; i++)
            {
                if (isLaunching[i] && !isConnected[i])
                {
                    double elapsed = (DateTime.Now - launchTime[i]).TotalSeconds;
                    if (elapsed > 90 || (elapsed > 10 && Process.GetProcessesByName(plugins[i].ProcessName).Length == 0))
                    {
                        isLaunching[i] = false;
                        if (i == selectedGame) UpdateHeader(i);
                        UpdateSidebar();
                    }
                }

                if (panelBuilt[i])
                    PollGame(i);
                else
                    PollConnection(i);
            }

            saveCounter++;
            if (saveCounter >= 10)
            {
                saveCounter = 0;
                for (int i = 0; i < plugins.Length; i++)
                    if (isConnected[i]) SavePanelState(i);
            }
        }

        void PollGame(int idx)
        {
            PluginHost host = hosts[idx];
            string statusFile = Path.Combine(host.IpcDir, "status.txt");
            try
            {
                if (!File.Exists(statusFile) || (DateTime.Now - new FileInfo(statusFile).LastWriteTime).TotalSeconds > 5)
                {
                    if (isConnected[idx])
                    {
                        isConnected[idx] = false;
                        plugins[idx].OnDisconnected();
                        if (idx == selectedGame) UpdateHeader(idx);
                        UpdateSidebar();
                    }
                    return;
                }

                string content = File.ReadAllText(statusFile);
                foreach (string line in content.Split('\n'))
                {
                    string t = line.Trim();
                    if (t.Length == 0) continue;
                    string[] kv = t.Split('=');
                    if (kv.Length != 2) continue;
                    string key = kv[0], val = kv[1];

                    if (key == "Patches") { int.TryParse(val, out patchesOk[idx]); }
                    else if (key == "PatchesFailed") { int.TryParse(val, out patchesFail[idx]); }
                    else if (key == "CoreVersion" && val != coreVersion[idx]) { coreVersion[idx] = val; }
                    else { plugins[idx].ParseStatus(key, val); }
                }

                if (!isConnected[idx])
                {
                    isConnected[idx] = true;
                    isLaunching[idx] = false;
                    plugins[idx].OnConnected();
                    RegisterGameHotkeys(idx);
                }
                if (idx == selectedGame) { UpdateHeader(idx); UpdateFooter(idx); }
                UpdateSidebar();

                plugins[idx].OnTick();
            }
            catch { }
        }

        void PollConnection(int idx)
        {
            string statusFile = Path.Combine(hosts[idx].IpcDir, "status.txt");
            try
            {
                bool fresh = File.Exists(statusFile) && (DateTime.Now - new FileInfo(statusFile).LastWriteTime).TotalSeconds <= 5;
                if (fresh && !isConnected[idx])
                {
                    isConnected[idx] = true;
                    isLaunching[idx] = false;
                    UpdateSidebar();
                    if (idx == selectedGame) UpdateHeader(idx);
                }
                else if (!fresh && isConnected[idx])
                {
                    isConnected[idx] = false;
                    UpdateSidebar();
                    if (idx == selectedGame) UpdateHeader(idx);
                }
            }
            catch { }
        }

        // ── Hotkey System ──

        void StartBinding(int gameIdx, string action, Button btn)
        {
            PluginHost host = hosts[gameIdx];
            if (host.HotkeyBindings == null) return;

            foreach (var kvp in host.HotkeyButtons)
            {
                kvp.Value.BackColor = Theme.HK_BG;
                kvp.Value.ForeColor = Theme.HK_TEXT;
            }

            bindingGameIdx = gameIdx;
            bindingAction = action;
            btn.Text = "•••";
            btn.BackColor = Theme.HK_EDIT_BG;
            btn.ForeColor = Color.White;
        }

        int bindingGameIdx = -1;
        string bindingAction;

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;

            if (bindingAction != null && bindingGameIdx >= 0)
            {
                PluginHost host = hosts[bindingGameIdx];
                if (key == Keys.Escape)
                {
                    bindingAction = null;
                    bindingGameIdx = -1;
                    UpdateAllHotkeyLabels(host);
                    return true;
                }
                host.HotkeyBindings[bindingAction] = key;
                bindingAction = null;
                int gi = bindingGameIdx;
                bindingGameIdx = -1;
                SaveHotkeys(gi);
                UnregisterAllHotkeys();
                RegisterGameHotkeys(gi);
                UpdateAllHotkeyLabels(host);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void UpdateAllHotkeyLabels(PluginHost host)
        {
            foreach (var kvp in host.HotkeyButtons)
            {
                kvp.Value.Text = PluginHost.FormatKey(host.HotkeyBindings.ContainsKey(kvp.Key) ? host.HotkeyBindings[kvp.Key] : Keys.None);
                kvp.Value.BackColor = Theme.HK_BG;
                kvp.Value.ForeColor = Theme.HK_TEXT;
            }
        }

        void RegisterGameHotkeys(int idx)
        {
            UnregisterAllHotkeys();
            PluginHost host = hosts[idx];
            int id = 0;
            foreach (var kvp in host.HotkeyBindings)
            {
                if (kvp.Value != Keys.None)
                    RegisterHotKey(Handle, id, 0, (uint)kvp.Value);
                id++;
            }
        }

        void UnregisterAllHotkeys()
        {
            for (int i = 0; i < 50; i++) UnregisterHotKey(Handle, i);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && selectedGame >= 0)
            {
                int hkId = m.WParam.ToInt32();
                PluginHost host = hosts[selectedGame];
                int idx = 0;
                foreach (var kvp in host.HotkeyBindings)
                {
                    if (idx == hkId) { plugins[selectedGame].ExecuteHotkey(kvp.Key); break; }
                    idx++;
                }
            }
            base.WndProc(ref m);
        }

        // ── Hotkey Persistence ──

        string HotkeyPath(int idx) { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, plugins[idx].ShortName.ToLower() + "_hotkeys.cfg"); }
        string StatePath(int idx) { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, plugins[idx].ShortName.ToLower() + "_state.cfg"); }

        void SaveHotkeys(int idx)
        {
            try
            {
                var lines = new List<string>();
                foreach (var kvp in hosts[idx].HotkeyBindings)
                    lines.Add(kvp.Key + "=" + (int)kvp.Value);
                File.WriteAllLines(HotkeyPath(idx), lines.ToArray());
            }
            catch { }
        }

        void LoadHotkeys(int idx)
        {
            try
            {
                string path = HotkeyPath(idx);
                if (!File.Exists(path)) return;
                foreach (string line in File.ReadAllLines(path))
                {
                    string[] parts = line.Split('=');
                    if (parts.Length == 2)
                    {
                        int v; if (int.TryParse(parts[1], out v))
                            hosts[idx].HotkeyBindings[parts[0]] = (Keys)v;
                    }
                }
                UpdateAllHotkeyLabels(hosts[idx]);
            }
            catch { }
        }

        // ── Form State Restore (window position + selected game) ──

        int RestoreFormState()
        {
            int startGame = 0;
            try
            {
                for (int i = 0; i < plugins.Length; i++)
                {
                    string path = StatePath(i);
                    if (!File.Exists(path)) continue;
                    foreach (string line in File.ReadAllLines(path))
                    {
                        string[] parts = line.Split('=');
                        if (parts.Length != 2) continue;
                        if (parts[0] == "WindowX")
                        {
                            int x;
                            if (int.TryParse(parts[1], out x))
                            {
                                StartPosition = FormStartPosition.Manual;
                                Left = x;
                            }
                        }
                        else if (parts[0] == "WindowY")
                        {
                            int y;
                            if (int.TryParse(parts[1], out y))
                            {
                                StartPosition = FormStartPosition.Manual;
                                Top = y;
                            }
                        }
                        else if (parts[0] == "SelectedGame")
                        {
                            int g;
                            if (int.TryParse(parts[1], out g)) startGame = g;
                        }
                    }
                    break;
                }
            }
            catch { }
            return startGame;
        }

        // ── Panel State Persistence ──

        void SavePanelState(int idx)
        {
            try
            {
                PluginHost host = hosts[idx];
                var lines = new List<string>();
                foreach (var kvp in host.FeatureStates)
                    lines.Add("Toggle_" + kvp.Key + "=" + (kvp.Value ? "1" : "0"));

                List<string> extra = plugins[idx].SaveExtraState();
                lines.AddRange(extra);

                lines.Add("WindowX=" + Left);
                lines.Add("WindowY=" + Top);
                lines.Add("SelectedGame=" + selectedGame);

                File.WriteAllLines(StatePath(idx), lines.ToArray());
            }
            catch { }
        }

        void LoadPanelState(int idx)
        {
            try
            {
                string path = StatePath(idx);
                if (!File.Exists(path)) return;
                PluginHost host = hosts[idx];
                var extra = new Dictionary<string, string>();

                foreach (string line in File.ReadAllLines(path))
                {
                    string[] parts = line.Split('=');
                    if (parts.Length != 2) continue;
                    string key = parts[0], val = parts[1];

                    if (key.StartsWith("Toggle_"))
                    {
                        string feature = key.Substring(7);
                        host.FeatureStates[feature] = val == "1";
                        if (host.ToggleSwitches.ContainsKey(feature))
                        {
                            host.ToggleSwitches[feature].Checked = val == "1";
                            if (host.FeatureLabels.ContainsKey(feature))
                                host.FeatureLabels[feature].ForeColor = val == "1" ? host.AccentColor : Theme.TEXT_PRIMARY;
                        }
                    }
                    else
                    {
                        extra[key] = val;
                    }
                }

                plugins[idx].LoadExtraState(extra);
            }
            catch { }
        }

        // ── Plugin Hot-Reload ──

        void SetupPluginWatcher()
        {
            string pluginDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
            if (!Directory.Exists(pluginDir)) return;

            reloadDebounce = new System.Windows.Forms.Timer();
            reloadDebounce.Interval = 800;
            reloadDebounce.Tick += delegate {
                reloadDebounce.Stop();
                reloadLabel.Text = "⚡ Plugin updated — click to reload";
                reloadLabel.Visible = true;
            };

            pluginWatcher = new FileSystemWatcher(pluginDir, "*.dll");
            pluginWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
            pluginWatcher.Changed += delegate(object s, FileSystemEventArgs e) {
                try
                {
                    BeginInvoke((Action)delegate {
                        reloadDebounce.Stop();
                        reloadDebounce.Start();
                    });
                }
                catch { }
            };
            pluginWatcher.EnableRaisingEvents = true;
        }

        void DoReload()
        {
            reloadLabel.Text = "";

            for (int i = 0; i < plugins.Length; i++)
                if (isConnected[i]) SavePanelState(i);
            SaveHotkeys(selectedGame);

            string exePath = Application.ExecutablePath;
            string args = selectedGame >= 0 ? selectedGame.ToString() : "0";
            try { Process.Start(exePath, args); } catch { }
            Application.Exit();
        }

        // ── Auto-Update ──

        void CheckForUpdate()
        {
            updateBtn.Enabled = false;
            updateBtn.Text = "Checking...";
            updateStatus.Text = "";

            Thread t = new Thread(delegate() { CheckUpdateWorker(); });
            t.IsBackground = true;
            t.Start();
        }

        void CheckUpdateWorker()
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                WebClient wc = new WebClient();
                wc.Headers.Add("User-Agent", "ParasTrainer/" + APP_VERSION);
                string json = wc.DownloadString(
                    "https://api.github.com/repos/" + GITHUB_OWNER + "/" + GITHUB_REPO + "/releases/latest");

                string tagName = ParseJsonValue(json, "tag_name");
                string downloadUrl = ParseJsonValue(json, "browser_download_url");
                string body = ParseJsonValue(json, "body");
                string latestVer = tagName.TrimStart('v', 'V');

                BeginInvoke((Action)delegate {
                    if (IsNewerVersion(latestVer, APP_VERSION))
                    {
                        updateBtn.Text = "Update to v" + latestVer;
                        updateBtn.BackColor = Color.FromArgb(10, 60, 40);
                        updateBtn.ForeColor = Theme.ACCENT_GREEN;
                        updateBtn.Enabled = true;
                        updateStatus.Text = "v" + latestVer + " available";
                        updateStatus.ForeColor = Theme.ACCENT_GREEN;

                        string msg = "Update available: v" + latestVer + "\n\n"
                            + (body.Length > 0 ? body + "\n\n" : "")
                            + "Download and install now?";
                        DialogResult dr = MessageBox.Show(msg, "Paras Trainer Update",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                        if (dr == DialogResult.Yes)
                            DownloadAndApplyUpdate(downloadUrl, latestVer);
                    }
                    else
                    {
                        updateBtn.Text = "Up to Date";
                        updateBtn.ForeColor = Theme.ACCENT_GREEN;
                        updateBtn.Enabled = true;
                        updateStatus.Text = "v" + APP_VERSION + " is latest";
                        updateStatus.ForeColor = Theme.ACCENT_GREEN;
                    }
                });
            }
            catch
            {
                try
                {
                    BeginInvoke((Action)delegate {
                        updateBtn.Text = "Check for Updates";
                        updateBtn.Enabled = true;
                        updateStatus.Text = "Check failed — retry later";
                        updateStatus.ForeColor = Theme.STATUS_RED;
                    });
                }
                catch {}
            }
        }

        void DownloadAndApplyUpdate(string url, string ver)
        {
            updateBtn.Enabled = false;
            updateBtn.Text = "Downloading v" + ver + "...";

            Thread t = new Thread(delegate() { DownloadUpdateWorker(url); });
            t.IsBackground = true;
            t.Start();
        }

        void DownloadUpdateWorker(string url)
        {
            try
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string exeName = Path.GetFileName(Application.ExecutablePath);
                string newExe = Path.Combine(exeDir, "ParasTrainer_update.exe");
                string batPath = Path.Combine(exeDir, "_update.bat");

                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                WebClient wc = new WebClient();
                wc.Headers.Add("User-Agent", "ParasTrainer/" + APP_VERSION);
                wc.DownloadFile(url, newExe);

                string bat = "@echo off\r\n"
                    + ":wait\r\n"
                    + "tasklist /fi \"imagename eq " + exeName + "\" 2>nul | find /i \"" + exeName + "\" >nul\r\n"
                    + "if %errorlevel%==0 (\r\n"
                    + "    timeout /t 1 /nobreak >nul\r\n"
                    + "    goto wait\r\n"
                    + ")\r\n"
                    + "copy /y \"" + newExe + "\" \"" + Path.Combine(exeDir, exeName) + "\"\r\n"
                    + "del \"" + newExe + "\"\r\n"
                    + "start \"\" \"" + Path.Combine(exeDir, exeName) + "\"\r\n"
                    + "(goto) 2>nul & del \"%~f0\"\r\n";
                File.WriteAllText(batPath, bat);

                BeginInvoke((Action)delegate {
                    for (int i = 0; i < plugins.Length; i++)
                        if (panelBuilt[i]) SavePanelState(i);

                    ProcessStartInfo psi = new ProcessStartInfo(batPath);
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                    psi.CreateNoWindow = true;
                    Process.Start(psi);
                    Application.Exit();
                });
            }
            catch (Exception ex)
            {
                try
                {
                    BeginInvoke((Action)delegate {
                        updateBtn.Text = "Check for Updates";
                        updateBtn.BackColor = Theme.BG_SIDEBAR_CARD;
                        updateBtn.ForeColor = Theme.TEXT_SECONDARY;
                        updateBtn.Enabled = true;
                        updateStatus.Text = "Download failed";
                        updateStatus.ForeColor = Theme.STATUS_RED;
                        MessageBox.Show("Update failed: " + ex.Message, "Update Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                }
                catch {}
            }
        }

        static string ParseJsonValue(string json, string key)
        {
            string search = "\"" + key + "\"";
            int idx = json.IndexOf(search);
            if (idx < 0) return "";
            int colon = json.IndexOf(':', idx + search.Length);
            if (colon < 0) return "";
            int start = json.IndexOf('"', colon + 1);
            if (start < 0) return "";
            int end = start + 1;
            while (end < json.Length)
            {
                if (json[end] == '"' && json[end - 1] != '\\') break;
                end++;
            }
            if (end >= json.Length) return "";
            return json.Substring(start + 1, end - start - 1)
                .Replace("\\n", "\n").Replace("\\r", "").Replace("\\\"", "\"");
        }

        static bool IsNewerVersion(string remote, string local)
        {
            string[] r = remote.Split('.');
            string[] l = local.Split('.');
            int len = Math.Max(r.Length, l.Length);
            for (int i = 0; i < len; i++)
            {
                int rv = 0, lv = 0;
                if (i < r.Length) int.TryParse(r[i], out rv);
                if (i < l.Length) int.TryParse(l[i], out lv);
                if (rv > lv) return true;
                if (rv < lv) return false;
            }
            return false;
        }

        // ── Cleanup ──

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            statusPoll.Stop();
            if (pluginWatcher != null) pluginWatcher.EnableRaisingEvents = false;
            UnregisterAllHotkeys();
            for (int i = 0; i < plugins.Length; i++)
                if (panelBuilt[i]) SavePanelState(i);
            base.OnFormClosing(e);
        }

        [STAThread]
        static void Main(string[] cmdArgs)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MainForm form = new MainForm();
            if (cmdArgs.Length > 0)
            {
                int startIdx;
                if (int.TryParse(cmdArgs[0], out startIdx) && startIdx >= 0 && startIdx < form.plugins.Length)
                    form.SwitchToGame(startIdx);
            }
            Application.Run(form);
        }
    }
}
