using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ParasTrainer
{
    // ── External memory engine (dependency-free P/Invoke) ──────────────────────
    // Palworld is UE5, so there is no game-side BepInEx core. This plugin IS the
    // trainer: it attaches to the running Palworld-Win64-Shipping.exe and reads/
    // writes its memory directly from the ParasTrainer process (which runs 64-bit).
    // v0.1 proves attach + read; feature signatures (AOB/pointer paths) come next.
    internal class PalMem
    {
        [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(int access, bool inherit, int pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out int read);
        [DllImport("kernel32.dll")] static extern bool WriteProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out int written);

        const int PROCESS_VM_READ = 0x0010, PROCESS_VM_WRITE = 0x0020, PROCESS_VM_OPERATION = 0x0008, PROCESS_QUERY_INFORMATION = 0x0400;
        const int ACCESS = PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION | PROCESS_QUERY_INFORMATION;

        IntPtr _handle = IntPtr.Zero;
        Process _proc;
        public long ModuleBase;
        public int ModuleSize;
        public string LastError = "";

        public bool Attached { get { return _handle != IntPtr.Zero && _proc != null && !SafeExited(); } }
        bool SafeExited() { try { return _proc.HasExited; } catch { return true; } }

        public bool Attach(string procName)
        {
            try
            {
                Process[] ps = Process.GetProcessesByName(procName);
                if (ps.Length == 0) { LastError = "process not found"; return false; }
                _proc = ps[0];
                _handle = OpenProcess(ACCESS, false, _proc.Id);
                if (_handle == IntPtr.Zero) { LastError = "OpenProcess failed (try running the trainer as admin)"; return false; }
                try { ModuleBase = (long)_proc.MainModule.BaseAddress; ModuleSize = _proc.MainModule.ModuleMemorySize; }
                catch (Exception mex) { LastError = "MainModule: " + mex.Message; ModuleBase = 0; ModuleSize = 0; }
                LastError = "";
                return true;
            }
            catch (Exception e) { LastError = e.Message; return false; }
        }

        public void Detach()
        {
            if (_handle != IntPtr.Zero) { try { CloseHandle(_handle); } catch { } }
            _handle = IntPtr.Zero; _proc = null; ModuleBase = 0; ModuleSize = 0;
        }

        public byte[] ReadBytes(long addr, int size)
        {
            byte[] buf = new byte[size]; int read;
            if (!ReadProcessMemory(_handle, (IntPtr)addr, buf, size, out read) || read != size) return null;
            return buf;
        }
        public int ReadInt(long a) { byte[] b = ReadBytes(a, 4); return b == null ? 0 : BitConverter.ToInt32(b, 0); }
        public float ReadFloat(long a) { byte[] b = ReadBytes(a, 4); return b == null ? 0f : BitConverter.ToSingle(b, 0); }
        public long ReadLong(long a) { byte[] b = ReadBytes(a, 8); return b == null ? 0L : BitConverter.ToInt64(b, 0); }

        public bool WriteBytes(long addr, byte[] b) { int w; return WriteProcessMemory(_handle, (IntPtr)addr, b, b.Length, out w) && w == b.Length; }
        public bool WriteInt(long a, int v) { return WriteBytes(a, BitConverter.GetBytes(v)); }
        public bool WriteFloat(long a, float v) { return WriteBytes(a, BitConverter.GetBytes(v)); }

        // Resolve a multi-level pointer: start at baseAddr, deref (baseAddr+off) 64-bit
        // for every offset except the last, which is added to give the final address.
        public long Resolve(long baseAddr, int[] offsets)
        {
            long addr = baseAddr;
            for (int i = 0; i < offsets.Length; i++)
            {
                if (i == offsets.Length - 1) return addr + offsets[i];
                addr = ReadLong(addr + offsets[i]);
                if (addr == 0) return 0;
            }
            return addr;
        }

        // AOB scan the main module. Pattern = "48 8B ?? ?? 00" ('??' or '?' = wildcard).
        public long AobScanModule(string pattern)
        {
            if (ModuleBase == 0 || ModuleSize <= 0) return 0;
            string[] toks = pattern.Split(' ');
            int n = toks.Length;
            byte[] pat = new byte[n]; bool[] wild = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (toks[i] == "??" || toks[i] == "?") wild[i] = true;
                else pat[i] = Convert.ToByte(toks[i], 16);
            }
            const int chunk = 0x100000; // 1 MB
            for (long off = 0; off < ModuleSize; off += chunk - n)
            {
                int rd = (int)Math.Min(chunk, ModuleSize - off);
                byte[] buf = ReadBytes(ModuleBase + off, rd);
                if (buf == null) continue;
                for (int i = 0; i + n <= buf.Length; i++)
                {
                    bool ok = true;
                    for (int j = 0; j < n; j++) { if (!wild[j] && buf[i + j] != pat[j]) { ok = false; break; } }
                    if (ok) return ModuleBase + off + i;
                }
            }
            return 0;
        }
    }

    public class PalworldPlugin : GamePlugin
    {
        public override string GameName { get { return "Palworld"; } }
        public override string ShortName { get { return "Palworld"; } }
        public override string SteamId { get { return "1623730"; } }
        public override string ProcessName { get { return "Palworld-Win64-Shipping"; } }
        public override Color AccentColor { get { return Color.FromArgb(96, 200, 120); } }   // pal green
        public override Color AccentDimColor { get { return Color.FromArgb(30, 70, 42); } }
        public override string IconPath { get { return @"J:\SteamLibrary\steamapps\common\Palworld\Palworld.exe"; } }
        public override string IpcDirectory { get { return Path.Combine(Path.GetTempPath(), "palworld_trainer"); } }
        public override string PluginVersion { get { return "1"; } }

        private const string CoreVersion = "0.1.0";
        private static readonly PalMem Mem = new PalMem();

        // shared state written by the attach thread, shown by the panel
        private static volatile bool _attached;
        private static volatile string _baseHex = "-";
        private static volatile string _verify = "-";
        private Thread _worker;
        private volatile bool _run;

        private Label lblAttached, lblBase, lblVerify;

        public PalworldPlugin()
        {
            try { Directory.CreateDirectory(IpcDirectory); } catch { }
            _run = true;
            _worker = new Thread(WorkerLoop);
            _worker.IsBackground = true;
            _worker.Name = "PalworldAttach";
            _worker.Start();
        }

        // Background: attach to the game, verify a read, and keep status.txt fresh so
        // the ParasTrainer connection UI shows "connected" while attached.
        private void WorkerLoop()
        {
            string statusFile = Path.Combine(IpcDirectory, "status.txt");
            while (_run)
            {
                try
                {
                    if (!Mem.Attached)
                    {
                        _attached = false; _baseHex = "-"; _verify = "-";
                        Mem.Attach("Palworld-Win64-Shipping");
                    }
                    if (Mem.Attached)
                    {
                        _attached = true;
                        _baseHex = "0x" + Mem.ModuleBase.ToString("X");
                        // Read the PE header at module base — "MZ" (0x4D 0x5A) proves RPM works.
                        byte[] mz = Mem.ReadBytes(Mem.ModuleBase, 2);
                        _verify = (mz != null && mz[0] == 0x4D && mz[1] == 0x5A) ? "OK (MZ read)" : "read FAILED";
                        // keep status fresh → framework shows connected
                        try
                        {
                            File.WriteAllText(statusFile,
                                "CoreVersion=" + CoreVersion + "\nPatches=1"
                                + "\nAttached=1"
                                + "\nModuleBase=" + _baseHex
                                + "\nVerify=" + _verify);
                        }
                        catch { }
                    }
                }
                catch { }
                Thread.Sleep(1000);
            }
        }

        public override void InitDefaultHotkeys(Dictionary<string, Keys> b) { }

        public override void BuildPanel(Panel c)
        {
            int y = 10;

            y = Host.ColorSectionHeader(c, y, "MEMORY TRAINER (EXTERNAL)", AccentColor);

            y = Host.SectionHeader(c, y, "CONNECTION");
            Panel sc = Host.MakeCard(c, y, 3);
            lblAttached = Line(sc, 0, "Attached:", "waiting for Palworld…");
            lblBase = Line(sc, Theme.ROW_H, "Module base:", "-");
            lblVerify = Line(sc, Theme.ROW_H * 2, "Read test:", "-");
            y += Theme.ROW_H * 3 + Theme.PAD;

            Panel ac = Host.MakeCard(c, y, 1);
            Button re = Host.ActionBtn(ac, Theme.PAD, 7, "Re-Attach / Re-scan", Theme.CARD_W - Theme.PAD * 2);
            re.Click += delegate { Mem.Detach(); };   // co-located: call the engine directly, no IPC
            y += Theme.ROW_H + Theme.PAD;

            y = Host.SectionHeader(c, y, "STATUS");
            Panel nc = Host.MakeCard(c, y, 2);
            Label note = new Label();
            note.Text = "v0.1 validation build — proves the trainer can attach to Palworld and read its memory. "
                      + "Cheat features (god mode, infinite HP/stamina/hunger, XP, capture, items, gold…) come next, "
                      + "once we capture the AOB signatures + pointer paths for your game build.";
            note.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            note.ForeColor = Theme.TEXT_SECONDARY;
            note.Location = new Point(Theme.PAD + 2, 8);
            note.MaximumSize = new Size(Theme.CARD_W - Theme.PAD, 0);
            note.AutoSize = true;
            nc.Controls.Add(note);
            y += Theme.ROW_H * 2 + Theme.PAD;

            Host.AddSpacer(c, y, 20);
        }

        private Label Line(Panel card, int rowY, string caption, string val)
        {
            Label cap = new Label();
            cap.Text = caption;
            cap.Font = new Font("Segoe UI", 9f);
            cap.ForeColor = Theme.TEXT_SECONDARY;
            cap.Location = new Point(Theme.PAD, rowY + 10);
            cap.AutoSize = true;
            card.Controls.Add(cap);

            Label v = new Label();
            v.Text = val;
            v.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            v.ForeColor = AccentColor;
            v.Location = new Point(Theme.PAD + 100, rowY + 10);
            v.AutoSize = true;
            card.Controls.Add(v);
            return v;
        }

        // Called each poll on the UI thread — reflect the worker's state into the labels.
        public override void OnTick()
        {
            if (lblAttached == null) return;
            lblAttached.Text = _attached ? "YES" : "no (start Palworld)";
            lblAttached.ForeColor = _attached ? AccentColor : Theme.TEXT_SECONDARY;
            lblBase.Text = _baseHex;
            lblVerify.Text = _verify;
        }

        public override void ParseStatus(string key, string value) { }
        public override void ExecuteHotkey(string action) { }
    }
}
