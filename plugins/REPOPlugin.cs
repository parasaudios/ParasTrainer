using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ParasTrainer
{
    public class REPOPlugin : GamePlugin
    {
        public override string GameName { get { return "R.E.P.O."; } }
        public override string ShortName { get { return "REPO"; } }
        public override string SteamId { get { return "3241660"; } }
        public override string ProcessName { get { return "REPO"; } }
        public override Color AccentColor { get { return Color.FromArgb(0, 210, 210); } }
        public override Color AccentDimColor { get { return Color.FromArgb(0, 100, 100); } }
        public override string IconPath { get { return @"C:\Users\Para\REPOTrainer\app.ico"; } }
        public override string IpcDirectory { get { return Path.Combine(Path.GetTempPath(), "repo_trainer"); } }
        public override string PluginVersion { get { return "5"; } }

        private TextBox spdInput, gravInput, grabInput, strInput, moneyInput, valInput, dmgInput;
        private CheckBox[] espChecks;
        private string[] espKeys = { "Enemies", "Valuables", "Players", "Extraction", "Traps" };
        private string[] espLabels = { "Enemies", "Valuables ($)", "Players", "Extraction", "Traps" };
        private bool espLoading;

        public override void InitDefaultHotkeys(Dictionary<string, Keys> b)
        {
            b["GodMode"] = Keys.F2; b["InfiniteHealth"] = Keys.None; b["InfiniteStamina"] = Keys.F3;
            b["SuperSpeed"] = Keys.F4; b["InfiniteJump"] = Keys.F5; b["NoTumble"] = Keys.F6;
            b["NoGravity"] = Keys.F7; b["ExtendedGrab"] = Keys.F8; b["SuperGrabStrength"] = Keys.None;
            b["NoOvercharge"] = Keys.None; b["InfiniteMoney"] = Keys.F9; b["FreeShop"] = Keys.None;
            b["InfiniteLives"] = Keys.None; b["FreezeEnemies"] = Keys.None; b["EasyExtract"] = Keys.None;
            b["ValuableBoost"] = Keys.None; b["DamageMultiplier"] = Keys.None;
            b["InvisibleToEnemies"] = Keys.F11; b["InfiniteItemEnergy"] = Keys.None;
            b["InfiniteAmmo"] = Keys.None; b["HealSelfLoop"] = Keys.None;
            b["EspEnabled"] = Keys.None; b["InvulnerableValuables"] = Keys.None;
            b["KillAll"] = Keys.F10; b["FreezeAll"] = Keys.None;
        }

        public override void BuildPanel(Panel c)
        {
            int y = 10;

            // ── WORKS EVERYWHERE ──
            y = Host.ColorSectionHeader(c, y, "WORKS EVERYWHERE", Theme.ACCENT_GREEN);

            // SURVIVAL
            y = Host.SectionHeader(c, y, "SURVIVAL");
            Panel sc = Host.MakeCard(c, y, 3);
            Host.ToggleRow(sc, 0, "GodMode", "God Mode");
            Host.Divider(sc, Theme.ROW_H);
            Host.ToggleRow(sc, Theme.ROW_H, "InfiniteHealth", "Infinite Health");
            Host.Divider(sc, Theme.ROW_H * 2);
            Host.ToggleRow(sc, Theme.ROW_H * 2, "NoTumble", "No Tumble / Ragdoll");
            y += Theme.ROW_H * 3 + Theme.PAD;

            // MOVEMENT
            y = Host.SectionHeader(c, y, "MOVEMENT");
            Panel mc = Host.MakeCard(c, y, 6);
            Host.ToggleRow(mc, 0, "InfiniteStamina", "Infinite Stamina");
            Host.Divider(mc, Theme.ROW_H);
            Host.ToggleRow(mc, Theme.ROW_H, "SuperSpeed", "Super Speed");
            Host.Divider(mc, Theme.ROW_H * 2);
            spdInput = Host.ValueRow(mc, Theme.ROW_H * 2, "Multiplier:", "3", "Apply", delegate {
                float v; if (float.TryParse(spdInput.Text, out v) && v > 0)
                    Host.SendCommand("SET:SpeedMultiplierVal:" + v.ToString("F1"));
            });
            Host.Divider(mc, Theme.ROW_H * 3);
            Host.ToggleRow(mc, Theme.ROW_H * 3, "InfiniteJump", "Infinite Jump");
            Host.Divider(mc, Theme.ROW_H * 4);
            Host.ToggleRow(mc, Theme.ROW_H * 4, "NoGravity", "No Gravity / Float");
            Host.Divider(mc, Theme.ROW_H * 5);
            gravInput = Host.ValueRow(mc, Theme.ROW_H * 5, "Gravity:", "0.01", "Apply", delegate {
                float v; if (float.TryParse(gravInput.Text, out v) && v >= 0)
                    Host.SendCommand("SET:GravityScaleVal:" + v.ToString("F2"));
            });
            y += Theme.ROW_H * 6 + Theme.PAD;

            // GRAB BEAM
            y = Host.SectionHeader(c, y, "GRAB BEAM");
            Panel gc = Host.MakeCard(c, y, 5);
            Host.ToggleRow(gc, 0, "ExtendedGrab", "Extended Range");
            Host.Divider(gc, Theme.ROW_H);
            grabInput = Host.ValueRow(gc, Theme.ROW_H, "Range ×:", "5", "Apply", delegate {
                float v; if (float.TryParse(grabInput.Text, out v) && v > 0)
                    Host.SendCommand("SET:GrabRangeMultiplierVal:" + v.ToString("F1"));
            });
            Host.Divider(gc, Theme.ROW_H * 2);
            Host.ToggleRow(gc, Theme.ROW_H * 2, "SuperGrabStrength", "Super Strength");
            Host.Divider(gc, Theme.ROW_H * 3);
            strInput = Host.ValueRow(gc, Theme.ROW_H * 3, "Strength ×:", "10", "Apply", delegate {
                float v; if (float.TryParse(strInput.Text, out v) && v > 0)
                    Host.SendCommand("SET:StrengthMultiplierVal:" + v.ToString("F1"));
            });
            Host.Divider(gc, Theme.ROW_H * 4);
            Host.ToggleRow(gc, Theme.ROW_H * 4, "NoOvercharge", "No Overcharge");
            y += Theme.ROW_H * 5 + Theme.PAD;

            // ITEMS
            y = Host.SectionHeader(c, y, "ITEMS");
            Panel ic = Host.MakeCard(c, y, 2);
            Host.ToggleRow(ic, 0, "InfiniteItemEnergy", "Infinite Item Energy");
            Host.Divider(ic, Theme.ROW_H);
            Host.ToggleRow(ic, Theme.ROW_H, "InfiniteAmmo", "Infinite Weapon Ammo");
            y += Theme.ROW_H * 2 + Theme.PAD;

            // STEALTH
            y = Host.SectionHeader(c, y, "STEALTH");
            Panel stc = Host.MakeCard(c, y, 1);
            Host.ToggleRow(stc, 0, "InvisibleToEnemies", "Invisible to Enemies");
            y += Theme.ROW_H + Theme.PAD;

            // NON-HOST ACTIONS
            y = Host.SectionHeader(c, y, "NON-HOST ACTIONS");
            Panel nhc = Host.MakeCard(c, y, 5);
            Host.ToggleRow(nhc, 0, "HealSelfLoop", "Auto-Heal (Non-Host)");
            Host.Divider(nhc, Theme.ROW_H);
            Button hsBtn = Host.ActionBtn(nhc, Theme.PAD, Theme.ROW_H + 7, "Heal Self (Once)", Theme.CARD_W - Theme.PAD * 2);
            hsBtn.BackColor = Color.FromArgb(10, 60, 40);
            hsBtn.ForeColor = Theme.ACCENT_GREEN;
            hsBtn.Click += delegate { Host.SendCommand("HEAL_SELF"); };
            Host.Divider(nhc, Theme.ROW_H * 2);
            Button feBtn = Host.ActionBtn(nhc, Theme.PAD, Theme.ROW_H * 2 + 7, "Force Extract", Theme.CARD_W - Theme.PAD * 2);
            feBtn.BackColor = Color.FromArgb(10, 50, 70);
            feBtn.ForeColor = Color.FromArgb(80, 200, 255);
            feBtn.Click += delegate { Host.SendCommand("FORCE_EXTRACT"); };
            Host.Divider(nhc, Theme.ROW_H * 3);
            Button dtBtn = Host.ActionBtn(nhc, Theme.PAD, Theme.ROW_H * 3 + 7, "Destroy All Traps", Theme.CARD_W - Theme.PAD * 2);
            dtBtn.BackColor = Color.FromArgb(70, 48, 6);
            dtBtn.ForeColor = Theme.ACCENT_AMBER;
            dtBtn.Click += delegate { Host.SendCommand("DESTROY_TRAPS"); };
            Host.Divider(nhc, Theme.ROW_H * 4);
            Button cosBtn = Host.ActionBtn(nhc, Theme.PAD, Theme.ROW_H * 4 + 7, "Unlock All Cosmetics", Theme.CARD_W - Theme.PAD * 2);
            cosBtn.BackColor = Color.FromArgb(60, 20, 70);
            cosBtn.ForeColor = Color.FromArgb(210, 130, 255);
            cosBtn.Click += delegate { Host.SendCommand("UNLOCK_ALL_COSMETICS"); };
            y += Theme.ROW_H * 5 + Theme.PAD;

            // ── MAP HACK ──
            y = Host.ColorSectionHeader(c, y, "MAP HACK", Theme.ACCENT_GREEN);

            int espCheckRows = 4;
            Panel espc = Host.MakeCard(c, y, espCheckRows);
            Host.ToggleRow(espc, 0, "EspEnabled", "ESP Overlay", true);
            Host.Divider(espc, Theme.ROW_H);

            espChecks = new CheckBox[5];
            for (int i = 0; i < 5; i++)
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

            // ── HOST ONLY ──
            y = Host.ColorSectionHeader(c, y, "HOST ONLY", Theme.ACCENT_AMBER);

            // ECONOMY
            y = Host.SectionHeader(c, y, "ECONOMY");
            Panel ec = Host.MakeCard(c, y, 4);
            Host.ToggleRow(ec, 0, "InfiniteMoney", "Infinite Money");
            Host.Divider(ec, Theme.ROW_H);
            moneyInput = Host.ValueRow(ec, Theme.ROW_H, "Amount:", "999999", "Apply", delegate {
                int v; if (int.TryParse(moneyInput.Text, out v) && v >= 0)
                    Host.SendCommand("SET:MoneyAmountVal:" + v);
            });
            Host.Divider(ec, Theme.ROW_H * 2);
            Host.ToggleRow(ec, Theme.ROW_H * 2, "FreeShop", "Free Shop");
            Host.Divider(ec, Theme.ROW_H * 3);
            Host.ToggleRow(ec, Theme.ROW_H * 3, "InfiniteLives", "Infinite Lives");
            y += Theme.ROW_H * 4 + Theme.PAD;

            // EXTRACTION
            y = Host.SectionHeader(c, y, "EXTRACTION");
            Panel xc = Host.MakeCard(c, y, 4);
            Host.ToggleRow(xc, 0, "EasyExtract", "Easy Extract (Goal = 1)");
            Host.Divider(xc, Theme.ROW_H);
            Host.ToggleRow(xc, Theme.ROW_H, "ValuableBoost", "Valuable Boost");
            Host.Divider(xc, Theme.ROW_H * 2);
            valInput = Host.ValueRow(xc, Theme.ROW_H * 2, "Value ×:", "10", "Apply", delegate {
                float v; if (float.TryParse(valInput.Text, out v) && v > 0)
                    Host.SendCommand("SET:ValuableMultiplierVal:" + v.ToString("F1"));
            });
            Host.Divider(xc, Theme.ROW_H * 3);
            Host.ToggleRow(xc, Theme.ROW_H * 3, "InvulnerableValuables", "Invulnerable Valuables");
            y += Theme.ROW_H * 4 + Theme.PAD;

            // ENEMIES
            y = Host.SectionHeader(c, y, "ENEMIES");
            Panel ac = Host.MakeCard(c, y, 5);
            Host.ToggleRow(ac, 0, "FreezeEnemies", "Freeze All (Continuous)");
            Host.Divider(ac, Theme.ROW_H);
            Host.ToggleRow(ac, Theme.ROW_H, "DamageMultiplier", "Damage Multiplier");
            Host.Divider(ac, Theme.ROW_H * 2);
            dmgInput = Host.ValueRow(ac, Theme.ROW_H * 2, "Damage ×:", "5", "Apply", delegate {
                float v; if (float.TryParse(dmgInput.Text, out v) && v > 0)
                    Host.SendCommand("SET:DamageMultiplierVal:" + v.ToString("F1"));
            });
            Host.Divider(ac, Theme.ROW_H * 3);
            Button killBtn = Host.ActionBtn(ac, Theme.PAD, Theme.ROW_H * 3 + 7, "Kill All Enemies", Theme.CARD_W - Theme.PAD * 2 - 54);
            killBtn.BackColor = Color.FromArgb(80, 20, 20);
            killBtn.ForeColor = Color.FromArgb(255, 120, 120);
            killBtn.Click += delegate { Host.SendCommand("KILL_ALL"); };
            Host.HotkeyBtn(ac, Theme.CARD_W - Theme.PAD - 46, Theme.ROW_H * 3 + 9, "KillAll");
            Host.Divider(ac, Theme.ROW_H * 4);
            Button frzBtn = Host.ActionBtn(ac, Theme.PAD, Theme.ROW_H * 4 + 7, "Freeze All (Once)", Theme.CARD_W - Theme.PAD * 2 - 54);
            frzBtn.BackColor = Color.FromArgb(10, 50, 70);
            frzBtn.ForeColor = Color.FromArgb(80, 200, 255);
            frzBtn.Click += delegate { Host.SendCommand("FREEZE_ALL"); };
            Host.HotkeyBtn(ac, Theme.CARD_W - Theme.PAD - 46, Theme.ROW_H * 4 + 9, "FreezeAll");
            y += Theme.ROW_H * 5 + Theme.PAD;

            Host.AddSpacer(c, y, 20);
        }

        public override void ParseStatus(string key, string value)
        {
            float f;
            int i;
            if (key == "SpeedMultiplierVal" && spdInput != null)
            {
                if (float.TryParse(value, out f) && !Host.DirtyInputs.Contains(spdInput))
                    spdInput.Text = f.ToString("F0");
            }
            else if (key == "GrabRangeMultiplierVal" && grabInput != null)
            {
                if (float.TryParse(value, out f) && !Host.DirtyInputs.Contains(grabInput))
                    grabInput.Text = f.ToString("F0");
            }
            else if (key == "GravityScaleVal" && gravInput != null)
            {
                if (float.TryParse(value, out f) && !Host.DirtyInputs.Contains(gravInput))
                    gravInput.Text = f.ToString("F2");
            }
            else if (key == "StrengthMultiplierVal" && strInput != null)
            {
                if (float.TryParse(value, out f) && !Host.DirtyInputs.Contains(strInput))
                    strInput.Text = f.ToString("F0");
            }
            else if (key == "MoneyAmountVal" && moneyInput != null)
            {
                if (int.TryParse(value, out i) && !Host.DirtyInputs.Contains(moneyInput))
                    moneyInput.Text = i.ToString();
            }
            else if (key == "ValuableMultiplierVal" && valInput != null)
            {
                if (float.TryParse(value, out f) && !Host.DirtyInputs.Contains(valInput))
                    valInput.Text = f.ToString("F0");
            }
            else if (key == "DamageMultiplierVal" && dmgInput != null)
            {
                if (float.TryParse(value, out f) && !Host.DirtyInputs.Contains(dmgInput))
                    dmgInput.Text = f.ToString("F0");
            }
            else if (key == "EspCats" && espChecks != null)
            {
                espLoading = true;
                HashSet<string> enabled = new HashSet<string>();
                if (!string.IsNullOrEmpty(value))
                {
                    foreach (string s in value.Split(','))
                        enabled.Add(s.Trim());
                }
                for (int j = 0; j < espChecks.Length; j++)
                    espChecks[j].Checked = enabled.Contains(espKeys[j]);
                espLoading = false;
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
            else if (action == "FreezeAll")
            {
                Host.SendCommand("FREEZE_ALL");
            }
            else
            {
                Host.SendCommand("TOGGLE:" + action);
            }
        }

        public override List<string> SaveExtraState()
        {
            List<string> lines = new List<string>();
            if (spdInput != null) lines.Add("Val_SpeedMult=" + spdInput.Text);
            if (gravInput != null) lines.Add("Val_Gravity=" + gravInput.Text);
            if (grabInput != null) lines.Add("Val_GrabRange=" + grabInput.Text);
            if (strInput != null) lines.Add("Val_Strength=" + strInput.Text);
            if (moneyInput != null) lines.Add("Val_Money=" + moneyInput.Text);
            if (valInput != null) lines.Add("Val_Valuable=" + valInput.Text);
            if (dmgInput != null) lines.Add("Val_Damage=" + dmgInput.Text);
            if (espChecks != null)
            {
                for (int j = 0; j < espKeys.Length; j++)
                    lines.Add("EspCat_" + espKeys[j] + "=" + (espChecks[j].Checked ? "1" : "0"));
            }
            return lines;
        }

        public override void LoadExtraState(Dictionary<string, string> data)
        {
            string v;
            if (data.TryGetValue("Val_SpeedMult", out v) && spdInput != null) spdInput.Text = v;
            if (data.TryGetValue("Val_Gravity", out v) && gravInput != null) gravInput.Text = v;
            if (data.TryGetValue("Val_GrabRange", out v) && grabInput != null) grabInput.Text = v;
            if (data.TryGetValue("Val_Strength", out v) && strInput != null) strInput.Text = v;
            if (data.TryGetValue("Val_Money", out v) && moneyInput != null) moneyInput.Text = v;
            if (data.TryGetValue("Val_Valuable", out v) && valInput != null) valInput.Text = v;
            if (data.TryGetValue("Val_Damage", out v) && dmgInput != null) dmgInput.Text = v;
            if (espChecks != null)
            {
                for (int j = 0; j < espKeys.Length; j++)
                {
                    if (data.TryGetValue("EspCat_" + espKeys[j], out v) && espChecks[j] != null)
                        espChecks[j].Checked = v == "1";
                }
            }
        }
    }
}
