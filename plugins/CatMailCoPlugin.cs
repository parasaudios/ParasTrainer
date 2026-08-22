using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ParasTrainer
{
    public class CatMailCoPlugin : GamePlugin
    {
        public override string GameName { get { return "Cat Mail Co."; } }
        public override string ShortName { get { return "CatMail"; } }
        public override string SteamId { get { return "4380490"; } }
        public override string ProcessName { get { return "CatMailCo"; } }
        public override Color AccentColor { get { return Color.FromArgb(242, 165, 66); } }      // postal amber
        public override Color AccentDimColor { get { return Color.FromArgb(90, 60, 22); } }
        public override string IconPath { get { return @"C:\Users\Para\CatMailTrainer\app.ico"; } }
        public override string IpcDirectory { get { return Path.Combine(Path.GetTempPath(), "catmail_trainer"); } }
        public override string PluginVersion { get { return "1"; } }

        private TextBox scoreInput, levelInput, spawnInput;
        private Label liveScore, liveLevel, liveTime;
        private Panel cheatHost;
        private int cheatY;
        private Label cheatNote;
        private bool cheatsPopulated;

        public override void InitDefaultHotkeys(Dictionary<string, Keys> b)
        {
            b["EnableCheats"] = Keys.None; b["FreezeTime"] = Keys.None; b["AutoDeliver"] = Keys.None;
            b["AddScore"] = Keys.None; b["SetLevel"] = Keys.None; b["MaxLevel"] = Keys.None;
            b["UnlockAll"] = Keys.None; b["CycleTime"] = Keys.None; b["SpawnParcels"] = Keys.None;
            b["OpenCheatMenu"] = Keys.None;
        }

        public override void BuildPanel(Panel c)
        {
            int y = 10;

            // ── LIVE STATE ──
            y = Host.ColorSectionHeader(c, y, "LIVE", AccentColor);
            Panel lc = Host.MakeCard(c, y, 1);
            liveScore = MakeLive(lc, Theme.PAD, "Score: -");
            liveLevel = MakeLive(lc, 150, "Level: -");
            liveTime = MakeLive(lc, 270, "Time: -");
            y += Theme.ROW_H + Theme.PAD;

            // ── WORKS EVERYWHERE ──
            y = Host.ColorSectionHeader(c, y, "WORKS EVERYWHERE", Theme.ACCENT_GREEN);

            // ── CHEATS ──
            y = Host.SectionHeader(c, y, "CHEATS");
            Panel cc = Host.MakeCard(c, y, 2);
            Host.ToggleRow(cc, 0, "EnableCheats", "Enable Cheat Menu (cheatsuiop)");
            Host.Divider(cc, Theme.ROW_H);
            Button ocmBtn = Host.ActionBtn(cc, Theme.PAD, Theme.ROW_H + 7, "Open Built-in Cheat Menu", Theme.CARD_W - Theme.PAD * 2);
            ocmBtn.Click += delegate { Host.SendCommand("ACTION:OpenCheatMenu"); };
            y += Theme.ROW_H * 2 + Theme.PAD;

            // ── HOST ONLY ──
            y = Host.ColorSectionHeader(c, y, "HOST ONLY", Theme.ACCENT_AMBER);

            // ── PROGRESSION ──
            y = Host.SectionHeader(c, y, "PROGRESSION");
            Panel pc = Host.MakeCard(c, y, 4);
            scoreInput = Host.ValueRow(pc, 0, "Score +:", "1000", "Add", delegate {
                int v; if (int.TryParse(scoreInput.Text, out v)) Host.SendCommand("SET:ScoreAmountVal:" + v);
                Host.SendCommand("ACTION:AddScore");
            });
            Host.Divider(pc, Theme.ROW_H);
            levelInput = Host.ValueRow(pc, Theme.ROW_H, "Level:", "5", "Set", delegate {
                int v; if (int.TryParse(levelInput.Text, out v)) Host.SendCommand("SET:LevelTargetVal:" + v);
                Host.SendCommand("ACTION:SetLevel");
            });
            Host.Divider(pc, Theme.ROW_H * 2);
            Button maxBtn = Host.ActionBtn(pc, Theme.PAD, Theme.ROW_H * 2 + 7, "Max Level", Theme.CARD_W - Theme.PAD * 2);
            maxBtn.Click += delegate { Host.SendCommand("ACTION:MaxLevel"); };
            Host.Divider(pc, Theme.ROW_H * 3);
            Button unlockBtn = Host.ActionBtn(pc, Theme.PAD, Theme.ROW_H * 3 + 7, "Unlock All (Regions/Rooms/Scanners)", Theme.CARD_W - Theme.PAD * 2);
            unlockBtn.BackColor = Color.FromArgb(70, 48, 6);
            unlockBtn.ForeColor = Theme.ACCENT_AMBER;
            unlockBtn.Click += delegate { Host.SendCommand("ACTION:UnlockAll"); };
            y += Theme.ROW_H * 4 + Theme.PAD;

            // ── TIME ──
            y = Host.SectionHeader(c, y, "TIME");
            Panel tc = Host.MakeCard(c, y, 2);
            Host.ToggleRow(tc, 0, "FreezeTime", "Freeze Day Timer");
            Host.Divider(tc, Theme.ROW_H);
            Button cycBtn = Host.ActionBtn(tc, Theme.PAD, Theme.ROW_H + 7, "Cycle Time Period", Theme.CARD_W - Theme.PAD * 2);
            cycBtn.Click += delegate { Host.SendCommand("ACTION:CycleTime"); };
            y += Theme.ROW_H * 2 + Theme.PAD;

            // ── PARCELS ──
            y = Host.SectionHeader(c, y, "PARCELS");
            Panel prc = Host.MakeCard(c, y, 2);
            spawnInput = Host.ValueRow(prc, 0, "Spawn #:", "5", "Spawn", delegate {
                int v; if (int.TryParse(spawnInput.Text, out v)) Host.SendCommand("SET:SpawnCountVal:" + v);
                Host.SendCommand("ACTION:SpawnParcels");
            });
            Host.Divider(prc, Theme.ROW_H);
            Host.ToggleRow(prc, Theme.ROW_H, "AutoDeliver", "Auto-Deliver / Auto-Validate");
            y += Theme.ROW_H * 2 + Theme.PAD;

            // ── BUILT-IN CHEATS (auto-populated from the game's own cheat table) ──
            y = Host.ColorSectionHeader(c, y, "BUILT-IN CHEATS", Theme.ACCENT_GREEN);
            cheatHost = c;
            cheatY = y;
            cheatNote = new Label();
            cheatNote.Text = "Load into the game once — every built-in cheat appears here automatically.";
            cheatNote.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            cheatNote.ForeColor = Theme.TEXT_SECONDARY;
            cheatNote.Location = new Point(Theme.PAD + 2, y);
            cheatNote.MaximumSize = new Size(Theme.CARD_W, 0);
            cheatNote.AutoSize = true;
            c.Controls.Add(cheatNote);

            Host.AddSpacer(c, y + 60, 20);
        }

        // Called each status poll on the UI thread. Once the core has written the
        // built-in cheat table to <ipc>/cheats.txt, generate one button per cheat.
        public override void OnTick()
        {
            if (cheatsPopulated || cheatHost == null) return;
            string path = Path.Combine(IpcDirectory, "cheats.txt");
            if (!File.Exists(path)) return;

            List<string[]> cheats = new List<string[]>();
            try
            {
                foreach (string ln in File.ReadAllLines(path))
                {
                    if (string.IsNullOrEmpty(ln)) continue;
                    string[] p = ln.Split('|');           // type|combo|index|names|params
                    if (p.Length >= 3) cheats.Add(p);
                }
            }
            catch { return; }
            if (cheats.Count == 0) return;

            cheatsPopulated = true;
            if (cheatNote != null) { try { cheatHost.Controls.Remove(cheatNote); } catch { } cheatNote = null; }

            Panel card = Host.MakeCard(cheatHost, cheatY, cheats.Count);
            for (int i = 0; i < cheats.Count; i++)
            {
                string[] ch = cheats[i];
                string names = (ch.Length > 3 && ch[3].Length > 0) ? ch[3] : ("cheat " + ch[2]);
                string prm = (ch.Length > 4 && ch[4].Length > 0) ? ("  [" + ch[4] + "]") : "";
                string label = ch[1] + "  ·  " + names + prm;
                Button b = Host.ActionBtn(card, Theme.PAD, i * Theme.ROW_H + 7, label, Theme.CARD_W - Theme.PAD * 2);
                b.TextAlign = ContentAlignment.MiddleLeft;
                string cmd = "ACTION:Cheat:" + ch[0] + ":" + ch[2];
                b.Click += delegate { Host.SendCommand(cmd); };
            }
        }

        private Label MakeLive(Panel card, int x, string text)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            l.ForeColor = AccentColor;
            l.Location = new Point(x, 10);
            l.AutoSize = true;
            card.Controls.Add(l);
            return l;
        }

        public override void ParseStatus(string key, string value)
        {
            int i;
            if (key == "ScoreAmountVal" && scoreInput != null)
            {
                if (int.TryParse(value, out i)) Host.SyncInput(scoreInput, i.ToString());
            }
            else if (key == "LevelTargetVal" && levelInput != null)
            {
                if (int.TryParse(value, out i)) Host.SyncInput(levelInput, i.ToString());
            }
            else if (key == "SpawnCountVal" && spawnInput != null)
            {
                if (int.TryParse(value, out i)) Host.SyncInput(spawnInput, i.ToString());
            }
            else if (key == "CurrentScore" && liveScore != null) { liveScore.Text = "Score: " + value; }
            else if (key == "CurrentLevel" && liveLevel != null) { liveLevel.Text = "Level: " + value; }
            else if (key == "CurrentTime" && liveTime != null) { liveTime.Text = "Time: " + value; }
            else if (value == "0" || value == "1")
            {
                Host.SyncToggle(key, value == "1");
            }
        }

        public override void ExecuteHotkey(string action)
        {
            if (action == "EnableCheats" || action == "FreezeTime" || action == "AutoDeliver")
                Host.SendCommand("TOGGLE:" + action);
            else
                Host.SendCommand("ACTION:" + action);
        }

        public override List<string> SaveExtraState()
        {
            List<string> lines = new List<string>();
            if (scoreInput != null) lines.Add("Val_Score=" + scoreInput.Text);
            if (levelInput != null) lines.Add("Val_Level=" + levelInput.Text);
            if (spawnInput != null) lines.Add("Val_Spawn=" + spawnInput.Text);
            return lines;
        }

        public override void LoadExtraState(Dictionary<string, string> data)
        {
            string v;
            if (data.TryGetValue("Val_Score", out v) && scoreInput != null) scoreInput.Text = v;
            if (data.TryGetValue("Val_Level", out v) && levelInput != null) levelInput.Text = v;
            if (data.TryGetValue("Val_Spawn", out v) && spawnInput != null) spawnInput.Text = v;
        }
    }
}
