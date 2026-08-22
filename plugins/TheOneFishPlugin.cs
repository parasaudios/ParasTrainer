using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ParasTrainer
{
    public class TheOneFishPlugin : GamePlugin
    {
        public override string GameName { get { return "The One Fish"; } }
        public override string ShortName { get { return "OneFish"; } }
        public override string SteamId { get { return "4883580"; } }
        public override string ProcessName { get { return "The One Fish"; } }
        public override Color AccentColor { get { return Color.FromArgb(64, 176, 224); } }
        public override Color AccentDimColor { get { return Color.FromArgb(20, 60, 82); } }
        public override string IconPath { get { return @"C:\Users\Para\OneFishTrainer\app.ico"; } }
        public override string IpcDirectory { get { return Path.Combine(Path.GetTempPath(), "onefish_trainer"); } }
        public override string PluginVersion { get { return "2"; } }

        private TextBox currencyInput;
        private Label liveHealth, liveHunger, liveCurrency, liveHost;
        private int curHp, maxHp;

        public override void InitDefaultHotkeys(Dictionary<string, Keys> b)
        {
            b["GodMode"] = Keys.None; b["NoHunger"] = Keys.None; b["NoDrowning"] = Keys.None;
            b["NoKnockback"] = Keys.None; b["AutoRevive"] = Keys.None; b["KillEnemies"] = Keys.None;
            b["EasyFishing"] = Keys.None; b["BestFish"] = Keys.None; b["AutoHotspot"] = Keys.None;
            b["FreeShop"] = Keys.None; b["SetCurrency"] = Keys.None; b["AddCurrency"] = Keys.None;
            b["Revive"] = Keys.None; b["MaxBoons"] = Keys.None;
        }

        public override void BuildPanel(Panel c)
        {
            int y = 10;

            // ── LIVE ──
            y = Host.ColorSectionHeader(c, y, "LIVE", AccentColor);
            Panel lc = Host.MakeCard(c, y, 2);
            liveHealth = Mk(lc, Theme.PAD, 8, "Health: -");
            liveHunger = Mk(lc, 200, 8, "Hunger: -");
            liveCurrency = Mk(lc, Theme.PAD, 8 + Theme.ROW_H, "Currency: -");
            liveHost = Mk(lc, 200, 8 + Theme.ROW_H, "Host: -");
            y += Theme.ROW_H * 2 + Theme.PAD;

            // note
            Panel nc = Host.MakeCard(c, y, 1);
            Label note = new Label();
            note.Text = "Most cheats apply when you host or play solo. Fishing + No Knockback work as a guest too.";
            note.Font = new Font("Segoe UI", 8f, FontStyle.Italic);
            note.ForeColor = Theme.TEXT_SECONDARY;
            note.Location = new Point(Theme.PAD + 2, 8);
            note.MaximumSize = new Size(Theme.CARD_W - Theme.PAD, 0);
            note.AutoSize = true;
            nc.Controls.Add(note);
            y += Theme.ROW_H + Theme.PAD;

            // ── SURVIVAL ──
            y = Host.SectionHeader(c, y, "SURVIVAL");
            Panel sc = Host.MakeCard(c, y, 6);
            Host.ToggleRow(sc, 0, "GodMode", "God Mode (blocks all damage)");
            Host.Divider(sc, Theme.ROW_H);
            Host.ToggleRow(sc, Theme.ROW_H, "NoHunger", "No Hunger");
            Host.Divider(sc, Theme.ROW_H * 2);
            Host.ToggleRow(sc, Theme.ROW_H * 2, "NoDrowning", "No Drowning");
            Host.Divider(sc, Theme.ROW_H * 3);
            Host.ToggleRow(sc, Theme.ROW_H * 3, "NoKnockback", "No Knockback");
            Host.Divider(sc, Theme.ROW_H * 4);
            Host.ToggleRow(sc, Theme.ROW_H * 4, "AutoRevive", "Auto-Revive on Death");
            Host.Divider(sc, Theme.ROW_H * 5);
            Button rev = Host.ActionBtn(sc, Theme.PAD, Theme.ROW_H * 5 + 7, "Revive / Heal Now", Theme.CARD_W - Theme.PAD * 2);
            rev.BackColor = Color.FromArgb(10, 60, 40); rev.ForeColor = Theme.ACCENT_GREEN;
            rev.Click += delegate { Host.SendCommand("ACTION:Revive"); };
            y += Theme.ROW_H * 6 + Theme.PAD;

            // ── FISHING (works as guest) ──
            y = Host.SectionHeader(c, y, "FISHING");
            Panel fc = Host.MakeCard(c, y, 3);
            Host.ToggleRow(fc, 0, "EasyFishing", "Easy Fishing (never fail catch)");
            Host.Divider(fc, Theme.ROW_H);
            Host.ToggleRow(fc, Theme.ROW_H, "BestFish", "Always Best Fish (Mythical + max size)");
            Host.Divider(fc, Theme.ROW_H * 2);
            Host.ToggleRow(fc, Theme.ROW_H * 2, "AutoHotspot", "Auto Hotspot (bonus fish anywhere)");
            y += Theme.ROW_H * 3 + Theme.PAD;

            // ── ECONOMY ──
            y = Host.SectionHeader(c, y, "ECONOMY ($)");
            Panel ec = Host.MakeCard(c, y, 2);
            currencyInput = Host.ValueRow(ec, 0, "Amount:", "99999", "Set", delegate {
                int v; if (int.TryParse(currencyInput.Text, out v)) Host.SendCommand("SET:CurrencyVal:" + v);
                Host.SendCommand("ACTION:SetCurrency");
            });
            Host.Divider(ec, Theme.ROW_H);
            Button add = Host.ActionBtn(ec, Theme.PAD, Theme.ROW_H + 7, "Add Amount", Theme.CARD_W - Theme.PAD * 2);
            add.Click += delegate {
                int v; if (int.TryParse(currencyInput.Text, out v)) Host.SendCommand("SET:CurrencyVal:" + v);
                Host.SendCommand("ACTION:AddCurrency");
            };
            y += Theme.ROW_H * 2 + Theme.PAD;

            // ── SHOP ──
            y = Host.SectionHeader(c, y, "SHOP");
            Panel shc = Host.MakeCard(c, y, 1);
            Host.ToggleRow(shc, 0, "FreeShop", "Free Shop (everything costs 0)");
            y += Theme.ROW_H + Theme.PAD;

            // ── COMBAT ──
            y = Host.SectionHeader(c, y, "COMBAT");
            Panel coc = Host.MakeCard(c, y, 1);
            Host.ToggleRow(coc, 0, "KillEnemies", "Instant-Kill Enemies");
            y += Theme.ROW_H + Theme.PAD;

            // ── BOONS ──
            y = Host.SectionHeader(c, y, "BOONS");
            Panel bc = Host.MakeCard(c, y, 1);
            Button mb = Host.ActionBtn(bc, Theme.PAD, 7, "Max All Boons", Theme.CARD_W - Theme.PAD * 2);
            mb.Click += delegate { Host.SendCommand("ACTION:MaxBoons"); };
            y += Theme.ROW_H + Theme.PAD;

            Host.AddSpacer(c, y, 20);
        }

        private Label Mk(Panel card, int x, int y, string text)
        {
            Label l = new Label();
            l.Text = text; l.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            l.ForeColor = AccentColor; l.Location = new Point(x, y); l.AutoSize = true;
            card.Controls.Add(l);
            return l;
        }

        public override void ParseStatus(string key, string value)
        {
            int i;
            if (key == "CurrencyVal" && currencyInput != null) { if (int.TryParse(value, out i)) Host.SyncInput(currencyInput, i.ToString()); }
            else if (key == "CurHealth") { int.TryParse(value, out curHp); if (liveHealth != null) liveHealth.Text = "Health: " + curHp + " / " + maxHp; }
            else if (key == "MaxHealth") { int.TryParse(value, out maxHp); if (liveHealth != null) liveHealth.Text = "Health: " + curHp + " / " + maxHp; }
            else if (key == "Hunger" && liveHunger != null) { liveHunger.Text = "Hunger: " + value + "%"; }
            else if (key == "Currency" && liveCurrency != null) { liveCurrency.Text = "Currency: " + value; }
            else if (key == "IsHost" && liveHost != null)
            {
                bool host = value == "1";
                liveHost.Text = "Host: " + (host ? "yes" : "NO");
                liveHost.ForeColor = host ? AccentColor : Color.FromArgb(220, 120, 120);
            }
            else if (value == "0" || value == "1") { Host.SyncToggle(key, value == "1"); }
        }

        public override void ExecuteHotkey(string action)
        {
            if (action == "GodMode" || action == "NoHunger" || action == "NoDrowning" || action == "NoKnockback"
                || action == "AutoRevive" || action == "KillEnemies" || action == "EasyFishing"
                || action == "BestFish" || action == "AutoHotspot" || action == "FreeShop")
                Host.SendCommand("TOGGLE:" + action);
            else
                Host.SendCommand("ACTION:" + action);
        }

        public override List<string> SaveExtraState()
        {
            List<string> lines = new List<string>();
            if (currencyInput != null) lines.Add("Val_Currency=" + currencyInput.Text);
            return lines;
        }

        public override void LoadExtraState(Dictionary<string, string> data)
        {
            string v;
            if (data.TryGetValue("Val_Currency", out v) && currencyInput != null) currencyInput.Text = v;
        }
    }
}
