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
        public override string PluginVersion { get { return "41"; } }

        private TextBox itemMultInput;
        private TextBox iemDmg, iemAtkSpd, iemCrit, iemMoveSpd, iemSurv, iemOther;
        private TextBox vacuumRange, lunarInput, goldInput;
        private ComboBox buffCombo, respawnMode, targetCombo, itemCombo;
        private TextBox itemSearch;
        private NumericUpDown itemCount;
        private CheckBox[] espChecks;
        private string[] espKeys = { "ChestSmall", "ChestLarge", "Legendary", "Equipment", "Lunar", "Void",
                                      "Multishop", "Printer", "Scrapper", "Shrine", "Newt", "Teleporter" };
        private string[] espLabels = { "Small Chest", "Large Chest", "Legendary", "Equipment", "Lunar", "Void",
                                        "Multishop", "Printer", "Scrapper", "Shrine", "Newt Altar", "Teleporter" };
        private bool catalogLoaded;
        private bool espLoading;
        private List<string[]> items = new List<string[]>();

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
            b["InfiniteLunar"] = Keys.None;
        }

        public override void BuildPanel(Panel c)
        {
            int y = 10;

            // ── WORKS EVERYWHERE ──
            y = Host.ColorSectionHeader(c, y, "WORKS EVERYWHERE", Theme.ACCENT_GREEN);

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

            // ── NON-HOST EXPLOITS ──
            y = Host.ColorSectionHeader(c, y, "NON-HOST EXPLOITS", Theme.ACCENT_GREEN);

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

            // ── HOST ONLY ──
            y = Host.ColorSectionHeader(c, y, "HOST ONLY", Theme.ACCENT_AMBER);

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

            // ITEM EFFECT MULTIPLIER
            y = Host.SectionHeader(c, y, "ITEM EFFECT MULTIPLIER");
            Panel iemc = Host.MakeCard(c, y, 8);
            Host.ToggleRow(iemc, 0, "ItemEffectMult", "Item Effect Multiplier");
            Host.Divider(iemc, Theme.ROW_H);

            string[] iemLabels = { "Damage:", "Atk Speed:", "Crit:", "Move Spd:", "Survival:", "All Other:" };
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
                if (i < 5) Host.Divider(iemc, Theme.ROW_H * (row + 1));
            }
            iemDmg = iemInputs[0]; iemAtkSpd = iemInputs[1]; iemCrit = iemInputs[2];
            iemMoveSpd = iemInputs[3]; iemSurv = iemInputs[4]; iemOther = iemInputs[5];

            Host.Divider(iemc, Theme.ROW_H * 7);
            Button setAllBtn = Host.ActionBtn(iemc, Theme.PAD, Theme.ROW_H * 7 + 7, "Set All", Theme.CARD_W - Theme.PAD * 2);
            setAllBtn.BackColor = Color.FromArgb(40, 30, 10);
            setAllBtn.ForeColor = Theme.ACCENT_AMBER;
            setAllBtn.Click += delegate {
                string[] keys = { "IemDamage", "IemAtkSpd", "IemCrit", "IemMoveSpd", "IemSurvival", "IemOther" };
                TextBox[] tbs = { iemDmg, iemAtkSpd, iemCrit, iemMoveSpd, iemSurv, iemOther };
                for (int i = 0; i < 6; i++)
                {
                    int v; if (int.TryParse(tbs[i].Text, out v))
                    {
                        v = Math.Max(1, Math.Min(100, v));
                        Host.SendCommand("SET:" + keys[i] + ":" + v);
                    }
                }
            };
            y += Theme.ROW_H * 8 + Theme.PAD;

            // ── ACTIONS (HOST ONLY) ──
            y = Host.ColorSectionHeader(c, y, "ACTIONS (HOST ONLY)", Theme.ACCENT_AMBER);
            Panel kac = Host.MakeCard(c, y, 1);
            Button kaBtn = Host.ActionBtn(kac, Theme.PAD, 7, "Kill All Enemies", Theme.CARD_W - Theme.PAD * 2 - 54);
            kaBtn.BackColor = Color.FromArgb(80, 20, 20);
            kaBtn.ForeColor = Color.FromArgb(255, 120, 120);
            kaBtn.Click += delegate { Host.SendCommand("KILL_ALL"); };
            Host.HotkeyBtn(kac, Theme.CARD_W - Theme.PAD - 46, 9, "KillAll");
            y += Theme.ROW_H + Theme.PAD;

            // ── RESPAWN & TELEPORTER ──
            y = Host.ColorSectionHeader(c, y, "RESPAWN & TELEPORTER", Color.FromArgb(100, 200, 255));

            Panel rc = Host.MakeCard(c, y, 4);
            Button revBtn = Host.ActionBtn(rc, Theme.PAD, 7, "Revive (One-Shot)", Theme.CARD_W - Theme.PAD * 2);
            revBtn.BackColor = Color.FromArgb(20, 60, 80);
            revBtn.ForeColor = Color.FromArgb(100, 200, 255);
            revBtn.Click += delegate { Host.SendCommand("RESPAWN"); };
            Host.Divider(rc, Theme.ROW_H);

            int halfW = (Theme.CARD_W - Theme.PAD * 3) / 2;
            Button rspBtn = Host.ActionBtn(rc, Theme.PAD, Theme.ROW_H + 7, "Respawn", halfW);
            rspBtn.BackColor = Color.FromArgb(20, 60, 80);
            rspBtn.ForeColor = Color.FromArgb(100, 200, 255);
            rspBtn.Click += delegate { Host.SendCommand("RESPAWN"); };
            Button ftpBtn = Host.ActionBtn(rc, Theme.PAD + halfW + Theme.PAD, Theme.ROW_H + 7, "Force Teleporter", halfW);
            ftpBtn.BackColor = Color.FromArgb(20, 60, 80);
            ftpBtn.ForeColor = Color.FromArgb(100, 200, 255);
            ftpBtn.Click += delegate { Host.SendCommand("FORCE_TP"); };
            Host.Divider(rc, Theme.ROW_H * 2);

            Label rmLbl = new Label();
            rmLbl.Text = "Mode:";
            rmLbl.Font = new Font("Segoe UI", 9f);
            rmLbl.ForeColor = Theme.TEXT_SECONDARY;
            rmLbl.Location = new Point(Theme.PAD, Theme.ROW_H * 2 + 12);
            rmLbl.AutoSize = true;
            rc.Controls.Add(rmLbl);
            respawnMode = new ComboBox();
            respawnMode.DropDownStyle = ComboBoxStyle.DropDownList;
            respawnMode.SetBounds(70, Theme.ROW_H * 2 + 8, 90, 24);
            respawnMode.BackColor = Theme.BG_INPUT;
            respawnMode.ForeColor = Theme.TEXT_PRIMARY;
            respawnMode.Items.AddRange(new object[] { "Instant", "Pod" });
            respawnMode.SelectedIndex = 0;
            respawnMode.SelectedIndexChanged += delegate {
                Host.SendCommand(respawnMode.SelectedIndex == 0 ? "RESPAWN_MODE_INSTANT" : "RESPAWN_MODE_POD");
            };
            rc.Controls.Add(respawnMode);

            Button atpBtn = Host.ActionBtn(rc, 180, Theme.ROW_H * 2 + 7, "Activate TP (Console)", Theme.CARD_W - 180 - Theme.PAD);
            atpBtn.BackColor = Color.FromArgb(20, 50, 60);
            atpBtn.ForeColor = Color.FromArgb(80, 180, 220);
            atpBtn.Click += delegate { Host.SendCommand("CCMD:activate_teleporter"); };
            Host.Divider(rc, Theme.ROW_H * 3);

            Button usBtn = Host.ActionBtn(rc, Theme.PAD, Theme.ROW_H * 3 + 7, "Unstuck (Reset & Respawn)", Theme.CARD_W - Theme.PAD * 2);
            usBtn.BackColor = Color.FromArgb(80, 60, 10);
            usBtn.ForeColor = Color.FromArgb(255, 200, 60);
            usBtn.Click += delegate { Host.SendCommand("UNSTUCK"); };
            y += Theme.ROW_H * 4 + Theme.PAD;

            // ── SERVER CONSOLE (cheats required) ──
            y = Host.ColorSectionHeader(c, y, "SERVER CONSOLE (cheats required)", Color.FromArgb(80, 180, 220));

            Panel ccc = Host.MakeCard(c, y, 4);
            Button ecBtn = Host.ActionBtn(ccc, Theme.PAD, 7, "Try Enable Cheats", Theme.CARD_W - Theme.PAD * 2);
            ecBtn.BackColor = Color.FromArgb(60, 30, 10);
            ecBtn.ForeColor = Theme.ACCENT_AMBER;
            ecBtn.Click += delegate { Host.SendCommand("ENABLE_CHEATS"); };
            Host.Divider(ccc, Theme.ROW_H);

            Label ggLbl = new Label();
            ggLbl.Text = "Give Gold:";
            ggLbl.Font = new Font("Segoe UI", 9f);
            ggLbl.ForeColor = Color.FromArgb(80, 180, 220);
            ggLbl.Location = new Point(Theme.PAD, Theme.ROW_H + 12);
            ggLbl.AutoSize = true;
            ccc.Controls.Add(ggLbl);
            goldInput = Host.MakeInput(ccc, 100, Theme.ROW_H + 8, 80, "10000");
            Button ggBtn = Host.ActionBtn(ccc, 188, Theme.ROW_H + 7, "Give", 52);
            ggBtn.Click += delegate { Host.SendCommand("CCMD:team_give_money:1:" + goldInput.Text); };
            Host.Divider(ccc, Theme.ROW_H * 2);

            int consHalfW = (Theme.CARD_W - Theme.PAD * 3) / 2;
            Button ckBtn = Host.ActionBtn(ccc, Theme.PAD, Theme.ROW_H * 2 + 7, "Kill All (Console)", consHalfW);
            ckBtn.BackColor = Color.FromArgb(60, 20, 20);
            ckBtn.ForeColor = Color.FromArgb(220, 100, 100);
            ckBtn.Click += delegate { Host.SendCommand("CCMD:kill_all"); };
            Button slBtn = Host.ActionBtn(ccc, Theme.PAD + consHalfW + Theme.PAD, Theme.ROW_H * 2 + 7, "Set Level 99", consHalfW);
            slBtn.BackColor = Color.FromArgb(20, 50, 60);
            slBtn.ForeColor = Color.FromArgb(80, 180, 220);
            slBtn.Click += delegate { Host.SendCommand("CCMD:team_set_level:1:99"); };
            Host.Divider(ccc, Theme.ROW_H * 3);

            Label giLbl = new Label();
            giLbl.Text = "Give Item:";
            giLbl.Font = new Font("Segoe UI", 9f);
            giLbl.ForeColor = Color.FromArgb(80, 180, 220);
            giLbl.Location = new Point(Theme.PAD, Theme.ROW_H * 3 + 12);
            giLbl.AutoSize = true;
            ccc.Controls.Add(giLbl);
            Button giBtn = Host.ActionBtn(ccc, 100, Theme.ROW_H * 3 + 7, "Give x1", 80);
            giBtn.BackColor = Color.FromArgb(20, 50, 60);
            giBtn.ForeColor = Color.FromArgb(80, 180, 220);
            giBtn.Click += delegate {
                if (itemCombo != null && itemCombo.SelectedItem != null)
                {
                    string[] parts = itemCombo.SelectedItem.ToString().Split('|');
                    if (parts.Length >= 2)
                        Host.SendCommand("CCMD:give_item:" + parts[1].Trim() + ":1");
                }
            };
            y += Theme.ROW_H * 4 + Theme.PAD;

            // ── GIVE ITEMS (HOST ONLY) ──
            y = Host.ColorSectionHeader(c, y, "GIVE ITEMS (HOST ONLY)", Theme.ACCENT_AMBER);

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
            targetCombo.Items.AddRange(new object[] { "Self (Player 1)", "All Players" });
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

            // ── MAP HACK ──
            y = Host.ColorSectionHeader(c, y, "MAP HACK", Theme.ACCENT_GREEN);

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

            // ── MONEY VACUUM (HOST ONLY) ──
            y = Host.ColorSectionHeader(c, y, "MONEY VACUUM (HOST ONLY)", Theme.ACCENT_AMBER);

            Panel mvc = Host.MakeCard(c, y, 2);
            Host.ToggleRow(mvc, 0, "MoneyVacuum", "Money Vacuum");
            Host.Divider(mvc, Theme.ROW_H);
            vacuumRange = Host.ValueRow(mvc, Theme.ROW_H, "Range:", "50", "Apply", delegate {
                int v; if (int.TryParse(vacuumRange.Text, out v))
                {
                    v = Math.Max(10, Math.Min(200, v));
                    Host.SendCommand("SET:VacuumRange:" + v);
                }
            });
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
            int playerIdx = targetCombo.SelectedIndex;
            string sign = remove ? "-" : "+";
            Host.SendCommand("GIVE_ITEM:" + playerIdx + ":" + internalName + ":" + sign + count);
        }

        public override void ParseStatus(string key, string value)
        {
            float f;
            int i;
            if (key == "ItemMultiplierVal" && itemMultInput != null)
            {
                if (int.TryParse(value, out i) && !Host.DirtyInputs.Contains(itemMultInput))
                    itemMultInput.Text = i.ToString();
            }
            else if (key == "IemDamage" && iemDmg != null)
            {
                if (int.TryParse(value, out i)) iemDmg.Text = i.ToString();
            }
            else if (key == "IemAtkSpd" && iemAtkSpd != null)
            {
                if (int.TryParse(value, out i)) iemAtkSpd.Text = i.ToString();
            }
            else if (key == "IemCrit" && iemCrit != null)
            {
                if (int.TryParse(value, out i)) iemCrit.Text = i.ToString();
            }
            else if (key == "IemMoveSpd" && iemMoveSpd != null)
            {
                if (int.TryParse(value, out i)) iemMoveSpd.Text = i.ToString();
            }
            else if (key == "IemSurvival" && iemSurv != null)
            {
                if (int.TryParse(value, out i)) iemSurv.Text = i.ToString();
            }
            else if (key == "IemOther" && iemOther != null)
            {
                if (int.TryParse(value, out i)) iemOther.Text = i.ToString();
            }
            else if (key == "VacuumRange" && vacuumRange != null)
            {
                if (float.TryParse(value, out f) && !Host.DirtyInputs.Contains(vacuumRange))
                    vacuumRange.Text = f.ToString("F0");
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
            if (action == "KillAll")
            {
                Host.SendCommand("KILL_ALL");
            }
            else
            {
                Host.SendCommand("TOGGLE:" + action);
            }
        }

        public override void OnTick()
        {
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

            string playersPath = Path.Combine(IpcDirectory, "players.txt");
            if (File.Exists(playersPath) && targetCombo != null)
            {
                try
                {
                    string[] playerLines = File.ReadAllLines(playersPath);
                    if (playerLines.Length > 0)
                    {
                        targetCombo.Items.Clear();
                        foreach (string pl in playerLines)
                        {
                            if (pl.Trim().Length > 0)
                                targetCombo.Items.Add(pl.Trim());
                        }
                        if (targetCombo.Items.Count > 0)
                            targetCombo.SelectedIndex = 0;
                    }
                }
                catch { }
            }
        }

        public override List<string> SaveExtraState()
        {
            List<string> lines = new List<string>();
            if (itemMultInput != null) lines.Add("Val_ItemMult=" + itemMultInput.Text);
            if (vacuumRange != null) lines.Add("Val_Vacuum=" + vacuumRange.Text);
            if (respawnMode != null) lines.Add("RespawnMode=" + (respawnMode.SelectedIndex == 0 ? "Instant" : "Pod"));
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
            if (data.TryGetValue("RespawnMode", out v) && respawnMode != null)
                respawnMode.SelectedIndex = v == "Pod" ? 1 : 0;
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
