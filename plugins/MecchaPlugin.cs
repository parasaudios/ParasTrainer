using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ParasTrainer
{
    public class MecchaPlugin : GamePlugin
    {
        public override string GameName { get { return "MECCHA CHAMELEON"; } }
        public override string ShortName { get { return "Meccha"; } }
        public override string SteamId { get { return "4704690"; } }
        public override string ProcessName { get { return "PenguinHotel-Win64-Shipping"; } }
        public override Color AccentColor { get { return Color.FromArgb(80, 200, 120); } }
        public override Color AccentDimColor { get { return Color.FromArgb(30, 80, 45); } }
        public override string IconPath { get { return @"C:\Users\Para\MecchaTrainer\app.ico"; } }
        public override string IpcDirectory { get { return Path.Combine(Path.GetTempPath(), "meccha_trainer"); } }
        public override string PluginVersion { get { return "3"; } }

        static readonly string UE4SS_MODS = @"C:\Program Files (x86)\Steam\steamapps\common\MECCHA CHAMELEON\Chameleon\Binaries\Win64\ue4ss\Mods";

        Label statusLabel;
        Label modStatusLabel;
        Label ghostModeLabel;
        Label ghostColorLabel;

        public override void InitDefaultHotkeys(Dictionary<string, Keys> b)
        {
            b["AutoPaint"] = Keys.F7;
            b["GhostMode"] = Keys.Insert;
        }

        public override void BuildPanel(Panel c)
        {
            int y = 10;

            // ── UE4SS MOD STATUS ──
            y = Host.ColorSectionHeader(c, y, "UE4SS MOD STATUS", AccentColor);

            Panel sc = Host.MakeCard(c, y, 2);

            Label engLbl = new Label();
            engLbl.Text = "Engine: Unreal Engine 5.6.1 (UE4SS)";
            engLbl.Font = new Font("Segoe UI", 9f);
            engLbl.ForeColor = Theme.TEXT_SECONDARY;
            engLbl.Location = new Point(Theme.PAD, 12);
            engLbl.AutoSize = true;
            sc.Controls.Add(engLbl);

            modStatusLabel = new Label();
            modStatusLabel.Font = new Font("Segoe UI", 9f);
            modStatusLabel.Location = new Point(Theme.PAD, Theme.ROW_H + 12);
            modStatusLabel.AutoSize = true;
            sc.Controls.Add(modStatusLabel);
            UpdateModStatus();

            y += Theme.ROW_H * 2 + Theme.PAD;

            // ── AUTO-PAINT ──
            y = Host.ColorSectionHeader(c, y, "AUTO-PAINT CAMOUFLAGE", AccentColor);

            y = Host.SectionHeader(c, y, "CONTROLS");
            Panel ac = Host.MakeCard(c, y, 1);
            Host.ToggleRow(ac, 0, "AutoPaint", "Auto-Paint (Blend In)");
            y += Theme.ROW_H + Theme.PAD;

            // ── GHOST MODE ──
            y = Host.ColorSectionHeader(c, y, "GHOST MODE", AccentColor);

            y = Host.SectionHeader(c, y, "CONTROLS");
            Panel gc = Host.MakeCard(c, y, 1);
            Host.ToggleRow(gc, 0, "GhostMode", "Ghost Mode (Cycle)");
            y += Theme.ROW_H + Theme.PAD;

            ghostModeLabel = new Label();
            ghostModeLabel.Font = new Font("Segoe UI", 8.5f);
            ghostModeLabel.ForeColor = Theme.TEXT_SECONDARY;
            ghostModeLabel.Location = new Point(Theme.PAD, y);
            ghostModeLabel.AutoSize = true;
            ghostModeLabel.Text = "Mode: NORMAL";
            c.Controls.Add(ghostModeLabel);
            y += 20;

            ghostColorLabel = new Label();
            ghostColorLabel.Font = new Font("Segoe UI", 8f);
            ghostColorLabel.ForeColor = Theme.TEXT_SECONDARY;
            ghostColorLabel.Location = new Point(Theme.PAD, y);
            ghostColorLabel.AutoSize = true;
            ghostColorLabel.Text = "";
            c.Controls.Add(ghostColorLabel);
            y += 20;

            // ── KEYBIND REFERENCE ──
            y = Host.SectionHeader(c, y, "IN-GAME KEYBINDS (UE4SS)");
            Panel kc = Host.MakeCard(c, y, 5);

            KeybindInfoRow(kc, 0, "F6", "Run Discovery Dump");
            Host.Divider(kc, Theme.ROW_H);
            KeybindInfoRow(kc, Theme.ROW_H, "F7", "Toggle Auto-Paint");
            Host.Divider(kc, Theme.ROW_H * 2);
            KeybindInfoRow(kc, Theme.ROW_H * 2, "F8", "Dump Player Properties");
            Host.Divider(kc, Theme.ROW_H * 3);
            KeybindInfoRow(kc, Theme.ROW_H * 3, "INS", "Cycle Ghost Mode");
            Host.Divider(kc, Theme.ROW_H * 4);
            KeybindInfoRow(kc, Theme.ROW_H * 4, "HOME", "Ghost Discovery Dump");
            y += Theme.ROW_H * 5 + Theme.PAD;

            // ── ACTIONS ──
            y = Host.SectionHeader(c, y, "ACTIONS");
            Panel actc = Host.MakeCard(c, y, 2);

            Button openModBtn = Host.ActionBtn(actc, Theme.PAD, 7, "Open Mod Folder", 130);
            openModBtn.Click += delegate {
                try
                {
                    string modPath = Path.Combine(UE4SS_MODS, "AutoPaint", "Scripts");
                    if (Directory.Exists(modPath))
                        System.Diagnostics.Process.Start("explorer.exe", modPath);
                    else
                        System.Diagnostics.Process.Start("explorer.exe", UE4SS_MODS);
                }
                catch { }
            };

            Button openLogBtn = Host.ActionBtn(actc, Theme.PAD + 140, 7, "Open UE4SS Log", 130);
            openLogBtn.Click += delegate {
                try
                {
                    string logPath = Path.Combine(Path.GetDirectoryName(UE4SS_MODS), "UE4SS.log");
                    if (File.Exists(logPath))
                        System.Diagnostics.Process.Start("notepad.exe", logPath);
                }
                catch { }
            };

            Host.Divider(actc, Theme.ROW_H);

            Button reinstallBtn = Host.ActionBtn(actc, Theme.PAD, Theme.ROW_H + 7, "Reinstall Mod", 130);
            reinstallBtn.Click += delegate { ReinstallMod(); };

            statusLabel = new Label();
            statusLabel.Text = "";
            statusLabel.Font = new Font("Segoe UI", 8f);
            statusLabel.ForeColor = AccentColor;
            statusLabel.Location = new Point(Theme.PAD + 140, Theme.ROW_H + 12);
            statusLabel.AutoSize = true;
            actc.Controls.Add(statusLabel);

            y += Theme.ROW_H * 2 + Theme.PAD;

            // ── INFO ──
            y = Host.SectionHeader(c, y, "NOTES");
            Panel nc = Host.MakeCard(c, y, 3);
            InfoRow(nc, 0, "This game uses UE4SS (not BepInEx).");
            Host.Divider(nc, Theme.ROW_H);
            InfoRow(nc, Theme.ROW_H, "CAMOUFLAGE paints your model to match surroundings.");
            Host.Divider(nc, Theme.ROW_H * 2);
            InfoRow(nc, Theme.ROW_H * 2, "Press HOME in-game for discovery, paste output to Claude.");
            y += Theme.ROW_H * 3 + Theme.PAD;

            Host.AddSpacer(c, y, 20);
        }

        void KeybindInfoRow(Panel card, int rowY, string key, string desc)
        {
            Label kLbl = new Label();
            kLbl.Text = key;
            kLbl.Font = new Font("Consolas", 10f, FontStyle.Bold);
            kLbl.ForeColor = AccentColor;
            kLbl.Location = new Point(Theme.PAD, rowY + 11);
            kLbl.AutoSize = true;
            card.Controls.Add(kLbl);

            Label dLbl = new Label();
            dLbl.Text = desc;
            dLbl.Font = new Font("Segoe UI", 9.5f);
            dLbl.ForeColor = Theme.TEXT_PRIMARY;
            dLbl.Location = new Point(Theme.PAD + 45, rowY + 10);
            dLbl.AutoSize = true;
            card.Controls.Add(dLbl);
        }

        void InfoRow(Panel card, int rowY, string text)
        {
            Label lbl = new Label();
            lbl.Text = text;
            lbl.Font = new Font("Segoe UI", 8.5f);
            lbl.ForeColor = Theme.TEXT_SECONDARY;
            lbl.Location = new Point(Theme.PAD, rowY + 12);
            lbl.AutoSize = true;
            card.Controls.Add(lbl);
        }

        void UpdateModStatus()
        {
            if (modStatusLabel == null) return;
            string apMain = Path.Combine(UE4SS_MODS, "AutoPaint", "Scripts", "main.lua");
            string apEnabled = Path.Combine(UE4SS_MODS, "AutoPaint", "enabled.txt");
            string gmMain = Path.Combine(UE4SS_MODS, "GhostMode", "Scripts", "main.lua");
            string gmEnabled = Path.Combine(UE4SS_MODS, "GhostMode", "enabled.txt");

            string apStatus = File.Exists(apMain) && File.Exists(apEnabled) ? "OK" : File.Exists(apMain) ? "off" : "X";
            string gmStatus = File.Exists(gmMain) && File.Exists(gmEnabled) ? "OK" : File.Exists(gmMain) ? "off" : "X";

            if (apStatus == "OK" && gmStatus == "OK")
            {
                modStatusLabel.Text = "AutoPaint + GhostMode: Installed";
                modStatusLabel.ForeColor = Theme.ACCENT_GREEN;
            }
            else
            {
                modStatusLabel.Text = "AutoPaint: " + apStatus + "  GhostMode: " + gmStatus;
                modStatusLabel.ForeColor = Theme.ACCENT_AMBER;
            }
        }

        void ReinstallMod()
        {
            try
            {
                string src = @"C:\Users\Para\MecchaTrainer\Mods\AutoPaint";
                string dst = Path.Combine(UE4SS_MODS, "AutoPaint");
                if (!Directory.Exists(src))
                {
                    if (statusLabel != null) { statusLabel.Text = "Source not found"; statusLabel.ForeColor = Theme.STATUS_RED; }
                    return;
                }
                if (Directory.Exists(dst)) Directory.Delete(dst, true);
                CopyDir(src, dst);
                UpdateModStatus();
                if (statusLabel != null) { statusLabel.Text = "Reinstalled!"; statusLabel.ForeColor = Theme.ACCENT_GREEN; }
            }
            catch (Exception ex)
            {
                if (statusLabel != null) { statusLabel.Text = "Error: " + ex.Message; statusLabel.ForeColor = Theme.STATUS_RED; }
            }
        }

        static void CopyDir(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (string f in Directory.GetFiles(src))
                File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
            foreach (string d in Directory.GetDirectories(src))
                CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
        }

        public override void ParseStatus(string key, string value)
        {
            if (key == "ModeName" && ghostModeLabel != null)
            {
                ghostModeLabel.Text = "Mode: " + value;
                if (value == "CAMOUFLAGE")
                    ghostModeLabel.ForeColor = AccentColor;
                else if (value == "INVISIBLE")
                    ghostModeLabel.ForeColor = Theme.ACCENT_AMBER;
                else
                    ghostModeLabel.ForeColor = Theme.TEXT_SECONDARY;
            }
            else if (key == "Color" && ghostColorLabel != null)
            {
                ghostColorLabel.Text = "Paint: RGB(" + value + ")";
            }
            else if (key == "GhostMode")
            {
                Host.SyncToggle("GhostMode", value != "0");
            }
            else if (value == "0" || value == "1")
            {
                Host.SyncToggle(key, value == "1");
            }
        }

        public override void ExecuteHotkey(string action)
        {
            if (action == "GhostMode")
            {
                try
                {
                    string cmdDir = Path.Combine(IpcDirectory, "ghost_cmd");
                    if (!Directory.Exists(cmdDir)) Directory.CreateDirectory(cmdDir);
                    string cmdFile = Path.Combine(cmdDir, DateTime.Now.Ticks + ".cmd");
                    File.WriteAllText(cmdFile, "TOGGLE:GhostMode");
                }
                catch { }
            }
            else
            {
                Host.SendCommand("TOGGLE:" + action);
            }
        }

        public override void OnTick()
        {
            UpdateModStatus();
            ReadGhostStatus();
        }

        void ReadGhostStatus()
        {
            try
            {
                string ghostFile = Path.Combine(IpcDirectory, "ghost_status.txt");
                if (!File.Exists(ghostFile)) return;
                string content = File.ReadAllText(ghostFile);
                foreach (string line in content.Split('\n'))
                {
                    string t = line.Trim();
                    if (t.Length == 0) continue;
                    string[] kv = t.Split('=');
                    if (kv.Length != 2) continue;
                    ParseStatus(kv[0], kv[1]);
                }
            }
            catch { }
        }
    }
}
