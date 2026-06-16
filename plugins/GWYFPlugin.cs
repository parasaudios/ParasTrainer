using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ParasTrainer
{
    public class GWYFPlugin : GamePlugin
    {
        public override string GameName { get { return "Gamble With Your Friends"; } }
        public override string ShortName { get { return "GWYF"; } }
        public override string SteamId { get { return "3892270"; } }
        public override string ProcessName { get { return "Gamble With Your Friends"; } }
        public override Color AccentColor { get { return Color.FromArgb(74, 222, 128); } }
        public override Color AccentDimColor { get { return Color.FromArgb(30, 80, 50); } }
        public override string IconPath { get { return @"C:\Users\Para\GWYFTrainer\app.ico"; } }
        public override string IpcDirectory { get { return Path.Combine(Path.GetTempPath(), "gwyf_trainer"); } }
        public override string PluginVersion { get { return "2"; } }

        private TextBox multInput, moneyInput, ticketInput;
        private TextBox addMoneyNH, addTicketNH;

        public override void InitDefaultHotkeys(Dictionary<string, Keys> b)
        {
            b["FreePurchases"] = Keys.F2; b["FreeBets"] = Keys.F3; b["FreeTickets"] = Keys.F4;
            b["InfiniteBalance"] = Keys.F5; b["InfiniteTickets"] = Keys.F6;
            b["BoostedPayouts"] = Keys.F7; b["NoBetLimits"] = Keys.F8;
            b["FreeRerolls"] = Keys.F9; b["FreezeTime"] = Keys.F10;
            b["SetMoney"] = Keys.F11; b["SetTickets"] = Keys.F12;
            b["AlwaysWin"] = Keys.Pause; b["InfiniteJump"] = Keys.None; b["FlyMode"] = Keys.None;
        }

        public override void BuildPanel(Panel c)
        {
            int y = 10;

            // ── WORKS EVERYWHERE ──
            y = Host.ColorSectionHeader(c, y, "WORKS EVERYWHERE", AccentColor);

            // MOVEMENT
            y = Host.SectionHeader(c, y, "MOVEMENT");
            Panel mc = Host.MakeCard(c, y, 2);
            Host.ToggleRow(mc, 0, "InfiniteJump", "Infinite Jump");
            Host.Divider(mc, Theme.ROW_H);
            Host.ToggleRow(mc, Theme.ROW_H, "FlyMode", "Fly Mode (Noclip)");
            y += Theme.ROW_H * 2 + Theme.PAD;

            // OTHER
            y = Host.SectionHeader(c, y, "OTHER");
            Panel oc = Host.MakeCard(c, y, 1);
            Host.ToggleRow(oc, 0, "FreezeTime", "Freeze Time");
            y += Theme.ROW_H + Theme.PAD;

            // ADD MONEY (NON-HOST)
            y = Host.SectionHeader(c, y, "ADD MONEY (NON-HOST)");
            Panel amnhc = Host.MakeCard(c, y, 1);
            Label amnhLbl = new Label();
            amnhLbl.Text = "Amount:";
            amnhLbl.Font = new Font("Segoe UI", 9f);
            amnhLbl.ForeColor = Theme.TEXT_PRIMARY;
            amnhLbl.Location = new Point(Theme.PAD, 12);
            amnhLbl.AutoSize = true;
            amnhc.Controls.Add(amnhLbl);
            addMoneyNH = Host.MakeInput(amnhc, 90, 8, 120, "100000");
            Button amnhBtn = Host.ActionBtn(amnhc, 218, 7, "Add", 52);
            amnhBtn.Click += delegate { Host.SendCommand("ADDMONEY_NH:" + addMoneyNH.Text); };
            y += Theme.ROW_H + Theme.PAD;

            // ADD TICKETS (NON-HOST)
            y = Host.SectionHeader(c, y, "ADD TICKETS (NON-HOST)");
            Panel atnhc = Host.MakeCard(c, y, 1);
            Label atnhLbl = new Label();
            atnhLbl.Text = "Amount:";
            atnhLbl.Font = new Font("Segoe UI", 9f);
            atnhLbl.ForeColor = Theme.TEXT_PRIMARY;
            atnhLbl.Location = new Point(Theme.PAD, 12);
            atnhLbl.AutoSize = true;
            atnhc.Controls.Add(atnhLbl);
            addTicketNH = Host.MakeInput(atnhc, 90, 8, 120, "1000");
            Button atnhBtn = Host.ActionBtn(atnhc, 218, 7, "Add", 52);
            atnhBtn.Click += delegate { Host.SendCommand("ADDTICKETS_NH:" + addTicketNH.Text); };
            y += Theme.ROW_H + Theme.PAD;

            // ── HOST ONLY ──
            y = Host.ColorSectionHeader(c, y, "HOST ONLY", Theme.ACCENT_AMBER);

            // MONEY & BALANCE
            y = Host.SectionHeader(c, y, "MONEY & BALANCE");
            Panel mbc = Host.MakeCard(c, y, 4);
            Host.ToggleRow(mbc, 0, "FreePurchases", "Free Purchases");
            Host.Divider(mbc, Theme.ROW_H);
            Host.ToggleRow(mbc, Theme.ROW_H, "FreeBets", "Free Bets");
            Host.Divider(mbc, Theme.ROW_H * 2);
            Host.ToggleRow(mbc, Theme.ROW_H * 2, "InfiniteBalance", "Infinite Balance (10x)");
            Host.Divider(mbc, Theme.ROW_H * 3);
            Label monLbl = new Label();
            monLbl.Text = "Money:";
            monLbl.Font = new Font("Segoe UI", 9f);
            monLbl.ForeColor = Theme.TEXT_PRIMARY;
            monLbl.Location = new Point(Theme.PAD, Theme.ROW_H * 3 + 12);
            monLbl.AutoSize = true;
            mbc.Controls.Add(monLbl);
            moneyInput = Host.MakeInput(mbc, 90, Theme.ROW_H * 3 + 8, 120, "100000");
            Button monBtn = Host.ActionBtn(mbc, 218, Theme.ROW_H * 3 + 7, "Set", 52);
            monBtn.Click += delegate { Host.SendCommand("SETMONEY:" + moneyInput.Text); };
            Host.HotkeyBtn(mbc, Theme.CARD_W - Theme.PAD - 46, Theme.ROW_H * 3 + 9, "SetMoney");
            y += Theme.ROW_H * 4 + Theme.PAD;

            // TICKETS
            y = Host.SectionHeader(c, y, "TICKETS");
            Panel tc = Host.MakeCard(c, y, 3);
            Host.ToggleRow(tc, 0, "FreeTickets", "Free Tickets");
            Host.Divider(tc, Theme.ROW_H);
            Host.ToggleRow(tc, Theme.ROW_H, "InfiniteTickets", "Infinite Tickets (10x)");
            Host.Divider(tc, Theme.ROW_H * 2);
            Label tktLbl = new Label();
            tktLbl.Text = "Tickets:";
            tktLbl.Font = new Font("Segoe UI", 9f);
            tktLbl.ForeColor = Theme.TEXT_PRIMARY;
            tktLbl.Location = new Point(Theme.PAD, Theme.ROW_H * 2 + 12);
            tktLbl.AutoSize = true;
            tc.Controls.Add(tktLbl);
            ticketInput = Host.MakeInput(tc, 90, Theme.ROW_H * 2 + 8, 120, "1000");
            Button tktBtn = Host.ActionBtn(tc, 218, Theme.ROW_H * 2 + 7, "Set", 52);
            tktBtn.Click += delegate { Host.SendCommand("SETTICKETS:" + ticketInput.Text); };
            Host.HotkeyBtn(tc, Theme.CARD_W - Theme.PAD - 46, Theme.ROW_H * 2 + 9, "SetTickets");
            y += Theme.ROW_H * 3 + Theme.PAD;

            // GAMBLING
            y = Host.SectionHeader(c, y, "GAMBLING");
            Panel gc = Host.MakeCard(c, y, 4);
            Host.ToggleRow(gc, 0, "AlwaysWin", "Always Win");
            Host.Divider(gc, Theme.ROW_H);
            Host.ToggleRow(gc, Theme.ROW_H, "BoostedPayouts", "Boosted Payouts");
            Host.Divider(gc, Theme.ROW_H * 2);
            Label multLbl = new Label();
            multLbl.Text = "Multiplier:";
            multLbl.Font = new Font("Segoe UI", 9f);
            multLbl.ForeColor = Theme.TEXT_SECONDARY;
            multLbl.Location = new Point(Theme.PAD + 18, Theme.ROW_H * 2 + 12);
            multLbl.AutoSize = true;
            gc.Controls.Add(multLbl);
            multInput = Host.MakeInput(gc, 118, Theme.ROW_H * 2 + 8, 65, "5");
            Button multBtn = Host.ActionBtn(gc, 191, Theme.ROW_H * 2 + 7, "Apply", 52);
            multBtn.Click += delegate {
                float v;
                if (float.TryParse(multInput.Text, out v))
                    Host.SendCommand("SET:PayoutMultiplier:" + v.ToString("F1"));
            };
            Host.Divider(gc, Theme.ROW_H * 3);
            Host.ToggleRow(gc, Theme.ROW_H * 3, "NoBetLimits", "No Bet Limits");
            y += Theme.ROW_H * 4 + Theme.PAD;

            // OTHER
            y = Host.SectionHeader(c, y, "OTHER");
            Panel oc2 = Host.MakeCard(c, y, 1);
            Host.ToggleRow(oc2, 0, "FreeRerolls", "Free Rerolls");
            y += Theme.ROW_H + Theme.PAD;

            Host.AddSpacer(c, y, 20);
        }

        public override void ParseStatus(string key, string value)
        {
            float f;
            if (key == "PayoutMultiplier" && multInput != null)
            {
                if (float.TryParse(value, out f) && !Host.DirtyInputs.Contains(multInput))
                    multInput.Text = f.ToString("F0");
            }
            else if (value == "0" || value == "1")
            {
                Host.SyncToggle(key, value == "1");
            }
        }

        public override void ExecuteHotkey(string action)
        {
            if (action == "SetMoney")
            {
                if (moneyInput != null && !string.IsNullOrEmpty(moneyInput.Text))
                    Host.SendCommand("SETMONEY:" + moneyInput.Text);
            }
            else if (action == "SetTickets")
            {
                if (ticketInput != null && !string.IsNullOrEmpty(ticketInput.Text))
                    Host.SendCommand("SETTICKETS:" + ticketInput.Text);
            }
            else
            {
                Host.SendCommand("TOGGLE:" + action);
            }
        }

        public override List<string> SaveExtraState()
        {
            List<string> lines = new List<string>();
            if (multInput != null) lines.Add("Val_Multiplier=" + multInput.Text);
            if (moneyInput != null) lines.Add("Val_Money=" + moneyInput.Text);
            if (ticketInput != null) lines.Add("Val_Tickets=" + ticketInput.Text);
            return lines;
        }

        public override void LoadExtraState(Dictionary<string, string> data)
        {
            string v;
            if (data.TryGetValue("Val_Multiplier", out v) && multInput != null) multInput.Text = v;
            if (data.TryGetValue("Val_Money", out v) && moneyInput != null) moneyInput.Text = v;
            if (data.TryGetValue("Val_Tickets", out v) && ticketInput != null) ticketInput.Text = v;
        }
    }
}
