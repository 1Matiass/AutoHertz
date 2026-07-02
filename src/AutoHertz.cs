using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;

namespace AutoHertz
{
    // ================= tema =================
    static class T
    {
        public static readonly Color Bg      = Color.FromArgb(26, 27, 38);
        public static readonly Color Card     = Color.FromArgb(40, 42, 58);
        public static readonly Color CardSel  = Color.FromArgb(52, 54, 78);
        public static readonly Color Border   = Color.FromArgb(58, 60, 82);
        public static readonly Color Accent   = Color.FromArgb(120, 132, 255);
        public static readonly Color Accent2  = Color.FromArgb(160, 116, 255);
        public static readonly Color Text     = Color.FromArgb(236, 237, 246);
        public static readonly Color TextDim  = Color.FromArgb(158, 162, 186);
        public static readonly Color Info     = Color.FromArgb(120, 200, 255);
        public static readonly Color Ok       = Color.FromArgb(120, 222, 150);
        public static readonly Color Err      = Color.FromArgb(244, 128, 128);

        public static GraphicsPath Round(Rectangle r, int rad)
        {
            GraphicsPath p = new GraphicsPath();
            int d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // ============= APIs nativas (display + energia) =============
    static class Native
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public ushort dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public uint dmFields;
            public int dmPositionX, dmPositionY;
            public uint dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public ushort dmLogPixels;
            public uint dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public uint dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2;
            public uint dmPanningWidth, dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int EnumDisplaySettings(string dev, int mode, ref DEVMODE dm);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int ChangeDisplaySettings(ref DEVMODE dm, int flags);

        const int ENUM_CURRENT_SETTINGS = -1;
        const int CDS_UPDATEREGISTRY = 0x01, CDS_GLOBAL = 0x08;
        const uint DM_PELSWIDTH = 0x80000, DM_PELSHEIGHT = 0x100000, DM_DISPLAYFREQUENCY = 0x400000;

        static DEVMODE Fresh()
        {
            DEVMODE dm = new DEVMODE();
            dm.dmDeviceName = new string('\0', 32);
            dm.dmFormName = new string('\0', 32);
            dm.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));
            return dm;
        }

