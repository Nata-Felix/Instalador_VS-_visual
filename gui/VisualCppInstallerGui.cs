using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("VisualCppInstaller")]
[assembly: AssemblyProduct("Instalador Microsoft Visual C++")]
[assembly: AssemblyCompany("SOLPPE")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace VisualCppInstaller
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm());
        }
    }

    internal sealed class InstallerForm : Form
    {
        private readonly Color blue = Color.FromArgb(0, 92, 190);
        private readonly Color darkBlue = Color.FromArgb(0, 49, 112);
        private readonly Color lightBlue = Color.FromArgb(232, 244, 255);
        private readonly Color border = Color.FromArgb(205, 214, 224);
        private readonly Color muted = Color.FromArgb(110, 119, 135);

        private readonly List<PackageItem> packages = new List<PackageItem>();
        private readonly ListView packageList = new ListView();
        private readonly ProgressBar progressBar = new ProgressBar();
        private readonly Label progressLabel = new Label();
        private readonly Label currentStepLabel = new Label();
        private readonly RichTextBox logBox = new RichTextBox();
        private readonly Label statusLabel = new Label();
        private readonly Button installButton = new Button();
        private readonly Button cancelButton = new Button();
        private readonly Button closeButton = new Button();
        private readonly Button copyLogButton = new Button();
        private readonly CheckBox closeWhenDone = new CheckBox();
        private BackgroundWorker worker;
        private volatile bool cancelRequested;
        private Process runningProcess;
        private string cacheDir;
        private bool restartRequired;
        private int failures;

        public InstallerForm()
        {
            Text = "Instalador Microsoft Visual C++";
            Width = 1024;
            Height = 660;
            MinimumSize = new Size(1024, 660);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            FormClosing += OnFormClosing;

            Icon icon = LoadIcon("VisualCppIcon");
            if (icon != null) Icon = icon;

            BuildPackages();
            BuildLayout();
            PopulatePackageList();
        }

        private void BuildPackages()
        {
            packages.Add(new PackageItem("2005 SP1", "8.0.61001", "x86", "vc2005_x86.exe",
                "https://download.microsoft.com/download/8/b/4/8b42259f-5d70-43f4-ac2e-4b208fd8d66a/vcredist_x86.EXE", "/Q"));
            packages.Add(new PackageItem("2005 SP1", "8.0.61001", "x64", "vc2005_x64.exe",
                "https://download.microsoft.com/download/8/b/4/8b42259f-5d70-43f4-ac2e-4b208fd8d66a/vcredist_x64.EXE", "/Q"));
            packages.Add(new PackageItem("2008 SP1", "9.0.30729.5677", "x86", "vc2008_x86.exe",
                "https://download.microsoft.com/download/5/D/8/5D8C65CB-C849-4025-8E95-C3966CAFD8AE/vcredist_x86.exe", "/q"));
            packages.Add(new PackageItem("2008 SP1", "9.0.30729.5677", "x64", "vc2008_x64.exe",
                "https://download.microsoft.com/download/5/D/8/5D8C65CB-C849-4025-8E95-C3966CAFD8AE/vcredist_x64.exe", "/q"));
            packages.Add(new PackageItem("2010 SP1", "10.0.40219.325", "x86", "vc2010_x86.exe",
                "https://download.microsoft.com/download/1/6/5/165255E7-1014-4D0A-B094-B6A430A6BFFC/vcredist_x86.exe", "/q /norestart"));
            packages.Add(new PackageItem("2010 SP1", "10.0.40219.325", "x64", "vc2010_x64.exe",
                "https://download.microsoft.com/download/1/6/5/165255E7-1014-4D0A-B094-B6A430A6BFFC/vcredist_x64.exe", "/q /norestart"));
            packages.Add(new PackageItem("2012 Update 4", "11.0.61030.0", "x86", "vc2012_x86.exe",
                "https://download.microsoft.com/download/1/6/B/16B06F60-3B20-4FF2-B699-5E9B7962F9AE/VSU_4/vcredist_x86.exe", "/install /quiet /norestart"));
            packages.Add(new PackageItem("2012 Update 4", "11.0.61030.0", "x64", "vc2012_x64.exe",
                "https://download.microsoft.com/download/1/6/B/16B06F60-3B20-4FF2-B699-5E9B7962F9AE/VSU_4/vcredist_x64.exe", "/install /quiet /norestart"));
            packages.Add(new PackageItem("2013", "12.0.40664.0", "x86", "vc2013_x86.exe",
                "https://aka.ms/highdpimfc2013x86enu", "/install /quiet /norestart"));
            packages.Add(new PackageItem("2013", "12.0.40664.0", "x64", "vc2013_x64.exe",
                "https://aka.ms/highdpimfc2013x64enu", "/install /quiet /norestart"));
            packages.Add(new PackageItem("2015-2025 (v14)", "mais recente", "x86", "vc14_x86.exe",
                "https://aka.ms/vc14/vc_redist.x86.exe", "/install /quiet /norestart"));
            packages.Add(new PackageItem("2015-2025 (v14)", "mais recente", "x64", "vc14_x64.exe",
                "https://aka.ms/vc14/vc_redist.x64.exe", "/install /quiet /norestart"));
        }

        private void BuildLayout()
        {
            Panel root = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            Controls.Add(root);
            BuildHeader(root);
            BuildContent(root);
            BuildFooter(root);
        }

        private void BuildHeader(Control root)
        {
            Panel header = new Panel { Left = 24, Top = 10, Width = 958, Height = 130, BackColor = Color.White };
            root.Controls.Add(header);

            PictureBox logo = new PictureBox { Left = 20, Top = 10, Width = 295, Height = 100, SizeMode = PictureBoxSizeMode.Zoom };
            logo.Image = LoadImage("VisualCppLogo");
            header.Controls.Add(logo);

            header.Controls.Add(new Panel { Left = 345, Top = 20, Width = 1, Height = 84, BackColor = border });
            header.Controls.Add(new Label {
                Text = "Instalador Microsoft Visual C++", Left = 380, Top = 26, Width = 550, Height = 42,
                Font = new Font("Segoe UI", 22F, FontStyle.Bold), ForeColor = darkBlue
            });
            header.Controls.Add(new Label {
                Text = "Pacotes redistribuíveis x86 e x64", Left = 384, Top = 72, Width = 520, Height = 28,
                Font = new Font("Segoe UI", 14F), ForeColor = muted
            });
            header.Controls.Add(new Panel { Left = 0, Top = 122, Width = 958, Height = 1, BackColor = border });
        }

        private void BuildContent(Control root)
        {
            root.Controls.Add(SectionLabel("Opção de instalação", 44, 158, 340));
            Panel option = new Panel { Left = 42, Top = 186, Width = 358, Height = 82, BackColor = lightBlue };
            option.Paint += delegate(object sender, PaintEventArgs e) {
                using (Pen p = new Pen(Color.FromArgb(54, 140, 230))) e.Graphics.DrawRectangle(p, 0, 0, option.Width - 1, option.Height - 1);
            };
            option.Controls.Add(new RadioButton { Left = 12, Top = 28, Width = 24, Height = 24, Checked = true, Enabled = false });
            option.Controls.Add(new PackageIcon { Left = 50, Top = 21, Width = 38, Height = 38, ForeColor = blue });
            option.Controls.Add(new Label { Text = "Microsoft Visual C++", Left = 100, Top = 14, Width = 220, Height = 27, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Color.FromArgb(28, 36, 48) });
            option.Controls.Add(new Label { Text = "2005 a 2025 • x86 e x64", Left = 100, Top = 42, Width = 220, Height = 24, ForeColor = muted });
            root.Controls.Add(option);

            root.Controls.Add(SectionLabel("Ordem dos pacotes", 44, 286, 340));
            packageList.Left = 42; packageList.Top = 314; packageList.Width = 358; packageList.Height = 236;
            packageList.View = View.Details; packageList.FullRowSelect = true; packageList.GridLines = true;
            packageList.HeaderStyle = ColumnHeaderStyle.Nonclickable; packageList.MultiSelect = false;
            packageList.Columns.Add("Ano", 110); packageList.Columns.Add("Versão", 110); packageList.Columns.Add("Arq.", 45); packageList.Columns.Add("Status", 70);
            root.Controls.Add(packageList);

            root.Controls.Add(SectionLabel("Progresso da execução", 420, 158, 360));
            progressBar.Left = 420; progressBar.Top = 196; progressBar.Width = 480; progressBar.Height = 28;
            root.Controls.Add(progressBar);
            progressLabel.Text = "0% concluído"; progressLabel.Left = 920; progressLabel.Top = 199; progressLabel.Width = 90; progressLabel.Height = 24;
            progressLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold); progressLabel.ForeColor = blue;
            root.Controls.Add(progressLabel);
            GearPanel gear = new GearPanel { Left = 420, Top = 240, Width = 34, Height = 34, ForeColor = Color.FromArgb(24, 118, 224) };
            root.Controls.Add(gear);
            currentStepLabel.Text = "Aguardando início da instalação"; currentStepLabel.Left = 462; currentStepLabel.Top = 244;
            currentStepLabel.Width = 510; currentStepLabel.Height = 28; currentStepLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            currentStepLabel.ForeColor = Color.FromArgb(35, 43, 55); root.Controls.Add(currentStepLabel);

            root.Controls.Add(SectionLabel("Log de execução", 420, 294, 360));
            logBox.Left = 420; logBox.Top = 322; logBox.Width = 560; logBox.Height = 228; logBox.ReadOnly = true;
            logBox.ScrollBars = RichTextBoxScrollBars.Vertical; logBox.Font = new Font("Consolas", 9.25F); logBox.BackColor = Color.White;
            logBox.BorderStyle = BorderStyle.FixedSingle; root.Controls.Add(logBox);

            copyLogButton.Text = "Copiar log"; copyLogButton.Left = 862; copyLogButton.Top = 286; copyLogButton.Width = 118; copyLogButton.Height = 30;
            copyLogButton.FlatStyle = FlatStyle.Flat; copyLogButton.FlatAppearance.BorderColor = border; copyLogButton.BackColor = Color.White;
            copyLogButton.Click += delegate { if (!String.IsNullOrWhiteSpace(logBox.Text)) Clipboard.SetText(logBox.Text); };
            root.Controls.Add(copyLogButton);
        }

        private void BuildFooter(Control root)
        {
            root.Controls.Add(new Panel { Left = 0, Top = 564, Width = 1024, Height = 1, BackColor = border });
            InfoCircle info = new InfoCircle { Left = 40, Top = 588, Width = 18, Height = 18, ForeColor = blue };
            root.Controls.Add(info);
            statusLabel.Text = "Pronto para iniciar"; statusLabel.Left = 62; statusLabel.Top = 590; statusLabel.Width = 300; statusLabel.Height = 24;
            root.Controls.Add(statusLabel);
            closeWhenDone.Text = "Fechar automaticamente ao finalizar"; closeWhenDone.Left = 364; closeWhenDone.Top = 584;
            closeWhenDone.Width = 225; closeWhenDone.Height = 24; root.Controls.Add(closeWhenDone);

            ConfigureButton(installButton, "Instalar", 604, 150, true);
            ConfigureButton(cancelButton, "Cancelar", 772, 124, false);
            ConfigureButton(closeButton, "Fechar", 912, 90, false);
            cancelButton.Enabled = false;
            installButton.Click += delegate { StartInstall(); };
            cancelButton.Click += delegate { CancelInstall(); };
            closeButton.Click += delegate { Close(); };
            root.Controls.Add(installButton); root.Controls.Add(cancelButton); root.Controls.Add(closeButton);
        }

        private Label SectionLabel(string text, int left, int top, int width)
        {
            return new Label { Text = text, Left = left, Top = top, Width = width, Height = 24,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = blue };
        }

        private void ConfigureButton(Button button, string text, int left, int width, bool primary)
        {
            button.Text = text; button.Left = left; button.Top = 572; button.Width = width; button.Height = 40;
            button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = primary ? Color.FromArgb(0, 76, 170) : border;
            button.BackColor = primary ? Color.FromArgb(0, 104, 210) : Color.White;
            button.ForeColor = primary ? Color.White : Color.FromArgb(38, 48, 64);
            if (primary) button.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        }

        private void PopulatePackageList()
        {
            packageList.Items.Clear();
            foreach (PackageItem p in packages)
            {
                ListViewItem row = new ListViewItem(p.Year);
                row.SubItems.Add(p.Version); row.SubItems.Add(p.Arch); row.SubItems.Add("Pendente");
                p.Row = row; packageList.Items.Add(row);
            }
        }

        private void StartInstall()
        {
            if (worker != null && worker.IsBusy) return;
            if (MessageBox.Show("Os pacotes serão instalados em ordem de ano, sempre x86 antes de x64. Deseja continuar?",
                "Instalador Microsoft Visual C++", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            cancelRequested = false; restartRequired = false; failures = 0; logBox.Clear(); SetProgress(0);
            foreach (PackageItem p in packages) UpdateRow(p, "Pendente", Color.White);
            cacheDir = Path.Combine(Path.GetTempPath(), "VisualCppInstaller_Cache");
            Directory.CreateDirectory(cacheDir);
            installButton.Enabled = false; closeButton.Enabled = false; cancelButton.Enabled = true; closeWhenDone.Enabled = false;
            statusLabel.Text = "Instalação em andamento";

            worker = new BackgroundWorker { WorkerReportsProgress = true };
            worker.DoWork += InstallWorker;
            worker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e) {
                SetProgress(e.ProgressPercentage); currentStepLabel.Text = Convert.ToString(e.UserState);
            };
            worker.RunWorkerCompleted += InstallCompleted;
            worker.RunWorkerAsync();
        }

        private void InstallWorker(object sender, DoWorkEventArgs e)
        {
            int completed = 0;
            AppendLog("[INFO] Ordem: ano crescente, x86 e depois x64.");
            AppendLog("[INFO] Cache: " + cacheDir);
            AppendLog("[INFO] Sistema: " + (Environment.Is64BitOperatingSystem ? "64 bits" : "32 bits"));

            foreach (PackageItem p in packages)
            {
                if (cancelRequested) { e.Cancel = true; return; }
                if (p.Arch == "x64" && !Environment.Is64BitOperatingSystem)
                {
                    UpdateRow(p, "Ignorado", Color.FromArgb(245, 245, 245));
                    AppendLog("[IGNORADO] " + p.DisplayName + " — Windows 32 bits.");
                    completed++; worker.ReportProgress((completed * 100) / packages.Count, "Ignorado: " + p.DisplayName); continue;
                }

                try
                {
                    string local = LocateOrDownload(p);
                    if (cancelRequested) { e.Cancel = true; return; }
                    UpdateRow(p, "Instalando", Color.FromArgb(255, 249, 220));
                    worker.ReportProgress((completed * 100) / packages.Count, "Instalando " + p.DisplayName);
                    AppendLog("[INSTALANDO] " + p.DisplayName);

                    ProcessStartInfo psi = new ProcessStartInfo(local, p.Arguments) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(local) };
                    using (Process process = Process.Start(psi))
                    {
                        runningProcess = process;
                        while (!process.WaitForExit(300))
                        {
                            if (cancelRequested) { TryKill(process); e.Cancel = true; return; }
                        }
                        runningProcess = null;
                        int code = process.ExitCode;
                        if (code == 3010 || code == 1641) restartRequired = true;
                        if (code != 0 && code != 1638 && code != 3010 && code != 1641)
                            throw new InvalidOperationException("ExitCode " + code);
                        UpdateRow(p, code == 1638 ? "Já instalado" : "Concluído", Color.FromArgb(232, 250, 238));
                        AppendLog("[OK] " + p.DisplayName + " — ExitCode " + code);
                    }
                }
                catch (Exception ex)
                {
                    failures++;
                    UpdateRow(p, "Falhou", Color.FromArgb(255, 232, 232));
                    AppendLog("[ERRO] " + p.DisplayName + " — " + ex.Message);
                }

                completed++;
                worker.ReportProgress((completed * 100) / packages.Count, "Processado: " + p.DisplayName);
            }
        }

        private string LocateOrDownload(PackageItem p)
        {
            string appPackages = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "packages");
            string bundled = Path.Combine(appPackages, p.FileName);
            if (IsExecutable(bundled))
            {
                AppendLog("[LOCAL] " + p.FileName);
                return bundled;
            }

            string cached = Path.Combine(cacheDir, p.FileName);
            if (IsExecutable(cached))
            {
                AppendLog("[CACHE] " + p.FileName);
                return cached;
            }

            UpdateRow(p, "Baixando", Color.FromArgb(232, 244, 255));
            UpdateCurrentStep("Baixando " + p.DisplayName);
            AppendLog("[DOWNLOAD] " + p.Url);
            string partial = cached + ".partial";
            if (File.Exists(partial)) File.Delete(partial);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(p.Url);
            request.AllowAutoRedirect = true; request.Timeout = 600000; request.ReadWriteTimeout = 600000;
            using (WebResponse response = request.GetResponse())
            using (Stream input = response.GetResponseStream())
            using (FileStream output = File.Create(partial))
            {
                byte[] buffer = new byte[1024 * 256];
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (cancelRequested) throw new OperationCanceledException("Cancelado pelo usuário.");
                    output.Write(buffer, 0, read);
                }
            }
            if (!IsExecutable(partial)) throw new InvalidDataException("O download não é um executável válido.");
            if (File.Exists(cached)) File.Delete(cached);
            File.Move(partial, cached);
            return cached;
        }

        private static bool IsExecutable(string path)
        {
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length < 1024) return false;
                using (FileStream s = File.OpenRead(path)) return s.ReadByte() == 0x4D && s.ReadByte() == 0x5A;
            }
            catch { return false; }
        }

        private void InstallCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            runningProcess = null; installButton.Enabled = true; closeButton.Enabled = true; cancelButton.Enabled = false; closeWhenDone.Enabled = true;
            if (e.Cancelled || cancelRequested)
            {
                statusLabel.Text = "Instalação cancelada"; currentStepLabel.Text = "Instalação cancelada"; AppendLog("[CANCELADO] Operação interrompida."); return;
            }
            SetProgress(100);
            if (failures > 0)
            {
                statusLabel.Text = failures + " pacote(s) com falha"; currentStepLabel.Text = "Concluído com falhas — consulte o log";
                MessageBox.Show("A instalação terminou com " + failures + " falha(s). Consulte o log.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            statusLabel.Text = restartRequired ? "Concluído — reinicialização necessária" : "Instalação concluída";
            currentStepLabel.Text = statusLabel.Text; AppendLog("[FINALIZADO] Todos os pacotes foram processados.");
            if (restartRequired) AppendLog("[AVISO] Reinicie o Windows para concluir as alterações.");
            if (closeWhenDone.Checked) Close();
        }

        private void CancelInstall()
        {
            cancelRequested = true; cancelButton.Enabled = false; statusLabel.Text = "Cancelando..."; TryKill(runningProcess);
        }

        private static void TryKill(Process process)
        {
            try { if (process != null && !process.HasExited) process.Kill(); } catch { }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (worker != null && worker.IsBusy && !cancelRequested)
            {
                if (MessageBox.Show("A instalação ainda está em andamento. Deseja cancelar e fechar?", Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) { e.Cancel = true; return; }
                cancelRequested = true; TryKill(runningProcess);
            }
        }

        private void UpdateRow(PackageItem p, string status, Color color)
        {
            if (packageList.InvokeRequired) { packageList.BeginInvoke(new Action<PackageItem, string, Color>(UpdateRow), p, status, color); return; }
            p.Row.SubItems[3].Text = status; p.Row.BackColor = color; p.Row.EnsureVisible();
        }

        private void AppendLog(string text)
        {
            if (logBox.InvokeRequired) { logBox.BeginInvoke(new Action<string>(AppendLog), text); return; }
            bool error = text.IndexOf("[ERRO]", StringComparison.OrdinalIgnoreCase) >= 0;
            logBox.SelectionStart = logBox.TextLength; logBox.SelectionColor = error ? Color.Firebrick : Color.FromArgb(24, 30, 38);
            logBox.AppendText(text + Environment.NewLine); logBox.ScrollToCaret();
        }

        private void UpdateCurrentStep(string text)
        {
            if (currentStepLabel.InvokeRequired) { currentStepLabel.BeginInvoke(new Action<string>(UpdateCurrentStep), text); return; }
            currentStepLabel.Text = text;
        }

        private void SetProgress(int value)
        {
            if (InvokeRequired) { BeginInvoke(new Action<int>(SetProgress), value); return; }
            value = Math.Max(0, Math.Min(100, value)); progressBar.Value = value; progressLabel.Text = value + "% concluído";
        }

        private static Image LoadImage(string name)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            return stream == null ? null : Image.FromStream(stream);
        }

        private static Icon LoadIcon(string name)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            return stream == null ? null : new Icon(stream);
        }
    }

    internal sealed class PackageItem
    {
        public readonly string Year, Version, Arch, FileName, Url, Arguments;
        public ListViewItem Row;
        public PackageItem(string year, string version, string arch, string fileName, string url, string arguments)
        { Year = year; Version = version; Arch = arch; FileName = fileName; Url = url; Arguments = arguments; }
        public string DisplayName { get { return "Visual C++ " + Year + " " + Arch; } }
    }

    internal sealed class PackageIcon : Panel
    {
        public PackageIcon() { DoubleBuffered = true; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (Brush b = new SolidBrush(ForeColor))
            using (Brush leftBrush = new SolidBrush(Color.FromArgb(0, 112, 225)))
            using (Brush rightBrush = new SolidBrush(Color.FromArgb(0, 74, 165)))
            {
                Point[] top = { new Point(19, 1), new Point(36, 10), new Point(19, 19), new Point(2, 10) };
                Point[] left = { new Point(2, 10), new Point(19, 19), new Point(19, 37), new Point(2, 28) };
                Point[] right = { new Point(36, 10), new Point(19, 19), new Point(19, 37), new Point(36, 28) };
                e.Graphics.FillPolygon(b, top); e.Graphics.FillPolygon(leftBrush, left); e.Graphics.FillPolygon(rightBrush, right);
            }
        }
    }

    internal sealed class GearPanel : Panel
    {
        public GearPanel() { DoubleBuffered = true; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int cx = Width / 2, cy = Height / 2;
            using (Brush b = new SolidBrush(ForeColor))
            {
                e.Graphics.FillEllipse(b, 6, 6, Width - 12, Height - 12);
                for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; e.Graphics.FillRectangle(b, cx + (int)(Math.Cos(a) * 13) - 3, cy + (int)(Math.Sin(a) * 13) - 3, 6, 6); }
                using (Brush w = new SolidBrush(Color.White)) e.Graphics.FillEllipse(w, cx - 5, cy - 5, 10, 10);
            }
        }
    }

    internal sealed class InfoCircle : Panel
    {
        public InfoCircle() { DoubleBuffered = true; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (Pen p = new Pen(ForeColor, 2F)) using (Brush b = new SolidBrush(ForeColor))
            { e.Graphics.DrawEllipse(p, 1, 1, Width - 3, Height - 3); e.Graphics.FillRectangle(b, Width / 2 - 1, 7, 2, Height - 9); e.Graphics.FillEllipse(b, Width / 2 - 1, 4, 2, 2); }
        }
    }
}
