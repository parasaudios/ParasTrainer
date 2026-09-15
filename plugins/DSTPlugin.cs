using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ParasTrainer
{
    // Don't Starve Together is a Klei Lua game — no managed runtime to inject
    // into — so cheats ship as a native client-side Lua mod ("Para's Trainer").
    // That mod is a two-way IPC bridge: it polls a command file this plugin writes
    // (%TEMP%\dst_trainer\command.txt) and runs each command on the RUNNING game
    // via the game's admin console, and writes status.txt back (live stats +
    // toggle states). So this is a full live module — the trainer drives an
    // already-running DST, WeMod-style — after a one-time "enable in Mods menu".
    //
    // Command protocol: append "<seq>|<token>\n" to command.txt. seq is a
    // milliseconds-since-2020 monotonic integer (survives trainer restarts and
    // always exceeds the mod's last-seen seq). The mod dedupes by seq. We clear
    // the file once the mod has acked everything (AckSeq >= our last seq).
    public class DSTPlugin : GamePlugin
    {
        public override string GameName { get { return "Don't Starve Together"; } }
        public override string ShortName { get { return "DST"; } }
        public override string SteamId { get { return "322330"; } }
        public override string ProcessName { get { return "dontstarve_steam_x64"; } }
        public override Color AccentColor { get { return Color.FromArgb(206, 170, 108); } }   // aged parchment gold
        public override Color AccentDimColor { get { return Color.FromArgb(58, 46, 28); } }
        public override string IconPath { get { return @"C:\Users\Para\ParasTrainer\dst.ico"; } }
        // DST hard-sandboxes `io` for mods, so the bridge goes through Klei's
        // persistent-string API — files live in the client persistent root
        // (...\Klei\DoNotStarveTogether\<account>\client_save\). We discover that
        // folder; the mod writes status.txt there and reads command.txt from there.
        private static string _ipcDir;
        public override string IpcDirectory
        {
            get { if (_ipcDir == null) _ipcDir = DiscoverClientSave(); return _ipcDir; }
        }

        private static string DiscoverClientSave()
        {
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] roots = new string[] {
                Path.Combine(user, @"OneDrive\Documents\Klei\DoNotStarveTogether"),
                Path.Combine(user, @"Documents\Klei\DoNotStarveTogether"),
            };
            string best = null;
            DateTime bestTime = DateTime.MinValue;
            foreach (string root in roots)
            {
                if (!Directory.Exists(root)) continue;
                try
                {
                    foreach (string acc in Directory.GetDirectories(root))
                    {
                        string cs = Path.Combine(acc, "client_save");
                        if (!Directory.Exists(cs)) continue;
                        DateTime t = Directory.GetLastWriteTime(cs);
                        if (t > bestTime) { bestTime = t; best = cs; }
                    }
                }
                catch { }
            }
            return best ?? Path.Combine(Path.GetTempPath(), "dst_trainer");
        }

        // Exact 11-byte Klei persistent-string header (magic + version). GetPersistentString
        // reads a file with this prefix and returns the payload; we forge it so the mod can
        // read our command file the same way it reads its own saves.
        private const string KLEI_HEADER = "KLEI     1 ";
        public override string PluginVersion { get { return "2"; } }

        private const string MODS_DIR = @"J:\SteamLibrary\steamapps\common\Don't Starve Together\mods\paras_trainer";
        private const string GAME_MODS_DIR = @"J:\SteamLibrary\steamapps\common\Don't Starve Together\mods";

        private Label liveHealth, liveHunger, liveSanity, liveAdmin, installLabel;
        private Timer setupTimer;
        private Panel playersPanel;      // dynamic per-player heal/revive rows
        private string lastPlayers = ""; // last Players= value, to rebuild only on change
        private long cmdSeq;   // last command seq we wrote
        private long ackSeq;   // last seq the mod reports having processed

        public override void InitDefaultHotkeys(Dictionary<string, Keys> b)
        {
            // None — DST hotkeys (F1-F7) are handled inside the game by the Lua
            // mod. Registering global hotkeys here would clash with them.
        }

        public override void BuildPanel(Panel c)
        {
            try { Directory.CreateDirectory(IpcDirectory); } catch { }
            int y = 10;

            // ── LIVE ──
            y = Host.ColorSectionHeader(c, y, "LIVE", AccentColor);
            Panel lc = Host.MakeCard(c, y, 2);
            liveHealth = Mk(lc, Theme.PAD, 8, "Health: -");
            liveHunger = Mk(lc, 200, 8, "Hunger: -");
            liveSanity = Mk(lc, Theme.PAD, 8 + Theme.ROW_H, "Sanity: -");
            liveAdmin = Mk(lc, 200, 8 + Theme.ROW_H, "Admin: -");
            y += Theme.ROW_H * 2 + Theme.PAD;

            Panel ncp = Host.MakeCard(c, y, 1);
            Label note = new Label();
            note.Text = "Live control of your running game (same actions are on F1-F7 in-game). "
                      + "Turn on \"Apply to ALL players\" to make every cheat and button below affect "
                      + "the whole party; leave it off and they only affect you. Heal/revive a single "
                      + "member from the PLAYERS list at the bottom.";
            note.Font = new Font("Segoe UI", 8f, FontStyle.Italic);
            note.ForeColor = Theme.TEXT_SECONDARY;
            note.Location = new Point(Theme.PAD + 2, 8);
            note.MaximumSize = new Size(Theme.CARD_W - Theme.PAD, 0);
            note.AutoSize = true;
            ncp.Controls.Add(note);
            y += Theme.ROW_H + Theme.PAD;

            // ── Scope ──
            y = Host.SectionHeader(c, y, "SCOPE");
            Panel pcard = Host.MakeCard(c, y, 1);
            DstToggleRow(pcard, 0, "party", "Apply to ALL players (party-wide)");
            y += Theme.ROW_H + Theme.PAD;

            // ── Toggles ──
            y = Host.SectionHeader(c, y, "CHEATS");
            Panel tc = Host.MakeCard(c, y, 5);
            DstToggleRow(tc, 0, "god", "God Mode (invincible)");
            Host.Divider(tc, Theme.ROW_H);
            DstToggleRow(tc, Theme.ROW_H, "freecraft", "Free Crafting");
            Host.Divider(tc, Theme.ROW_H * 2);
            DstToggleRow(tc, Theme.ROW_H * 2, "speed", "Move Speed x2");
            Host.Divider(tc, Theme.ROW_H * 3);
            DstToggleRow(tc, Theme.ROW_H * 3, "nospoil", "No Spoil (food stays fresh)");
            Host.Divider(tc, Theme.ROW_H * 4);
            DstToggleRow(tc, Theme.ROW_H * 4, "infdura", "Infinite Durability (gear)");
            y += Theme.ROW_H * 5 + Theme.PAD;

            // ── One-shot actions ──
            y = Host.SectionHeader(c, y, "ACTIONS");
            Panel ac = Host.MakeCard(c, y, 4);
            DstButton(ac, 0, "Full Restore (God + full stats + comfy)", "restore", Theme.ACCENT_GREEN);
            Host.Divider(ac, Theme.ROW_H);
            DstButton(ac, Theme.ROW_H, "Refill Health / Sanity / Hunger", "refill", Theme.TEXT_PRIMARY);
            Host.Divider(ac, Theme.ROW_H * 2);
            DstButton(ac, Theme.ROW_H * 2, "Dry Off + Warm Up", "comfort", Theme.TEXT_PRIMARY);
            Host.Divider(ac, Theme.ROW_H * 3);
            DstButton(ac, Theme.ROW_H * 3, "Revive / Heal Now", "revive", Theme.ACCENT_GREEN);
            y += Theme.ROW_H * 4 + Theme.PAD;

            // ── Setup (one-time) ──
            y = Host.ColorSectionHeader(c, y, "SETUP (one-time)", Theme.ACCENT_AMBER);
            Panel sc = Host.MakeCard(c, y, 3);
            installLabel = Mk(sc, Theme.PAD, 8, "Mod installed: ...");
            Button open = Host.ActionBtn(sc, Theme.PAD, Theme.ROW_H + 3, "Open Mods Folder", (Theme.CARD_W - Theme.PAD * 2 - 8) / 2);
            open.Click += delegate { OpenModsFolder(); };
            Button howto = Host.ActionBtn(sc, Theme.PAD + (Theme.CARD_W - Theme.PAD * 2 - 8) / 2 + 8, Theme.ROW_H + 3, "How to Enable", (Theme.CARD_W - Theme.PAD * 2 - 8) / 2);
            howto.Click += delegate { ShowEnableHelp(); };
            y += Theme.ROW_H * 3 + Theme.PAD;

            // ── Players (per-member heal/revive; populated live from status) ──
            y = Host.SectionHeader(c, y, "PLAYERS (heal / revive)");
            playersPanel = new BufferedPanel();
            playersPanel.SetBounds(Theme.PAD, y, Theme.CARD_W, Theme.ROW_H + 20);
            playersPanel.BackColor = Theme.BG_MAIN;
            c.Controls.Add(playersPanel);
            RebuildPlayers("");

            RefreshSetup();
            setupTimer = new Timer();
            setupTimer.Interval = 3000;
            setupTimer.Tick += delegate { RefreshSetup(); };
            setupTimer.Start();
        }

        // ── Command channel ──

        private static long NowMs()
        {
            return (long)(DateTime.UtcNow - new DateTime(2020, 1, 1)).TotalMilliseconds;
        }

        private readonly List<string> pending = new List<string>();

        private static long LineSeq(string line)
        {
            int bar = line.IndexOf('|');
            long v;
            if (bar > 0 && long.TryParse(line.Substring(0, bar), out v)) return v;
            return 0;
        }

        // Write the whole command file each time (persistent-string files can't be
        // appended — the KLEI header must be at the very start). We keep un-acked
        // commands queued so nothing is lost between the mod's 0.3s polls, and drop
        // them once the mod reports (AckSeq) that it has seen them.
        private void Dst(string token)
        {
            try
            {
                long s = NowMs();
                if (s <= cmdSeq) s = cmdSeq + 1;
                cmdSeq = s;
                pending.Add(cmdSeq + "|" + token);
                pending.RemoveAll(delegate(string line) { return LineSeq(line) <= ackSeq; });
                string content = KLEI_HEADER + string.Join("\n", pending.ToArray());
                Directory.CreateDirectory(IpcDirectory);
                File.WriteAllBytes(Path.Combine(IpcDirectory, "command.txt"),
                    System.Text.Encoding.ASCII.GetBytes(content));
            }
            catch { }
        }

        private void DstToggleRow(Panel card, int rowY, string feature, string label)
        {
            Label lbl = new Label();
            lbl.Text = label;
            lbl.Font = new Font("Segoe UI", 9.5f);
            lbl.ForeColor = Theme.TEXT_PRIMARY;
            lbl.Location = new Point(Theme.PAD, rowY + 10);
            lbl.AutoSize = true;
            card.Controls.Add(lbl);

            ToggleSwitch sw = new ToggleSwitch();
            sw.AccentColor = AccentColor;
            sw.Location = new Point(Theme.CARD_W - Theme.PAD - 44, rowY + 9);
            card.Controls.Add(sw);

            if (!Host.FeatureStates.ContainsKey(feature)) Host.FeatureStates[feature] = false;
            Host.ToggleSwitches[feature] = sw;
            Host.FeatureLabels[feature] = lbl;

            DSTPlugin self = this;
            sw.Toggled += delegate
            {
                Host.FeatureStates[feature] = sw.Checked;
                lbl.ForeColor = sw.Checked ? self.AccentColor : Theme.TEXT_PRIMARY;
                self.Dst(feature + (sw.Checked ? ":on" : ":off"));
            };
        }

        private void DstButton(Panel card, int rowY, string label, string token, Color fg)
        {
            Button btn = Host.ActionBtn(card, Theme.PAD, rowY + 7, label, Theme.CARD_W - Theme.PAD * 2);
            btn.ForeColor = fg;
            DSTPlugin self = this;
            btn.Click += delegate { self.Dst(token); };
        }

        // ── Setup helpers ──

        private void RefreshSetup()
        {
            bool installed = File.Exists(Path.Combine(MODS_DIR, "modmain.lua"))
                          && File.Exists(Path.Combine(MODS_DIR, "modinfo.lua"));
            if (installLabel != null)
            {
                installLabel.Text = "Mod installed: " + (installed ? "yes" : "NO");
                installLabel.ForeColor = installed ? Theme.ACCENT_GREEN : Theme.STATUS_RED;
            }
        }

        private void OpenModsFolder()
        {
            string target = Directory.Exists(MODS_DIR) ? MODS_DIR
                          : (Directory.Exists(GAME_MODS_DIR) ? GAME_MODS_DIR : null);
            if (target == null)
            {
                MessageBox.Show("Couldn't find the DST mods folder at:\n" + GAME_MODS_DIR,
                    "Para's Trainer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try { Process.Start("explorer.exe", "\"" + target + "\""); }
            catch { }
        }

        private void ShowEnableHelp()
        {
            MessageBox.Show(
                "Enable the mod (one time):\n\n" +
                "1. Launch Don't Starve Together.\n" +
                "2. Main menu -> Mods -> Client Mods.\n" +
                "3. Find \"Para's Trainer\", toggle it ON, then Apply.\n" +
                "4. Host your own world (Play -> Host Game). You're the server admin.\n\n" +
                "After that it's like installing BepInEx once: it auto-loads every\n" +
                "launch, and this panel drives it live on the running game. It's\n" +
                "client-side only, so your friends don't need it. F7 shows the keys.",
                "Para's Trainer — Don't Starve Together",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private Label Mk(Panel card, int x, int y, string text)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            l.ForeColor = AccentColor;
            l.Location = new Point(x, y);
            l.AutoSize = true;
            card.Controls.Add(l);
            return l;
        }

        private void SetStat(Label lbl, string name, string value)
        {
            if (lbl == null) return;
            lbl.Text = name + ": " + (value == "-1" ? "-" : value + "%");
        }

        // Rebuild the per-player heal/revive rows from a "uid~name;uid~name;" list.
        private void RebuildPlayers(string val)
        {
            if (playersPanel == null) return;
            playersPanel.SuspendLayout();
            playersPanel.Controls.Clear();
            string[] entries = (val ?? "").Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            int rowY = 0;
            if (entries.Length == 0)
            {
                Label none = new Label();
                none.Text = "No players yet — connect and host to see the party.";
                none.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
                none.ForeColor = Theme.TEXT_SECONDARY;
                none.Location = new Point(Theme.PAD + 2, 8);
                none.AutoSize = true;
                playersPanel.Controls.Add(none);
                rowY = Theme.ROW_H;
            }
            else
            {
                foreach (string e in entries)
                {
                    int t = e.IndexOf('~');
                    string uid = t > 0 ? e.Substring(0, t) : e;
                    string name = t >= 0 ? e.Substring(t + 1) : e;
                    string capUid = uid; // capture per-iteration for the closure

                    Label nm = new Label();
                    nm.Text = name;
                    nm.Font = new Font("Segoe UI", 9.5f);
                    nm.ForeColor = Theme.TEXT_PRIMARY;
                    nm.AutoEllipsis = true;
                    nm.SetBounds(Theme.PAD, rowY + 10, Theme.CARD_W - 150, 20);
                    playersPanel.Controls.Add(nm);

                    Button b = Host.ActionBtn(playersPanel, Theme.CARD_W - 132, rowY + 7, "Heal / Revive", 120);
                    b.ForeColor = Theme.ACCENT_GREEN;
                    b.Click += delegate { Dst("revive:" + capUid); };

                    rowY += Theme.ROW_H;
                }
            }
            playersPanel.Height = rowY + 8;
            playersPanel.ResumeLayout();
        }

        public override void ParseStatus(string key, string value)
        {
            if (key == "Health") SetStat(liveHealth, "Health", value);
            else if (key == "Hunger") SetStat(liveHunger, "Hunger", value);
            else if (key == "Sanity") SetStat(liveSanity, "Sanity", value);
            else if (key == "Admin")
            {
                bool a = value == "1";
                if (liveAdmin != null)
                {
                    liveAdmin.Text = "Admin: " + (a ? "yes" : "NO");
                    liveAdmin.ForeColor = a ? AccentColor : Color.FromArgb(220, 120, 120);
                }
            }
            else if (key == "AckSeq") { long.TryParse(value, out ackSeq); }
            else if (key == "GodMode") Host.SyncToggle("god", value == "1");
            else if (key == "FreeCraft") Host.SyncToggle("freecraft", value == "1");
            else if (key == "Speed") Host.SyncToggle("speed", value == "1");
            else if (key == "NoSpoil") Host.SyncToggle("nospoil", value == "1");
            else if (key == "InfDura") Host.SyncToggle("infdura", value == "1");
            else if (key == "Party") Host.SyncToggle("party", value == "1");
            else if (key == "Players")
            {
                if (value != lastPlayers) { lastPlayers = value; RebuildPlayers(value); }
            }
            // InGame: informational; buttons simply no-op server-side when not in game.
        }

        // Commands go through the file bridge, not the trainer's global hotkeys.
        public override void ExecuteHotkey(string action) { }
    }
}
