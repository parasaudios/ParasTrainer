using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ParasTrainer
{
    // Don't Starve Together is a Klei Lua game — there is no managed runtime to
    // inject into (unlike the BepInEx/memory-editing targets), so the cheats ship
    // as a native client-side Lua mod ("Para's Trainer") that fires the game's own
    // admin console commands from in-game hotkeys. This panel is therefore a
    // reference / manager for that mod, not a live IPC-driven module: it shows
    // status, lists the hotkeys, and gives quick actions. It deliberately
    // registers NO global hotkeys so it never fights the in-game F1-F7 bindings.
    public class DSTPlugin : GamePlugin
    {
        public override string GameName { get { return "Don't Starve Together"; } }
        public override string ShortName { get { return "DST"; } }
        public override string SteamId { get { return "322330"; } }
        public override string ProcessName { get { return "dontstarve_steam_x64"; } }
        public override Color AccentColor { get { return Color.FromArgb(206, 170, 108); } }   // aged parchment gold
        public override Color AccentDimColor { get { return Color.FromArgb(58, 46, 28); } }
        public override string IconPath { get { return @"C:\Users\Para\ParasTrainer\dst.ico"; } }
        public override string IpcDirectory { get { return Path.Combine(Path.GetTempPath(), "dst_trainer"); } }
        public override string PluginVersion { get { return "1"; } }

        // The mod lives in the game's own mods folder (DST reads it natively).
        private const string MODS_DIR = @"J:\SteamLibrary\steamapps\common\Don't Starve Together\mods\paras_trainer";
        private const string GAME_MODS_DIR = @"J:\SteamLibrary\steamapps\common\Don't Starve Together\mods";

        private static readonly string[][] HOTKEYS = new string[][] {
            new string[] { "F1", "Super God Mode — invincible + full stats + comfy temp" },
            new string[] { "F2", "God Mode — invincibility toggle (also revives ghosts)" },
            new string[] { "F3", "Free Crafting — all recipes, no ingredients" },
            new string[] { "F4", "Refill Health / Sanity / Hunger" },
            new string[] { "F5", "Dry off + comfortable temperature" },
            new string[] { "F6", "Move Speed x2 (toggle)" },
            new string[] { "F7", "Show the hotkey list in-game" },
        };

        private Label runLabel, installLabel;
        private Timer statusTimer;

        public override void InitDefaultHotkeys(Dictionary<string, Keys> b)
        {
            // None — DST hotkeys are handled inside the game by the Lua mod, not by
            // the trainer's global hotkey system. Registering any here would clash.
        }

        public override void BuildPanel(Panel c)
        {
            int y = 10;

            // ── How it works ──
            y = Host.ColorSectionHeader(c, y, "IN-GAME LUA MOD", AccentColor);
            Panel ncp = Host.MakeCard(c, y, 3);
            Label note = new Label();
            note.Text = "DST runs on Klei's Lua engine — there's nothing to inject, so cheats "
                      + "ship as a native client mod that fires the game's own admin commands from "
                      + "hotkeys. Enable it once in the in-game Mods menu; it works whenever you host "
                      + "your own world (you're the server admin). This panel is the reference + manager.";
            note.Font = new Font("Segoe UI", 8.5f);
            note.ForeColor = Theme.TEXT_SECONDARY;
            note.Location = new Point(Theme.PAD + 2, 8);
            note.MaximumSize = new Size(Theme.CARD_W - Theme.PAD * 2, 0);
            note.AutoSize = true;
            ncp.Controls.Add(note);
            y += Theme.ROW_H * 3 + Theme.PAD;

            // ── Status (self-updating) ──
            y = Host.ColorSectionHeader(c, y, "STATUS", AccentColor);
            Panel st = Host.MakeCard(c, y, 2);
            runLabel = MkStatus(st, 8, "DST running: ...");
            installLabel = MkStatus(st, 8 + Theme.ROW_H, "Mod installed: ...");
            y += Theme.ROW_H * 2 + Theme.PAD;

            // ── Hotkeys reference ──
            y = Host.ColorSectionHeader(c, y, "HOTKEYS", Theme.ACCENT_GREEN);
            Panel hk = Host.MakeCard(c, y, HOTKEYS.Length);
            for (int i = 0; i < HOTKEYS.Length; i++)
            {
                if (i > 0) Host.Divider(hk, Theme.ROW_H * i);
                KeyRow(hk, Theme.ROW_H * i, HOTKEYS[i][0], HOTKEYS[i][1]);
            }
            y += Theme.ROW_H * HOTKEYS.Length + Theme.PAD;

            // ── Actions ──
            y = Host.ColorSectionHeader(c, y, "ACTIONS", AccentColor);
            Panel ac = Host.MakeCard(c, y, 3);
            Button open = Host.ActionBtn(ac, Theme.PAD, 7, "Open Mods Folder", Theme.CARD_W - Theme.PAD * 2);
            open.Click += delegate { OpenModsFolder(); };
            Host.Divider(ac, Theme.ROW_H);
            Button howto = Host.ActionBtn(ac, Theme.PAD, Theme.ROW_H + 7, "How to Enable", Theme.CARD_W - Theme.PAD * 2);
            howto.Click += delegate { ShowEnableHelp(); };
            Host.Divider(ac, Theme.ROW_H * 2);
            Button recheck = Host.ActionBtn(ac, Theme.PAD, Theme.ROW_H * 2 + 7, "Re-check Status", Theme.CARD_W - Theme.PAD * 2);
            recheck.Click += delegate { RefreshStatus(); };
            y += Theme.ROW_H * 3 + Theme.PAD;

            Host.AddSpacer(c, y, 20);

            RefreshStatus();
            statusTimer = new Timer();
            statusTimer.Interval = 2000;
            statusTimer.Tick += delegate { RefreshStatus(); };
            statusTimer.Start();
        }

        private Label MkStatus(Panel card, int y, string text)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            l.ForeColor = Theme.TEXT_SECONDARY;
            l.Location = new Point(Theme.PAD, y + 10);
            l.AutoSize = true;
            card.Controls.Add(l);
            return l;
        }

        private void KeyRow(Panel card, int rowY, string key, string desc)
        {
            Label chip = new Label();
            chip.Text = key;
            chip.Font = new Font("Consolas", 9f, FontStyle.Bold);
            chip.TextAlign = ContentAlignment.MiddleCenter;
            chip.BackColor = Theme.HK_BG;
            chip.ForeColor = AccentColor;
            chip.SetBounds(Theme.PAD, rowY + 9, 34, 24);
            card.Controls.Add(chip);

            Label d = new Label();
            d.Text = desc;
            d.Font = new Font("Segoe UI", 9f);
            d.ForeColor = Theme.TEXT_PRIMARY;
            d.Location = new Point(Theme.PAD + 44, rowY + 12);
            d.MaximumSize = new Size(Theme.CARD_W - Theme.PAD * 2 - 44, 0);
            d.AutoSize = true;
            card.Controls.Add(d);
        }

        private void RefreshStatus()
        {
            bool running = Process.GetProcessesByName("dontstarve_steam_x64").Length > 0
                        || Process.GetProcessesByName("dontstarve_steam").Length > 0;
            bool installed = File.Exists(Path.Combine(MODS_DIR, "modmain.lua"))
                          && File.Exists(Path.Combine(MODS_DIR, "modinfo.lua"));

            if (runLabel != null)
            {
                runLabel.Text = "DST running: " + (running ? "yes" : "no");
                runLabel.ForeColor = running ? Theme.ACCENT_GREEN : Theme.TEXT_SECONDARY;
            }
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
            try { Process.Start("explorer.exe", "\"" + target + "\""); } catch { }
        }

        private void ShowEnableHelp()
        {
            MessageBox.Show(
                "Enable the mod (one time):\n\n" +
                "1. Launch Don't Starve Together.\n" +
                "2. Main menu -> Mods -> Client Mods.\n" +
                "3. Find \"Para's Trainer\" and toggle it ON, then Apply.\n" +
                "4. Host your own world (Play -> Host Game). You're the server admin,\n" +
                "   so the hotkeys work. F7 shows the list in-game.\n\n" +
                "The mod is client-side only — your friends don't need to install it.\n" +
                "Cheats you cast affect your own character.",
                "Para's Trainer — Don't Starve Together",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // No live IPC status stream for DST.
        public override void ParseStatus(string key, string value) { }

        // No trainer-driven actions; everything runs in-game via the Lua mod.
        public override void ExecuteHotkey(string action) { }
    }
}
