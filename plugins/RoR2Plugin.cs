using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ParasTrainer
{
    public class RoR2Plugin : GamePlugin
    {
        public override string GameName { get { return "Risk of Rain 2"; } }
        public override string ShortName { get { return "ROR2"; } }
        public override string SteamId { get { return "632360"; } }
        public override string ProcessName { get { return "Risk of Rain 2"; } }
        public override Color AccentColor { get { return Color.FromArgb(220, 60, 60); } }
        public override Color AccentDimColor { get { return Color.FromArgb(100, 34, 34); } }
        public override string IconPath { get { return @"C:\Users\Para\RoR2Trainer\app.ico"; } }
        public override string IpcDirectory { get { return Path.Combine(Path.GetTempPath(), "ror2_trainer"); } }
        public override string PluginVersion { get { return "73"; } }

        private TextBox itemMultInput;
        private TextBox iemDmg, iemAtkSpd, iemCrit, iemMoveSpd, iemSurv, iemOther;
        private TextBox vacuumRange, itemVacRange, lunarInput, goldInput;
        private ComboBox buffCombo, respawnMode, targetCombo, itemCombo, bodyCombo, hexTargetCombo;
        private TextBox itemSearch, lootRange;
        private NumericUpDown itemCount;
        private CheckBox[] espChecks;
        private string[] espKeys = { "ChestSmall", "ChestLarge", "Legendary", "Equipment", "Lunar", "Void",
                                      "Multishop", "Printer", "Scrapper", "Shrine", "Newt", "Teleporter" };
        private string[] espLabels = { "Small Chest", "Large Chest", "Legendary", "Equipment", "Lunar", "Void",
                                        "Multishop", "Printer", "Scrapper", "Shrine", "Newt Altar", "Teleporter" };
        private bool catalogLoaded;
        private bool espLoading;
        private List<string[]> items = new List<string[]>();

        // Survivor bodies for the Respawn-As dropdown.
        private string[][] bodyList = {
            new string[]{"Commando","CommandoBody"}, new string[]{"Huntress","HuntressBody"},
            new string[]{"MUL-T","ToolbotBody"}, new string[]{"Engineer","EngiBody"},
            new string[]{"Artificer","MageBody"}, new string[]{"Mercenary","MercBody"},
            new string[]{"REX","TreebotBody"}, new string[]{"Loader","LoaderBody"},
            new string[]{"Acrid","CrocoBody"}, new string[]{"Captain","CaptainBody"},
            new string[]{"Bandit","BanditBody"}, new string[]{"Railgunner","RailgunnerBody"},
            new string[]{"Void Fiend","VoidSurvivorBody"}, new string[]{"Seeker","SeekerBody"},
            new string[]{"False Son","FalseSonBody"}, new string[]{"Chef","ChefBody"},
            new string[]{"Heretic","HereticBody"}
        };

        public override void InitDefaultHotkeys(Dictionary<string, Keys> b)
        {
            b["GodMode"] = Keys.F2; b["InfiniteMoney"] = Keys.F3; b["NoCooldowns"] = Keys.F4;
            b["KillAll"] = Keys.F5; b["FreePurchases"] = Keys.F6; b["DamageMultiplier"] = Keys.F7;
            b["Flight"] = Keys.F9; b["AlwaysCrit"] = Keys.None;
            b["MaxArmor"] = Keys.None; b["MaxLuck"] = Keys.None; b["InfiniteEquipment"] = Keys.None;
            b["InfiniteJump"] = Keys.None;
            b["HiddenAtkSpeed"] = Keys.None; b["HiddenMoveSpeed"] = Keys.None;
            b["HiddenArmor"] = Keys.None; b["HiddenFire"] = Keys.None;
            b["HiddenFrost"] = Keys.None; b["HiddenInvuln"] = Keys.None;
            b["BoostedRewards"] = Keys.None; b["DifficultyFreeze"] = Keys.None;
            b["ItemMultiplier"] = Keys.None; b["BonusRandomItem"] = Keys.None;
            b["FreeScrap"] = Keys.None; b["ItemEffectMult"] = Keys.None;
            b["SaleStarPersist"] = Keys.None; b["Esp"] = Keys.None;
            b["MoneyVacuum"] = Keys.None;
            b["VacuumItems"] = Keys.F10; b["LootNearby"] = Keys.None;
            b["InfiniteLunar"] = Keys.None;
        }

        // ── Top-level: split the panel into Non-Host and Host tabs ──
        public override void BuildPanel(Panel c)
        {
            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.SizeMode = TabSizeMode.Fixed;
            tabs.ItemSize = new Size(((Theme.CARD_W + Theme.PAD * 2) / 2), 30);
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.DrawItem += DrawTab;

            TabPage nonHostTab = new TabPage("NON-HOST"); nonHostTab.BackColor = Theme.BG_MAIN;
            TabPage hostTab = new TabPage("HOST"); hostTab.BackColor = Theme.BG_MAIN;
            tabs.TabPages.Add(nonHostTab);
            tabs.TabPages.Add(hostTab);
            c.Controls.Add(tabs);

            BufferedPanel nhp = new BufferedPanel();
            nhp.Dock = DockStyle.Fill; nhp.AutoScroll = true; nhp.BackColor = Theme.BG_MAIN;
            nonHostTab.Controls.Add(nhp);
            BufferedPanel hp = new BufferedPanel();
            hp.Dock = DockStyle.Fill; hp.AutoScroll = true; hp.BackColor = Theme.BG_MAIN;
            hostTab.Controls.Add(hp);

            BuildNonHostTab(nhp);
            BuildHostTab(hp);
        }

        private void DrawTab(object s, DrawItemEventArgs e)
        {
            TabControl tc = (TabControl)s;
            bool sel = e.Index == tc.SelectedIndex;
            Rectangle r = e.Bounds;
            using (SolidBrush bg = new SolidBrush(sel ? Theme.BG_CARD : Theme.BG_HEADER))
                e.Graphics.FillRectangle(bg, r);
            if (sel)
                using (SolidBrush acc = new SolidBrush(AccentColor))
                    e.Graphics.FillRectangle(acc, r.Left, r.Bottom - 3, r.Width, 3);
            TextRenderer.DrawText(e.Graphics, tc.TabPages[e.Index].Text,
                new Font("Segoe UI", 9.5f, FontStyle.Bold), r,
                sel ? AccentColor : Theme.TEXT_SECONDARY,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        // ════════════════════════════════════════════════════════════════
        //  NON-HOST TAB — works as a client (stats, buffs, movement, respawn, ESP, IEM)
        // ════════════════════════════════════════════════════════════════
        private void BuildNonHostTab(Panel c)
        {
            int y = 10;

            // COMBAT
            y = Host.SectionHeader(c, y, "COMBAT");
            Panel cc = Host.MakeCard(c, y, 2);
            Host.ToggleRow(cc, 0, "GodMode", "God Mode");
            Host.Divider(cc, Theme.ROW_H);
            Host.ToggleRow(cc, Theme.ROW_H, "NoCooldowns", "No Cooldowns");
            y += Theme.ROW_H * 2 + Theme.PAD;

            // BUFF STACKING
            y = Host.SectionHeader(c, y, "BUFF STACKING");
            Panel bsc = Host.MakeCard(c, y, 10);
            Label hbLbl = new Label();
            hbLbl.Text = "  HIDDEN (invisible to spectators)";
            hbLbl.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            hbLbl.ForeColor = Color.FromArgb(100, 220, 100);
            hbLbl.BackColor = Theme.BG_CARD;
            hbLbl.SetBounds(0, 8, Theme.CARD_W, 20);
            bsc.Controls.Add(hbLbl);
            Host.Divider(bsc, Theme.ROW_H);
            Host.ToggleRow(bsc, Theme.ROW_H, "HiddenAtkSpeed", "Attack Speed + Armor");
            Host.Divider(bsc, Theme.ROW_H * 2);
            Host.ToggleRow(bsc, Theme.ROW_H * 2, "HiddenMoveSpeed", "Move Speed + Regen");
            Host.Divider(bsc, Theme.ROW_H * 3);
            Host.ToggleRow(bsc, Theme.ROW_H * 3, "HiddenArmor", "Armor");
            Host.Divider(bsc, Theme.ROW_H * 4);
            Host.ToggleRow(bsc, Theme.ROW_H * 4, "HiddenFire", "Fire Effect");
            Host.Divider(bsc, Theme.ROW_H * 5);
            Host.ToggleRow(bsc, Theme.ROW_H * 5, "HiddenFrost", "Frost Effect");
            Host.Divider(bsc, Theme.ROW_H * 6);
            Host.ToggleRow(bsc, Theme.ROW_H * 6, "HiddenInvuln", "Invulnerability");
            Host.Divider(bsc, Theme.ROW_H * 7);
            Label vbLbl = new Label();
            vbLbl.Text = "  VISIBLE (spectators can see)";
            vbLbl.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            vbLbl.ForeColor = Color.FromArgb(255, 180, 60);
            vbLbl.BackColor = Theme.BG_CARD;
            vbLbl.SetBounds(0, Theme.ROW_H * 7 + 8, Theme.CARD_W, 20);
            bsc.Controls.Add(vbLbl);
            Host.Divider(bsc, Theme.ROW_H * 8);
            Host.ToggleRow(bsc, Theme.ROW_H * 8, "DamageMultiplier", "Damage Multiplier");
            Host.Divider(bsc, Theme.ROW_H * 9);
            Host.ToggleRow(bsc, Theme.ROW_H * 9, "AlwaysCrit", "Always Crit");
            y += Theme.ROW_H * 10 + Theme.PAD;

            // MOVEMENT
            y = Host.SectionHeader(c, y, "MOVEMENT");
            Panel mc = Host.MakeCard(c, y, 2);
            Host.ToggleRow(mc, 0, "InfiniteJump", "Infinite Jump");
            Host.Divider(mc, Theme.ROW_H);
            Host.ToggleRow(mc, Theme.ROW_H, "Flight", "Flight (No Gravity)");
            y += Theme.ROW_H * 2 + Theme.PAD;

            // ITEM VACUUM — one-shot button + hotkey. Works as a client too: moved pickups stick
            // locally (no position sync) and Command essences are grabbed by interacting.
            y = Host.SectionHeader(c, y, "ITEM VACUUM");
            Panel ivc = Host.MakeCard(c, y, 2);
            Button ivBtn = Host.ActionBtn(ivc, Theme.PAD, 7, "Vacuum Items (sort by tier)", Theme.CARD_W - Theme.PAD * 2 - 54);
            ivBtn.BackColor = Color.FromArgb(15, 55, 30);
            ivBtn.ForeColor = Theme.ACCENT_GREEN;
            ivBtn.Click += delegate { Host.SendCommand("VACUUM_ITEMS"); };
            Host.HotkeyBtn(ivc, Theme.CARD_W - Theme.PAD - 46, 9, "VacuumItems");
            Host.Divider(ivc, Theme.ROW_H);
            itemVacRange = Host.ValueRow(ivc, Theme.ROW_H, "Range:", "70", "Apply", delegate {
                int v; if (int.TryParse(itemVacRange.Text, out v))
                { v = Math.Max(10, Math.Min(300, v)); Host.SendCommand("SET:ItemVacuumRange:" + v); }
            });
            y += Theme.ROW_H * 2 + Theme.PAD;

            // ITEM EFFECT MULTIPLIER (client-side transpiler — works as non-host)
            y = Host.SectionHeader(c, y, "ITEM EFFECT MULTIPLIER");
            Panel iemc = Host.MakeCard(c, y, 8);
            Host.ToggleRow(iemc, 0, "ItemEffectMult", "Item Effect Multiplier");
            Host.Divider(iemc, Theme.ROW_H);
            string[] iemLabels = { "Damage:", "Atk Speed:", "Crit:", "Move Spd:", "Survival:", "All Other:" };
            string[] iemKeys = { "IemDamage", "IemAtkSpd", "IemCrit", "IemMoveSpd", "IemSurvival", "IemOther" };
            TextBox[] iemInputs = new TextBox[6];
            for (int i = 0; i < 6; i++)
            {
                int row = i + 1;
                Label il = new Label();
                il.Text = iemLabels[i];
                il.Font = new Font("Segoe UI", 9f);
                il.ForeColor = Theme.TEXT_SECONDARY;
                il.Location = new Point(Theme.PAD + 18, Theme.ROW_H * row + 12);
                il.AutoSize = true;
                iemc.Controls.Add(il);
                iemInputs[i] = Host.MakeInput(iemc, 130, Theme.ROW_H * row + 8, 60, "1");
                TextBox iemTb = iemInputs[i];
                string iemKey = iemKeys[i];
                iemTb.TextChanged += delegate { Host.DirtyInputs.Add(iemTb); };   // don't let the poll clobber edits
                // Auto-apply as soon as the user finishes editing (focus lost) or presses Enter —
                // no separate "Set All" click required.
                System.EventHandler iemApply = delegate {
                    int v; if (int.TryParse(iemTb.Text, out v))
                    {
                        v = Math.Max(1, Math.Min(100, v));
                        if (v.ToString() != iemTb.Text) iemTb.Text = v.ToString();
                        Host.SendCommand("SET:" + iemKey + ":" + v);
                        Host.AppliedAt[iemTb] = DateTime.Now;
                    }
                };
                iemTb.Leave += iemApply;
                iemTb.KeyDown += delegate(object s2, KeyEventArgs e2) {
                    if (e2.KeyCode == Keys.Enter) { e2.SuppressKeyPress = true; iemApply(s2, EventArgs.Empty); }
                };
                if (i < 5) Host.Divider(iemc, Theme.ROW_H * (row + 1));
            }
            iemDmg = iemInputs[0]; iemAtkSpd = iemInputs[1]; iemCrit = iemInputs[2];
            iemMoveSpd = iemInputs[3]; iemSurv = iemInputs[4]; iemOther = iemInputs[5];
            Host.Divider(iemc, Theme.ROW_H * 7);
            Button setAllBtn = Host.ActionBtn(iemc, Theme.PAD, Theme.ROW_H * 7 + 7, "Set All", Theme.CARD_W - Theme.PAD * 2);
            setAllBtn.BackColor = Color.FromArgb(15, 55, 30);
            setAllBtn.ForeColor = Theme.ACCENT_GREEN;
            setAllBtn.Click += delegate {
                string[] keys = { "IemDamage", "IemAtkSpd", "IemCrit", "IemMoveSpd", "IemSurvival", "IemOther" };
                TextBox[] tbs = { iemDmg, iemAtkSpd, iemCrit, iemMoveSpd, iemSurv, iemOther };
                for (int i = 0; i < 6; i++)
                {
                    int v; if (int.TryParse(tbs[i].Text, out v))
                    { v = Math.Max(1, Math.Min(100, v)); Host.SendCommand("SET:" + keys[i] + ":" + v); Host.AppliedAt[tbs[i]] = DateTime.Now; }
                }
            };
            y += Theme.ROW_H * 8 + Theme.PAD;

            // APPLY BUFF
            y = Host.SectionHeader(c, y, "APPLY BUFF");
            Panel bfc = Host.MakeCard(c, y, 1);
            {
                Label bl = new Label();
                bl.Text = "Buff:";
                bl.Font = new Font("Segoe UI", 9f);
                bl.ForeColor = Theme.ACCENT_GREEN;
                bl.Location = new Point(Theme.PAD, 12);
                bl.AutoSize = true;
                bfc.Controls.Add(bl);
                buffCombo = new ComboBox();
                buffCombo.DropDownStyle = ComboBoxStyle.DropDownList;
                buffCombo.SetBounds(60, 8, 240, 24);
                buffCombo.BackColor = Theme.BG_INPUT;
                buffCombo.ForeColor = Theme.TEXT_PRIMARY;
                buffCombo.Font = new Font("Segoe UI", 8.5f);
                string[] buffs = {
                    "bdCloak (Invisibility)", "bdWarBannerBuff (Warbanner Speed+)",
                    "bdEnergized (Attack Speed+)", "bdTeslaField (Lightning Shield)",
                    "bdKillMoveSpeed (On-Kill Speed)", "bdCrocoRegen (Regeneration)",
                    "bdFullCrit (100% Crit)", "bdAttackSpeedOnCrit (Crit Attack Speed)"
                };
                buffCombo.Items.AddRange(buffs);
                buffCombo.SelectedIndex = 0;
                bfc.Controls.Add(buffCombo);
                Button abBtn = Host.ActionBtn(bfc, 308, 7, "Apply", 80);
                abBtn.BackColor = Color.FromArgb(15, 55, 30);
                abBtn.ForeColor = Theme.ACCENT_GREEN;
                abBtn.Click += delegate {
                    string sel = buffCombo.SelectedItem.ToString();
                    string bn = sel.Split(' ')[0];
                    Host.SendCommand("APPLY_BUFF:" + bn);
                };
            }
            y += Theme.ROW_H + Theme.PAD;

            // RESPAWN & TELEPORTER (respawn works as non-host via ForceRespawn)
            y = Host.SectionHeader(c, y, "RESPAWN & TELEPORTER");
            Panel rc = Host.MakeCard(c, y, 5);
            Button revBtn = Host.ActionBtn(rc, Theme.PAD, 7, "Revive / Respawn (One-Shot)", Theme.CARD_W - Theme.PAD * 2);
            revBtn.BackColor = Color.FromArgb(20, 60, 80);
            revBtn.ForeColor = Color.FromArgb(100, 200, 255);
            revBtn.Click += delegate { Host.SendCommand("RESPAWN"); };
            Host.Divider(rc, Theme.ROW_H);

            Label raLbl = new Label();
            raLbl.Text = "Respawn as:";
            raLbl.Font = new Font("Segoe UI", 9f);
            raLbl.ForeColor = Theme.TEXT_SECONDARY;
            raLbl.Location = new Point(Theme.PAD, Theme.ROW_H + 12);
            raLbl.AutoSize = true;
            rc.Controls.Add(raLbl);
            bodyCombo = new ComboBox();
            bodyCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            bodyCombo.SetBounds(100, Theme.ROW_H + 8, 180, 24);
            bodyCombo.BackColor = Theme.BG_INPUT;
            bodyCombo.ForeColor = Theme.TEXT_PRIMARY;
            foreach (string[] bd in bodyList) bodyCombo.Items.Add(bd[0]);
            bodyCombo.SelectedIndex = 0;
            rc.Controls.Add(bodyCombo);
            Button raBtn = Host.ActionBtn(rc, 288, Theme.ROW_H + 7, "Go", Theme.CARD_W - 288 - Theme.PAD);
            raBtn.BackColor = Color.FromArgb(20, 60, 80);
            raBtn.ForeColor = Color.FromArgb(100, 200, 255);
            raBtn.Click += delegate {
                if (bodyCombo.SelectedIndex >= 0)
                    Host.SendCommand("RESPAWN_AS:" + bodyList[bodyCombo.SelectedIndex][1]);
            };
            Host.Divider(rc, Theme.ROW_H * 2);

            int halfW = (Theme.CARD_W - Theme.PAD * 3) / 2;
            Button ftpBtn = Host.ActionBtn(rc, Theme.PAD, Theme.ROW_H * 2 + 7, "Force Teleporter", halfW);
            ftpBtn.BackColor = Color.FromArgb(20, 60, 80);
            ftpBtn.ForeColor = Color.FromArgb(100, 200, 255);
            ftpBtn.Click += delegate { Host.SendCommand("FORCE_TP"); };
            Button usBtn = Host.ActionBtn(rc, Theme.PAD + halfW + Theme.PAD, Theme.ROW_H * 2 + 7, "Unstuck", halfW);
            usBtn.BackColor = Color.FromArgb(80, 60, 10);
            usBtn.ForeColor = Color.FromArgb(255, 200, 60);
            usBtn.Click += delegate { Host.SendCommand("UNSTUCK"); };
            Host.Divider(rc, Theme.ROW_H * 3);

            // Loot Nearby (remote interact — works as non-host)
            Label lnLbl = new Label();
            lnLbl.Text = "Loot range:";
            lnLbl.Font = new Font("Segoe UI", 9f);
            lnLbl.ForeColor = Theme.ACCENT_GREEN;
            lnLbl.Location = new Point(Theme.PAD, Theme.ROW_H * 3 + 12);
            lnLbl.AutoSize = true;
            rc.Controls.Add(lnLbl);
            lootRange = Host.MakeInput(rc, 100, Theme.ROW_H * 3 + 8, 56, "80");
            Button lnBtn = Host.ActionBtn(rc, 164, Theme.ROW_H * 3 + 7, "Loot Nearby (open chests/barrels)", Theme.CARD_W - 164 - Theme.PAD - 54);
            lnBtn.BackColor = Color.FromArgb(15, 55, 30);
            lnBtn.ForeColor = Theme.ACCENT_GREEN;
            lnBtn.Click += delegate {
                string r = lootRange.Text.Trim(); if (r.Length == 0) r = "80";
                Host.SendCommand("LOOT_NEARBY:" + r);
            };
            Host.HotkeyBtn(rc, Theme.CARD_W - Theme.PAD - 46, Theme.ROW_H * 3 + 9, "LootNearby");
            y += Theme.ROW_H * 5 + Theme.PAD;

            // LUNAR COINS
            y = Host.SectionHeader(c, y, "LUNAR COINS");
            Panel lcc = Host.MakeCard(c, y, 2);
            Host.ToggleRow(lcc, 0, "InfiniteLunar", "Infinite Lunar Coins");
            Host.Divider(lcc, Theme.ROW_H);
            {
                Label ll = new Label();
                ll.Text = "Lunar Coins:";
                ll.Font = new Font("Segoe UI", 9f);
                ll.ForeColor = Color.FromArgb(100, 160, 255);
                ll.Location = new Point(Theme.PAD, Theme.ROW_H + 12);
                ll.AutoSize = true;
                lcc.Controls.Add(ll);
                lunarInput = Host.MakeInput(lcc, 120, Theme.ROW_H + 8, 80, "1000");
                Button lsBtn = Host.ActionBtn(lcc, 208, Theme.ROW_H + 7, "Set", 52);
                lsBtn.Click += delegate {
                    uint v; if (uint.TryParse(lunarInput.Text, out v))
                        Host.SendCommand("SET_LUNAR:" + v);
                };
            }
            y += Theme.ROW_H * 2 + Theme.PAD;

            // DLC UNLOCKS (profile-side — work as non-host)
            y = Host.SectionHeader(c, y, "HALLOWED CONCEPTS (DLC)");
            Panel dlc = Host.MakeCard(c, y, 2);
            int dHalf = (Theme.CARD_W - Theme.PAD * 3) / 2;
            Button acolyteBtn = Host.ActionBtn(dlc, Theme.PAD, 7, "Unlock Acolyte", dHalf);
            acolyteBtn.BackColor = Color.FromArgb(40, 30, 70);
            acolyteBtn.ForeColor = Color.FromArgb(160, 140, 240);
            acolyteBtn.Click += delegate { Host.SendCommand("UNLOCK_ACOLYTE"); };
            Button unlockAllBtn = Host.ActionBtn(dlc, Theme.PAD + dHalf + Theme.PAD, 7, "Unlock All", dHalf);
            unlockAllBtn.BackColor = Color.FromArgb(30, 55, 40);
            unlockAllBtn.ForeColor = Color.FromArgb(120, 220, 150);
            unlockAllBtn.Click += delegate { Host.SendCommand("UNLOCK_ALL"); };
            Host.Divider(dlc, Theme.ROW_H);
            // Hex give/remove target selector (host-only — inventory is server-authoritative).
            Label hexTgtLbl = new Label();
            hexTgtLbl.Text = "Hexes (host):";
            hexTgtLbl.Font = new Font("Segoe UI", 9f);
            hexTgtLbl.ForeColor = Color.FromArgb(220, 130, 245);
            hexTgtLbl.Location = new Point(Theme.PAD, Theme.ROW_H + 12);
            hexTgtLbl.AutoSize = true;
            dlc.Controls.Add(hexTgtLbl);
            hexTargetCombo = new ComboBox();
            hexTargetCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            hexTargetCombo.SetBounds(100, Theme.ROW_H + 8, Theme.CARD_W - Theme.PAD * 2 - 100, 24);
            hexTargetCombo.BackColor = Theme.BG_INPUT;
            hexTargetCombo.ForeColor = Theme.TEXT_PRIMARY;
            hexTargetCombo.Items.Add("All Players");   // refreshed with the live roster every tick
            hexTargetCombo.SelectedIndex = 0;
            dlc.Controls.Add(hexTargetCombo);
            Host.Divider(dlc, Theme.ROW_H * 2);
            Button hexBtn = Host.ActionBtn(dlc, Theme.PAD, Theme.ROW_H * 2 + 7, "Give Hexes", dHalf);
            hexBtn.BackColor = Color.FromArgb(60, 20, 70);
            hexBtn.ForeColor = Color.FromArgb(220, 130, 245);
            hexBtn.Click += delegate { Host.SendCommand("GIVE_HEXES:" + HexTargetIdx()); };
            Button hexRemoveBtn = Host.ActionBtn(dlc, Theme.PAD + dHalf + Theme.PAD, Theme.ROW_H * 2 + 7, "Remove Hexes", dHalf);
            hexRemoveBtn.BackColor = Color.FromArgb(70, 20, 30);
            hexRemoveBtn.ForeColor = Color.FromArgb(245, 140, 150);
            hexRemoveBtn.Click += delegate { Host.SendCommand("REMOVE_HEXES:" + HexTargetIdx()); };
            y += Theme.ROW_H * 3 + Theme.PAD;

            // MAP HACK (ESP — client-side overlay)
            y = Host.SectionHeader(c, y, "MAP HACK / ESP");
            int espCheckRows = 7;
            Panel espc = Host.MakeCard(c, y, espCheckRows);
            Host.ToggleRow(espc, 0, "Esp", "ESP Overlay", true);
            Host.Divider(espc, Theme.ROW_H);
            espChecks = new CheckBox[12];
            for (int i = 0; i < 12; i++)
            {
                int col = i % 2;
                int row = i / 2;
                CheckBox cb = new CheckBox();
                cb.Text = espLabels[i];
                cb.Font = new Font("Segoe UI", 8.5f);
                cb.ForeColor = Theme.TEXT_PRIMARY;
                cb.BackColor = Theme.BG_CARD;
                cb.Checked = true;
                cb.SetBounds(Theme.PAD + col * 230, Theme.ROW_H + 8 + row * 28, 210, 22);
                int ci = i;
                cb.CheckedChanged += delegate {
                    if (!espLoading)
                        Host.SendCommand("ESP_CAT:" + espKeys[ci] + ":" + (cb.Checked ? "1" : "0"));
                };
                espc.Controls.Add(cb);
                espChecks[i] = cb;
            }
            y += Theme.ROW_H * espCheckRows + Theme.PAD;

            Host.AddSpacer(c, y, 20);
        }

        // ════════════════════════════════════════════════════════════════
        //  HOST TAB — needs to be hosting (server-authoritative: money, items, vacuum…)
        // ════════════════════════════════════════════════════════════════
        private void BuildHostTab(Panel c)
        {
            int y = 10;

            // HOST TOGGLES
            y = Host.SectionHeader(c, y, "HOST TOGGLES");
            Panel htc = Host.MakeCard(c, y, 8);
            string[] hostToggles = { "InfiniteMoney", "FreePurchases", "BoostedRewards", "MaxArmor",
                                      "MaxLuck", "InfiniteEquipment", "DifficultyFreeze", "SaleStarPersist" };
            string[] hostLabels = { "Infinite Money", "Free Purchases", "Boosted Rewards (10x)", "Max Armor",
                                     "Max Luck", "Infinite Equipment", "Difficulty Freeze", "Permanent Sale Stars" };
            for (int i = 0; i < 8; i++)
            {
                Host.ToggleRow(htc, Theme.ROW_H * i, hostToggles[i], hostLabels[i]);
                if (i < 7) Host.Divider(htc, Theme.ROW_H * (i + 1));
            }
            y += Theme.ROW_H * 8 + Theme.PAD;

            // ITEM BOOST
            y = Host.SectionHeader(c, y, "ITEM BOOST");
            Panel ibc = Host.MakeCard(c, y, 4);
            Host.ToggleRow(ibc, 0, "ItemMultiplier", "Item Multiplier");
            Host.Divider(ibc, Theme.ROW_H);
            itemMultInput = Host.ValueRow(ibc, Theme.ROW_H, "Multiplier:", "2", "Apply", delegate {
                int v; if (int.TryParse(itemMultInput.Text, out v) && v > 0)
                    Host.SendCommand("SET:ItemMultiplierVal:" + v);
            });
            Host.Divider(ibc, Theme.ROW_H * 2);
            Host.ToggleRow(ibc, Theme.ROW_H * 2, "BonusRandomItem", "Bonus Random Item");
            Host.Divider(ibc, Theme.ROW_H * 3);
            Host.ToggleRow(ibc, Theme.ROW_H * 3, "FreeScrap", "Free Scrap on Pickup");
            y += Theme.ROW_H * 4 + Theme.PAD;

            // MONEY VACUUM
            y = Host.SectionHeader(c, y, "MONEY VACUUM");
            Panel mvc = Host.MakeCard(c, y, 2);
            Host.ToggleRow(mvc, 0, "MoneyVacuum", "Money Vacuum");
            Host.Divider(mvc, Theme.ROW_H);
            vacuumRange = Host.ValueRow(mvc, Theme.ROW_H, "Range:", "50", "Apply", delegate {
                int v; if (int.TryParse(vacuumRange.Text, out v))
                { v = Math.Max(10, Math.Min(200, v)); Host.SendCommand("SET:VacuumRange:" + v); }
            });
            y += Theme.ROW_H * 2 + Theme.PAD;

            // ACTIONS
            y = Host.SectionHeader(c, y, "ACTIONS");
            Panel kac = Host.MakeCard(c, y, 1);
            Button kaBtn = Host.ActionBtn(kac, Theme.PAD, 7, "Kill All Enemies", Theme.CARD_W - Theme.PAD * 2 - 54);
            kaBtn.BackColor = Color.FromArgb(80, 20, 20);
            kaBtn.ForeColor = Color.FromArgb(255, 120, 120);
            kaBtn.Click += delegate { Host.SendCommand("KILL_ALL"); };
            Host.HotkeyBtn(kac, Theme.CARD_W - Theme.PAD - 46, 9, "KillAll");
            y += Theme.ROW_H + Theme.PAD;

            // GIVE ITEMS
            y = Host.SectionHeader(c, y, "GIVE ITEMS");
            Panel gic = Host.MakeCard(c, y, 5);
            Label tgtLbl = new Label();
            tgtLbl.Text = "Target:";
            tgtLbl.Font = new Font("Segoe UI", 9f);
            tgtLbl.ForeColor = Theme.TEXT_SECONDARY;
            tgtLbl.Location = new Point(Theme.PAD, 12);
            tgtLbl.AutoSize = true;
            gic.Controls.Add(tgtLbl);
            targetCombo = new ComboBox();
            targetCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            targetCombo.SetBounds(80, 8, 200, 24);
            targetCombo.BackColor = Theme.BG_INPUT;
            targetCombo.ForeColor = Theme.TEXT_PRIMARY;
            targetCombo.Items.Add("All Players");   // refreshed with the live roster every tick (RefreshTargetList)
            targetCombo.SelectedIndex = 0;
            gic.Controls.Add(targetCombo);
            Host.Divider(gic, Theme.ROW_H);

            Label srLbl = new Label();
            srLbl.Text = "Search:";
            srLbl.Font = new Font("Segoe UI", 9f);
            srLbl.ForeColor = Theme.TEXT_SECONDARY;
            srLbl.Location = new Point(Theme.PAD, Theme.ROW_H + 12);
            srLbl.AutoSize = true;
            gic.Controls.Add(srLbl);
            itemSearch = Host.MakeInput(gic, 80, Theme.ROW_H + 8, 200, "");
            itemSearch.TextChanged += delegate { FilterItems(); };
            Host.Divider(gic, Theme.ROW_H * 2);

            Label itLbl = new Label();
            itLbl.Text = "Item:";
            itLbl.Font = new Font("Segoe UI", 9f);
            itLbl.ForeColor = Theme.TEXT_SECONDARY;
            itLbl.Location = new Point(Theme.PAD, Theme.ROW_H * 2 + 12);
            itLbl.AutoSize = true;
            gic.Controls.Add(itLbl);
            itemCombo = new ComboBox();
            itemCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            itemCombo.SetBounds(60, Theme.ROW_H * 2 + 8, 340, 24);
            itemCombo.BackColor = Theme.BG_INPUT;
            itemCombo.ForeColor = Theme.TEXT_PRIMARY;
            gic.Controls.Add(itemCombo);
            Host.Divider(gic, Theme.ROW_H * 3);

            Label cntLbl = new Label();
            cntLbl.Text = "Count:";
            cntLbl.Font = new Font("Segoe UI", 9f);
            cntLbl.ForeColor = Theme.TEXT_SECONDARY;
            cntLbl.Location = new Point(Theme.PAD, Theme.ROW_H * 3 + 12);
            cntLbl.AutoSize = true;
            gic.Controls.Add(cntLbl);
            itemCount = new NumericUpDown();
            itemCount.SetBounds(80, Theme.ROW_H * 3 + 8, 80, 24);
            itemCount.BackColor = Theme.BG_INPUT;
            itemCount.ForeColor = Theme.TEXT_PRIMARY;
            itemCount.Minimum = 1;
            itemCount.Maximum = 999;
            itemCount.Value = 1;
            gic.Controls.Add(itemCount);
            Host.Divider(gic, Theme.ROW_H * 4);

            int giveHalfW = (Theme.CARD_W - Theme.PAD * 3) / 2;
            Button gvBtn = Host.ActionBtn(gic, Theme.PAD, Theme.ROW_H * 4 + 7, "Give", giveHalfW);
            gvBtn.BackColor = Color.FromArgb(20, 70, 20);
            gvBtn.ForeColor = Color.FromArgb(100, 220, 100);
            gvBtn.Click += delegate { GiveItem(false); };
            Button rmBtn = Host.ActionBtn(gic, Theme.PAD + giveHalfW + Theme.PAD, Theme.ROW_H * 4 + 7, "Remove", giveHalfW);
            rmBtn.BackColor = Color.FromArgb(70, 20, 20);
            rmBtn.ForeColor = Color.FromArgb(255, 120, 120);
            rmBtn.Click += delegate { GiveItem(true); };
            y += Theme.ROW_H * 5 + Theme.PAD;

            // SERVER CONSOLE (most of these need the host to have enable_cheats)
            y = Host.SectionHeader(c, y, "SERVER CONSOLE (cheats required)");
            Panel ccc = Host.MakeCard(c, y, 2);
            int consHalfW = (Theme.CARD_W - Theme.PAD * 3) / 2;
            Button slBtn = Host.ActionBtn(ccc, Theme.PAD, 7, "Set Level 99", consHalfW);
            slBtn.BackColor = Color.FromArgb(20, 50, 60);
            slBtn.ForeColor = Color.FromArgb(80, 180, 220);
            slBtn.Click += delegate { Host.SendCommand("CCMD:team_set_level:1:99"); };
            Button atpBtn = Host.ActionBtn(ccc, Theme.PAD + consHalfW + Theme.PAD, 7, "Activate Teleporter", consHalfW);
            atpBtn.BackColor = Color.FromArgb(20, 50, 60);
            atpBtn.ForeColor = Color.FromArgb(80, 180, 220);
            atpBtn.Click += delegate { Host.SendCommand("CCMD:activate_teleporter"); };
            Host.Divider(ccc, Theme.ROW_H);
            Button ecBtn = Host.ActionBtn(ccc, Theme.PAD, Theme.ROW_H + 7, "Try Enable Cheats", Theme.CARD_W - Theme.PAD * 2);
            ecBtn.BackColor = Color.FromArgb(60, 30, 10);
            ecBtn.ForeColor = Theme.ACCENT_AMBER;
            ecBtn.Click += delegate { Host.SendCommand("ENABLE_CHEATS"); };
            y += Theme.ROW_H * 2 + Theme.PAD;

            Host.AddSpacer(c, y, 20);
        }

        private void FilterItems()
        {
            if (itemCombo == null) return;
            string filter = itemSearch.Text.ToLower().Trim();
            itemCombo.Items.Clear();
            foreach (string[] item in items)
            {
                string display = item[2];
                string internal_ = item[1];
                if (filter.Length == 0 || display.ToLower().Contains(filter) || internal_.ToLower().Contains(filter))
                    itemCombo.Items.Add(display + " | " + internal_);
            }
            if (itemCombo.Items.Count > 0)
                itemCombo.SelectedIndex = 0;
        }

        private void GiveItem(bool remove)
        {
            if (itemCombo.SelectedItem == null) return;
            string sel = itemCombo.SelectedItem.ToString();
            string[] parts = sel.Split('|');
            if (parts.Length < 2) return;
            string internalName = parts[1].Trim();
            int count = (int)itemCount.Value;
            // Item 0 is "All Players" (core wants -1); real players follow in players.txt order (0-based).
            int targetSel = targetCombo.SelectedIndex;
            int playerIdx = (targetSel <= 0) ? -1 : targetSel - 1;
            string sign = remove ? "-" : "+";
            Host.SendCommand("GIVE_ITEM:" + playerIdx + ":" + internalName + ":" + sign + count);
        }

        public override void ParseStatus(string key, string value)
        {
            float f;
            int i;
            if (key == "ItemMultiplierVal" && itemMultInput != null)
            {
                if (int.TryParse(value, out i)) Host.SyncInput(itemMultInput, i.ToString());
            }
            else if (key == "IemDamage" && iemDmg != null)
            {
                if (int.TryParse(value, out i)) Host.SyncInput(iemDmg, i.ToString());
            }
            else if (key == "IemAtkSpd" && iemAtkSpd != null)
            {
                if (int.TryParse(value, out i)) Host.SyncInput(iemAtkSpd, i.ToString());
            }
            else if (key == "IemCrit" && iemCrit != null)
            {
                if (int.TryParse(value, out i)) Host.SyncInput(iemCrit, i.ToString());
            }
            else if (key == "IemMoveSpd" && iemMoveSpd != null)
            {
                if (int.TryParse(value, out i)) Host.SyncInput(iemMoveSpd, i.ToString());
            }
            else if (key == "IemSurvival" && iemSurv != null)
            {
                if (int.TryParse(value, out i)) Host.SyncInput(iemSurv, i.ToString());
            }
            else if (key == "IemOther" && iemOther != null)
            {
                if (int.TryParse(value, out i)) Host.SyncInput(iemOther, i.ToString());
            }
            else if (key == "VacuumRange" && vacuumRange != null)
            {
                if (float.TryParse(value, out f)) Host.SyncInput(vacuumRange, f.ToString("F0"));
            }
            else if (key == "ItemVacuumRange" && itemVacRange != null)
            {
                if (float.TryParse(value, out f)) Host.SyncInput(itemVacRange, f.ToString("F0"));
            }
            else if (key == "EspCats" && espChecks != null)
            {
                espLoading = true;
                HashSet<string> enabled = new HashSet<string>();
                if (!string.IsNullOrEmpty(value))
                {
                    foreach (string c in value.Split(','))
                        enabled.Add(c.Trim());
                }
                for (int j = 0; j < espChecks.Length; j++)
                    espChecks[j].Checked = enabled.Contains(espKeys[j]);
                espLoading = false;
            }
            else if (key == "RespawnInstant" && respawnMode != null)
            {
                respawnMode.SelectedIndex = value == "1" ? 0 : 1;
            }
            else if (value == "0" || value == "1")
            {
                Host.SyncToggle(key, value == "1");
            }
        }

        public override void ExecuteHotkey(string action)
        {
            if (action == "KillAll") Host.SendCommand("KILL_ALL");
            else if (action == "VacuumItems") Host.SendCommand("VACUUM_ITEMS");
            else if (action == "LootNearby") Host.SendCommand("LOOT_NEARBY:" + ((lootRange != null && lootRange.Text.Trim().Length > 0) ? lootRange.Text.Trim() : "80"));
            else Host.SendCommand("TOGGLE:" + action);
        }

        public override void OnTick()
        {
            // Roster is DYNAMIC (players join/leave, swap survivors) and players.txt only fills
            // once you're in a run — so this MUST run every tick. It used to sit AFTER the
            // catalog early-return below; the catalog loads once early, then OnTick bailed and
            // the target list never refreshed = "doesn't show all players".
            RefreshTargetList();

            if (catalogLoaded) return;

            string catalogPath = Path.Combine(IpcDirectory, "item_catalog.txt");
            if (File.Exists(catalogPath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(catalogPath);
                    items.Clear();
                    foreach (string line in lines)
                    {
                        string[] parts = line.Split('|');
                        if (parts.Length >= 3)
                            items.Add(parts);
                    }
                    if (items.Count > 0)
                    {
                        catalogLoaded = true;
                        FilterItems();
                    }
                }
                catch { }
            }
        }

        // Rebuild the target dropdowns (Give-Items AND Hex give/remove) from players.txt:
        // "All Players" + each player (the "idx|" prefix stripped for display), only when the
        // roster actually changed, preserving each combo's selection so a poll can't yank it back.
        private void RefreshTargetList()
        {
            if (targetCombo == null && hexTargetCombo == null) return;
            try
            {
                List<string> desired = new List<string>();
                desired.Add("All Players");
                string playersPath = Path.Combine(IpcDirectory, "players.txt");
                if (File.Exists(playersPath))
                {
                    foreach (string pl in File.ReadAllLines(playersPath))
                    {
                        string line = pl.Trim();
                        if (line.Length == 0) continue;
                        int bar = line.IndexOf('|');
                        desired.Add(bar >= 0 ? line.Substring(bar + 1) : line);   // strip "idx|"
                    }
                }
                SyncCombo(targetCombo, desired);
                SyncCombo(hexTargetCombo, desired);
            }
            catch { }
        }

        // Point a combo at `desired` only if it differs, keeping the current pick when still present
        // (else defaulting to the first real player, or "All Players" when nobody is listed yet).
        private static void SyncCombo(ComboBox combo, List<string> desired)
        {
            if (combo == null) return;
            bool changed = desired.Count != combo.Items.Count;
            if (!changed)
                for (int i = 0; i < desired.Count; i++)
                    if (!string.Equals(combo.Items[i] as string, desired[i])) { changed = true; break; }
            if (!changed) return;

            string prev = combo.SelectedItem as string;
            combo.BeginUpdate();
            combo.Items.Clear();
            foreach (string d in desired) combo.Items.Add(d);
            int restore = (prev != null) ? combo.Items.IndexOf(prev) : -1;
            combo.SelectedIndex = (restore >= 0) ? restore : (combo.Items.Count > 1 ? 1 : 0);
            combo.EndUpdate();
        }

        // Map the hex target combo's selection to the core's playerIdx (index 0 = "All" = -1).
        private int HexTargetIdx()
        {
            if (hexTargetCombo == null) return -1;
            int s = hexTargetCombo.SelectedIndex;
            return (s <= 0) ? -1 : s - 1;
        }

        public override List<string> SaveExtraState()
        {
            List<string> lines = new List<string>();
            if (itemMultInput != null) lines.Add("Val_ItemMult=" + itemMultInput.Text);
            if (vacuumRange != null) lines.Add("Val_Vacuum=" + vacuumRange.Text);
            if (itemVacRange != null) lines.Add("Val_ItemVac=" + itemVacRange.Text);
            if (espChecks != null)
            {
                for (int j = 0; j < espChecks.Length; j++)
                    lines.Add("EspCat_" + espKeys[j] + "=" + (espChecks[j].Checked ? "1" : "0"));
            }
            return lines;
        }

        public override void LoadExtraState(Dictionary<string, string> data)
        {
            string v;
            if (data.TryGetValue("Val_ItemMult", out v) && itemMultInput != null) itemMultInput.Text = v;
            if (data.TryGetValue("Val_Vacuum", out v) && vacuumRange != null) vacuumRange.Text = v;
            if (data.TryGetValue("Val_ItemVac", out v) && itemVacRange != null) itemVacRange.Text = v;
            if (espChecks != null)
            {
                espLoading = true;
                for (int j = 0; j < espChecks.Length; j++)
                {
                    if (data.TryGetValue("EspCat_" + espKeys[j], out v))
                        espChecks[j].Checked = v == "1";
                    else
                        espChecks[j].Checked = true;
                }
                espLoading = false;
            }
        }

        public override void OnConnected()
        {
            foreach (var kvp in Host.FeatureStates)
            {
                Host.SendCommand("FEATURE:" + kvp.Key + ":" + (kvp.Value ? "1" : "0"));
            }
        }
    }
}