        public static int CurrentHz()
        {
            DEVMODE dm = Fresh();
            if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm) != 0) return (int)dm.dmDisplayFrequency;
            return 0;
        }

        public static int MaxHz()
        {
            DEVMODE cur = Fresh();
            if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref cur) == 0) return 60;
            int max = (int)cur.dmDisplayFrequency;
            for (int i = 0; ; i++)
            {
                DEVMODE dm = Fresh();
                if (EnumDisplaySettings(null, i, ref dm) == 0) break;
                if (dm.dmPelsWidth == cur.dmPelsWidth && dm.dmPelsHeight == cur.dmPelsHeight
                    && (int)dm.dmDisplayFrequency > max) max = (int)dm.dmDisplayFrequency;
            }
            return max;
        }

        public static int[] AvailableHz()
        {
            DEVMODE cur = Fresh();
            if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref cur) == 0) return new int[] { 60 };
            List<int> list = new List<int>();
            for (int i = 0; ; i++)
            {
                DEVMODE dm = Fresh();
                if (EnumDisplaySettings(null, i, ref dm) == 0) break;
                if (dm.dmPelsWidth == cur.dmPelsWidth && dm.dmPelsHeight == cur.dmPelsHeight)
                {
                    int hz = (int)dm.dmDisplayFrequency;
                    if (hz > 1 && !list.Contains(hz)) list.Add(hz);
                }
            }
            if (list.Count == 0) list.Add((int)cur.dmDisplayFrequency);
            list.Sort();
            return list.ToArray();
        }

        public static bool SetHz(int hz)
        {
            DEVMODE dm = Fresh();
            if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm) == 0) return false;
            dm.dmDisplayFrequency = (uint)hz;
            dm.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY;
            return ChangeDisplaySettings(ref dm, CDS_UPDATEREGISTRY | CDS_GLOBAL) == 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SPS { public byte ac, bf, blp, ssf; public int blt, bflt; }
        [DllImport("kernel32.dll")] static extern bool GetSystemPowerStatus(out SPS s);

        public static bool OnAC() { SPS s; return GetSystemPowerStatus(out s) && s.ac == 1; }
        public static bool SaverOn() { SPS s; return GetSystemPowerStatus(out s) && s.ssf == 1; }
    }

    // ================= cartão de opção =================
    class OptionCard : Panel
    {
        public int Value;
        public string Icon = "", Title = "", Desc = "";
        public bool Selected;
        public event EventHandler Picked;

        public OptionCard()
        {
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Click += delegate { if (Picked != null) Picked(this, EventArgs.Empty); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            float s = g.DpiX / 96f;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = T.Round(r, (int)(14 * s)))
            {
                using (SolidBrush b = new SolidBrush(Selected ? T.CardSel : T.Card)) g.FillPath(b, path);
                using (Pen p = new Pen(Selected ? T.Accent : T.Border, Selected ? 2f : 1f)) g.DrawPath(p, path);
            }
            int cx = (int)(30 * s), cy = Height / 2, rad = (int)(10 * s);
            using (Pen p = new Pen(Selected ? T.Accent : Color.FromArgb(120, 124, 150), 2f))
                g.DrawEllipse(p, cx - rad, cy - rad, rad * 2, rad * 2);
            if (Selected) using (SolidBrush b = new SolidBrush(T.Accent))
                g.FillEllipse(b, cx - rad / 2 - 1, cy - rad / 2 - 1, rad + 2, rad + 2);

            using (Font f = new Font("Segoe UI Emoji", 15f))
                g.DrawString(Icon, f, Brushes.White, (int)(52 * s), cy - (int)(17 * s));
            using (Font f = new Font("Segoe UI", 12.5f, FontStyle.Bold))
            using (SolidBrush b = new SolidBrush(T.Text))
                g.DrawString(Title, f, b, (int)(96 * s), (int)(16 * s));
            using (Font f = new Font("Segoe UI", 9.5f))
            using (SolidBrush b = new SolidBrush(T.TextDim))
                g.DrawString(Desc, f, b, new RectangleF((int)(96 * s), (int)(42 * s), Width - (int)(112 * s), Height - (int)(46 * s)));
        }
    }

    // ================= botão arredondado =================
    class RoundButton : Button
    {
        public RoundButton()
        {
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            ForeColor = Color.White; Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : T.Bg);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = T.Round(r, (int)(12 * (g.DpiX / 96f))))
            {
                if (!Enabled)
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(54, 56, 78))) g.FillPath(b, path);
                else
                    using (LinearGradientBrush b = new LinearGradientBrush(r, T.Accent, T.Accent2, LinearGradientMode.Horizontal))
                        g.FillPath(b, path);
            }
            Color tc = Enabled ? Color.White : Color.FromArgb(130, 134, 156);
            TextRenderer.DrawText(g, Text, Font, r, tc,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    // ================= chip de taxa (Hz) clicavel =================
    class Chip : Button
    {
        public int Hz;
        public bool Selected;
        public Chip(int hz)
        {
            Hz = hz;
            Text = hz + " Hz";
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            Margin = new Padding(0, 0, 8, 8);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : T.Bg);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = T.Round(r, (int)(10 * (g.DpiX / 96f))))
            {
                using (SolidBrush b = new SolidBrush(Selected ? T.Accent : T.Card)) g.FillPath(b, p);
                using (Pen pen = new Pen(Selected ? T.Accent : T.Border, 1f)) g.DrawPath(pen, p);
            }
            Color tc = Selected ? Color.White : T.Text;
            TextRenderer.DrawText(g, Text, Font, r, tc,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    // ================= janela principal =================
    class MainForm : Form
    {
        OptionCard c1, c2, c3;
        RoundButton apply;
        Label status, liveLabel;
        FlowLayoutPanel chipPanel;
        List<Chip> chips = new List<Chip>();
        System.Windows.Forms.Timer live;
        int selected = 0, maxHz;
        float sc;

        int S(int v) { return (int)Math.Round(v * sc); }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int on = 1; DwmSetWindowAttribute(Handle, 20, ref on, 4); } catch { }
        }

        public MainForm() : this(0) { }

        public MainForm(int preselect)
        {
            using (Graphics g = CreateGraphics()) sc = g.DpiX / 96f;
            maxHz = Native.MaxHz();

            Text = "AutoHertz";
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            BackColor = T.Bg;
            Font = new Font("Segoe UI", 9.75f);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(S(540), S(642));

            AddLabel("AutoHertz", 28, 18, 22f, FontStyle.Bold, T.Text);
            AddLabel("Ajuste a taxa de atualização da tela", 30, 60, 10.5f, FontStyle.Regular, T.TextDim);

            liveLabel = AddLabel("Atual: -- Hz", 30, 88, 12.5f, FontStyle.Bold, T.Info);
            AddLabel("Toque para mudar agora (taxas que esta tela suporta):", 30, 118, 9.5f, FontStyle.Regular, T.TextDim);

            chipPanel = new FlowLayoutPanel();
            chipPanel.Bounds = new Rectangle(S(30), S(140), S(484), S(48));
            chipPanel.BackColor = T.Bg;
            chipPanel.FlowDirection = FlowDirection.LeftToRight;
            chipPanel.WrapContents = true;
            chipPanel.Padding = new Padding(0);
            Controls.Add(chipPanel);
            foreach (int hz in Native.AvailableHz())
            {
                Chip ch = new Chip(hz);
                ch.Size = new Size(S(68), S(38));
                int local = hz;
                ch.Click += delegate { OnChip(local); };
                chipPanel.Controls.Add(ch);
                chips.Add(ch);
            }

            AddLabel("Ou deixe automático (economiza sozinho):", 30, 198, 9.5f, FontStyle.Regular, T.TextDim);

            c1 = MakeCard(1, "🔋", "Economia de energia",
                "Cai para 60 Hz só quando o Windows entrar no modo economia de energia. Fora dele, " + maxHz + " Hz.", 222, 84);
            c2 = MakeCard(2, "🔌", "Fora da tomada",
                "Cai para 60 Hz sempre que estiver na bateria — com ou sem economia. Na tomada, " + maxHz + " Hz.", 314, 84);
            c3 = MakeCard(3, "🧹", "Remover automação",
                "Desativa e apaga toda a configuração. Volta ao controle manual.", 406, 84);

            apply = new RoundButton();
            apply.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            apply.Bounds = new Rectangle(S(30), S(502), S(484), S(48));
            apply.Text = "Selecione uma opção";
            apply.Enabled = false;
            apply.Click += OnApply;
            Controls.Add(apply);

            status = new Label();
            status.AutoSize = false;
            status.Bounds = new Rectangle(S(30), S(558), S(484), S(30));
            status.ForeColor = T.TextDim;
            status.TextAlign = ContentAlignment.MiddleCenter;
            status.Font = new Font("Segoe UI", 9.5f);
            Controls.Add(status);

            Label contact = new Label();
            contact.AutoSize = false;
            contact.Bounds = new Rectangle(S(30), S(608), S(484), S(26));
            contact.Text = "📱  Contato / WhatsApp:  (74) 99988-7338";
            contact.ForeColor = T.Info;
            contact.TextAlign = ContentAlignment.MiddleCenter;
            contact.Font = new Font("Segoe UI", 9.5f);
            contact.Cursor = Cursors.Hand;
            contact.BackColor = Color.Transparent;
            contact.Click += delegate {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo("https://wa.me/5574999887338");
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                }
                catch { }
            };
            Controls.Add(contact);

            int cur = Installer.CurrentMode();
            if (cur == 1) Select(c1);
            else if (cur == 2) Select(c2);

            if (preselect >= 1 && preselect <= 3)
                Select(preselect == 1 ? c1 : (preselect == 2 ? c2 : c3));

            RefreshLive();
            live = new System.Windows.Forms.Timer();
            live.Interval = 1000;
            live.Tick += delegate { RefreshLive(); };
            live.Start();
        }

        void RefreshLive()
        {
            int c = Native.CurrentHz();
            liveLabel.Text = "Atual: " + c + " Hz     ·     esta tela vai até " + maxHz + " Hz";
            for (int i = 0; i < chips.Count; i++)
            {
                bool sel = (chips[i].Hz == c);
                if (chips[i].Selected != sel) { chips[i].Selected = sel; chips[i].Invalidate(); }
            }
        }

        Label AddLabel(string txt, int x, int y, float pt, FontStyle st, Color col)
        {
            Label l = new Label();
            l.Text = txt; l.AutoSize = true;
            l.Location = new Point(S(x), S(y));
            l.Font = new Font("Segoe UI", pt, st);
            l.ForeColor = col;
            l.BackColor = Color.Transparent;
            Controls.Add(l);
            return l;
        }

        OptionCard MakeCard(int val, string icon, string title, string desc, int y, int h)
        {
            OptionCard c = new OptionCard();
            c.Value = val; c.Icon = icon; c.Title = title; c.Desc = desc;
            c.Bounds = new Rectangle(S(30), S(y), S(484), S(h));
            c.Picked += delegate { Select(c); };
            Controls.Add(c);
            return c;
        }

        void Select(OptionCard c)
        {
            selected = c.Value;
            c1.Selected = (c == c1); c2.Selected = (c == c2); c3.Selected = (c == c3);
            c1.Invalidate(); c2.Invalidate(); c3.Invalidate();
            apply.Enabled = true;
            apply.Text = (selected == 3) ? "Remover e limpar" : "Aplicar configuração";
            apply.Invalidate();
        }

        void OnChip(int hz)
        {
            try
            {
                Installer.StopAuto();
                Native.SetHz(hz);
                selected = 0;
                c1.Selected = c2.Selected = c3.Selected = false;
                c1.Invalidate(); c2.Invalidate(); c3.Invalidate();
                apply.Enabled = false; apply.Text = "Selecione uma opção"; apply.Invalidate();
                RefreshLive();
                status.ForeColor = T.Ok;
                status.Text = "✔ Tela fixa em " + hz + " Hz (manual). Automático desligado.";
            }
            catch (Exception ex) { status.ForeColor = T.Err; status.Text = "Erro: " + ex.Message; }
        }

        void OnApply(object sender, EventArgs e)
        {
            try
            {
                if (selected == 3)
                {
                    Installer.Uninstall();
                    status.ForeColor = T.Ok;
                    status.Text = "✔ Automação removida. Controle manual restaurado.";
                    c3.Selected = false; c3.Invalidate();
                    apply.Enabled = false; apply.Text = "Pronto"; apply.Invalidate();
                    RefreshLive();
                }
                else
                {
                    Installer.Install(selected);
                    status.ForeColor = T.Ok;
                    status.Text = "✔ Configurado! Inicia sozinho ao ligar o PC.";
                    apply.Text = "Aplicado ✓"; apply.Invalidate();
                    RefreshLive();
                }
            }
            catch (Exception ex)
            {
                status.ForeColor = T.Err;
                status.Text = "Erro: " + ex.Message;
            }
        }
    }

    // ================= vigia (roda escondido) =================
    class Watcher : Form
    {
        int mode, hi, lo;
        int acdc = -1, saver = -1;   // ultimo valor recebido via evento (-1 = ainda sem evento)
        System.Windows.Forms.Timer t;
        static readonly Guid GUID_ACDC = new Guid("5d3e9a59-e9d5-4b00-a6bd-ff34ff516548");
        static readonly Guid GUID_SAVER = new Guid("e00958c0-c213-4ace-ac77-fecced2eeea5");
        const int WM_POWERBROADCAST = 0x0218;
        const int PBT_POWERSETTINGCHANGE = 0x8013;
        [StructLayout(LayoutKind.Sequential)]
        struct PBS { public Guid PowerSetting; public uint DataLength; public byte Data; }
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr RegisterPowerSettingNotification(IntPtr h, ref Guid g, int flags);

        public Watcher(int m)
        {
            mode = m;
            int[] av = Native.AvailableHz();
            hi = (av.Length > 0) ? av[av.Length - 1] : Native.MaxHz();   // maior taxa suportada
            lo = 60;
            bool has60 = false;
            for (int i = 0; i < av.Length; i++) if (av[i] == 60) has60 = true;
            if (!has60 && av.Length > 0) lo = av[0];                     // se nao tiver 60, usa a menor
            if (lo > hi) lo = hi;
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-4000, -4000);
            Size = new Size(0, 0);
            Opacity = 0;
        }

        protected override void SetVisibleCore(bool value)
        {
            if (!IsHandleCreated) CreateHandle();   // cria o handle (dispara OnHandleCreated) sem mostrar
            base.SetVisibleCore(false);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Guid a = GUID_ACDC, s = GUID_SAVER;
            RegisterPowerSettingNotification(Handle, ref a, 0);
            RegisterPowerSettingNotification(Handle, ref s, 0);
            t = new System.Windows.Forms.Timer();
            t.Interval = 5000;
            t.Tick += delegate { ApplyState(); };
            t.Start();
            ApplyState();
        }

        void ApplyState()
        {
            bool low;
            if (mode == 1) low = (saver >= 0) ? (saver == 1) : Native.SaverOn();
            else low = (acdc >= 0) ? (acdc != 0) : !Native.OnAC();
            int target = low ? lo : hi;
            if (Native.CurrentHz() != target) Native.SetHz(target);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_POWERBROADCAST)
            {
                if ((int)m.WParam == PBT_POWERSETTINGCHANGE && m.LParam != IntPtr.Zero)
                {
                    PBS ps = (PBS)Marshal.PtrToStructure(m.LParam, typeof(PBS));
                    if (ps.PowerSetting == GUID_ACDC) acdc = ps.Data;
                    else if (ps.PowerSetting == GUID_SAVER) saver = ps.Data;
                }
                ApplyState();
            }
            base.WndProc(ref m);
        }
    }

    // ================= instalador / desinstalador =================
    static class Installer
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "AutoHertz";

        public static string Dir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AutoHertz");
        }
        public static string Exe() { return Path.Combine(Dir(), "AutoHertz.exe"); }

        public static int CurrentMode()
        {
            try
            {
                string f = Path.Combine(Dir(), "mode.txt");
                if (File.Exists(f)) { int m; if (int.TryParse(File.ReadAllText(f).Trim(), out m)) return m; }
            }
            catch { }
            return 0;
        }

        public static void Install(int mode)
        {
            CleanupLegacy();
            StopWatchers();
            Directory.CreateDirectory(Dir());
            string src = Application.ExecutablePath, dst = Exe();
            if (!string.Equals(src, dst, StringComparison.OrdinalIgnoreCase)) File.Copy(src, dst, true);
            File.WriteAllText(Path.Combine(Dir(), "mode.txt"), mode.ToString());
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                k.SetValue(RunName, "\"" + dst + "\" --watch " + mode);
            ProcessStartInfo psi = new ProcessStartInfo(dst, "--watch " + mode);
            psi.UseShellExecute = true;                       // desanexa o vigia (sem herdar handles)
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.WorkingDirectory = Dir();
            Process.Start(psi);
        }

        public static void Uninstall()
        {
            StopWatchers();
            try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true)) if (k != null) k.DeleteValue(RunName, false); }
            catch { }
            CleanupLegacy();
            try
            {
                string d = Dir(), self = Application.ExecutablePath;
                if (Directory.Exists(d))
                {
                    foreach (string f in Directory.GetFiles(d))
                        if (!string.Equals(f, self, StringComparison.OrdinalIgnoreCase)) { try { File.Delete(f); } catch { } }
                    if (!string.Equals(self, Exe(), StringComparison.OrdinalIgnoreCase))
                        try { Directory.Delete(d, true); } catch { }
                }
            }
            catch { }
        }

        static void StopWatchers()
        {
            int me = Process.GetCurrentProcess().Id;
            foreach (Process p in Process.GetProcessesByName("AutoHertz"))
                try { if (p.Id != me && p.MainWindowHandle == IntPtr.Zero) p.Kill(); } catch { }
        }

        // desliga o automatico (para o vigia + tira do autostart) sem apagar a pasta
        public static void StopAuto()
        {
            StopWatchers();
            try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true)) if (k != null) k.DeleteValue(RunName, false); } catch { }
            try { string f = Path.Combine(Dir(), "mode.txt"); if (File.Exists(f)) File.Delete(f); } catch { }
        }

        // remove o setup antigo (RefreshByPower) desta máquina, se existir
        static void CleanupLegacy()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("schtasks.exe", "/delete /tn \"RefreshByPowerLogon\" /f");
                psi.UseShellExecute = false; psi.CreateNoWindow = true; psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process pr = Process.Start(psi); if (pr != null) pr.WaitForExit(4000);
            }
            catch { }
            // nome antigo do proprio app (RefreshAuto), antes de virar AutoHertz
            try { foreach (Process p in Process.GetProcessesByName("RefreshAuto")) try { p.Kill(); } catch { } } catch { }
            try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true)) if (k != null) k.DeleteValue("RefreshAuto", false); } catch { }
            try { string old = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "RefreshAuto"); if (Directory.Exists(old)) Directory.Delete(old, true); } catch { }
        }
    }

    // ================= gerador de icone =================
    static class IconMaker
    {
        static Bitmap RenderIcon(int sz)
        {
            Bitmap bmp = new Bitmap(sz, sz, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);

                float pad = sz * 0.055f;
                Rectangle r = new Rectangle((int)pad, (int)pad, (int)(sz - 2 * pad), (int)(sz - 2 * pad));
                int rad = (int)(sz * 0.23f);
                using (GraphicsPath p = T.Round(r, rad))
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(r,
                        Color.FromArgb(124, 132, 255), Color.FromArgb(170, 108, 255), 55f))
                        g.FillPath(b, p);
                }

                // arco de "refresh" (auto) sutil em branco
                using (Pen pen = new Pen(Color.FromArgb(75, 255, 255, 255), Math.Max(1f, sz * 0.055f)))
                {
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                    float m = sz * 0.235f;
                    RectangleF arc = new RectangleF(m, m, sz - 2 * m, sz - 2 * m);
                    g.DrawArc(pen, arc, -50, 265);
                }

                // "Hz"
                using (Font f = new Font("Segoe UI", sz * 0.40f, FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString("Hz", f, Brushes.White, new RectangleF(0, sz * 0.015f, sz, sz), sf);
                }
            }
            return bmp;
        }

        public static void SavePreview(string pngPath)
        {
            using (Bitmap b = RenderIcon(256)) b.Save(pngPath, System.Drawing.Imaging.ImageFormat.Png);
        }

        public static void Save(string icoPath)
        {
            int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
            List<byte[]> pngs = new List<byte[]>();
            foreach (int s in sizes)
            {
                using (Bitmap b = RenderIcon(s))
                using (MemoryStream ms = new MemoryStream())
                {
                    b.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    pngs.Add(ms.ToArray());
                }
            }
            using (FileStream fs = new FileStream(icoPath, FileMode.Create))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                bw.Write((short)0);              // reservado
                bw.Write((short)1);              // tipo = icone
                bw.Write((short)sizes.Length);   // qtd imagens
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    int s = sizes[i];
                    bw.Write((byte)(s >= 256 ? 0 : s));  // largura
                    bw.Write((byte)(s >= 256 ? 0 : s));  // altura
                    bw.Write((byte)0);                   // paleta
                    bw.Write((byte)0);                   // reservado
                    bw.Write((short)1);                  // planos
                    bw.Write((short)32);                 // bits/pixel
                    bw.Write(pngs[i].Length);            // tamanho dos dados
                    bw.Write(offset);                    // offset
                    offset += pngs[i].Length;
                }
                for (int i = 0; i < pngs.Count; i++) bw.Write(pngs[i]);
            }
        }
    }

    static class Program
    {
        static Mutex mtx;
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "--icon")
            {
                IconMaker.Save(args[1]);
                if (args.Length >= 3) IconMaker.SavePreview(args[2]);
                return;
            }
            if (args.Length >= 2 && args[0] == "--shot")
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                int pre = 0; if (args.Length >= 3) int.TryParse(args[2], out pre);
                MainForm f = new MainForm(pre);
                f.StartPosition = FormStartPosition.Manual;
                f.Location = new Point(-2200, -2200);
                f.Show();
                for (int i = 0; i < 12; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(60); }
                System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(f.Width, f.Height);
                f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                bmp.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
                f.Close();
                return;
            }
            if (args.Length >= 1 && args[0] == "--diag")
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("MaxHz=" + Native.MaxHz());
                string av = ""; foreach (int v in Native.AvailableHz()) av += (av.Length == 0 ? "" : ",") + v;
                sb.AppendLine("Available=" + av);
                sb.AppendLine("CurrentHz=" + Native.CurrentHz());
                sb.AppendLine("OnAC=" + Native.OnAC());
                sb.AppendLine("SaverOn=" + Native.SaverOn());
                if (args.Length >= 3 && args[1] == "set")
                {
                    int hz; int.TryParse(args[2], out hz);
                    bool ok = Native.SetHz(hz);
                    System.Threading.Thread.Sleep(900);
                    sb.AppendLine("SetHz(" + hz + ")=" + ok + " -> now " + Native.CurrentHz());
                }
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "refreshauto_diag.txt"), sb.ToString());
                return;
            }
            if (args.Length >= 2 && args[0] == "--install")
            {
                int m; if (int.TryParse(args[1], out m) && m >= 1 && m <= 2) Installer.Install(m);
                return;
            }
            if (args.Length >= 1 && args[0] == "--uninstall")
            {
                Installer.Uninstall();
                return;
            }
            if (args.Length >= 2 && args[0] == "--watch")
            {
                bool created;
                mtx = new Mutex(true, "Global\\AutoHertzWatcher", out created);
                if (!created) return;
                int mode; if (!int.TryParse(args[1], out mode)) return;
                Application.Run(new Watcher(mode));
                GC.KeepAlive(mtx);
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
