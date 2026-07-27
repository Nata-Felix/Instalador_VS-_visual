using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("VisualCppInstaller")]
[assembly: AssemblyProduct("Instalador Microsoft Visual C++")]
[assembly: AssemblyCompany("SOLPPE")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

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
        private readonly List<PackageItem> displayItems = new List<PackageItem>();
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
        private readonly CheckBox net35CheckBox = new CheckBox();
        private readonly CheckBox net48CheckBox = new CheckBox();
        private readonly CheckBox crystal2008CheckBox = new CheckBox();
        private readonly CheckBox windowsServerCheckBox = new CheckBox();
        private PackageItem net35Package;
        private PackageItem net48Package;
        private PackageItem crystal2008Package;
        private PackageItem windowsServerPackage;
        private BackgroundWorker worker;
        private volatile bool cancelRequested;
        private Process runningProcess;
        private string cacheDir;
        private bool restartRequired;
        private int failures;

        public InstallerForm()
        {
            Text = "Instalador Microsoft Visual C++ - v1.2.0";
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1024, 740);
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
            net35Package = new PackageItem(".NET 3.5", "Recurso Windows", "x86/x64", "DotNet35Setup.exe",
                "https://go.microsoft.com/fwlink/?LinkID=2337635", "/quiet /norestart",
                ".NET Framework 3.5", PackageKind.NetFx3, true, "");
            net48Package = new PackageItem(".NET 4.8", "4.8", "x86/x64", "dotnet48.exe",
                "https://go.microsoft.com/fwlink/?linkid=2088631", "/q /norestart",
                ".NET Framework 4.8", PackageKind.NetFx48, true, "");
            crystal2008Package = new PackageItem("Crystal 2008", "10.5.0.0", "x86", "CRRedist2008_x86.msi",
                "https://github.com/Nata-Felix/Instalador_VS-_visual/releases/download/v1.2.0/CRRedist2008_x86.msi", "",
                "Crystal Reports 2008 Runtime x86", PackageKind.Msi, true,
                "867267BBCCE888970B5633A8C527F286D80F026FBB72E63608032872D81D6257");
            windowsServerPackage = new PackageItem("Windows Server", "KB2999226", "x64", "Windows8.1-KB2999226-x64.msu",
                "https://github.com/Nata-Felix/Instalador_VS-_visual/releases/download/v1.2.0/Windows8.1-KB2999226-x64.msu", "",
                "Windows Server - KB2999226 x64", PackageKind.Msu, true,
                "9F707096C7D279ED4BC2A40BA695EFAC69C20406E0CA97E2B3E08443C6381D15");

            displayItems.Add(net35Package);
            displayItems.Add(net48Package);
            displayItems.Add(crystal2008Package);
            displayItems.Add(windowsServerPackage);

            AddVisualCpp(new PackageItem("2005 SP1", "8.0.61001", "x86", "vc2005_x86.exe",
                "https://download.microsoft.com/download/8/b/4/8b42259f-5d70-43f4-ac2e-4b208fd8d66a/vcredist_x86.EXE", "/Q"));
            AddVisualCpp(new PackageItem("2005 SP1", "8.0.61001", "x64", "vc2005_x64.exe",
                "https://download.microsoft.com/download/8/b/4/8b42259f-5d70-43f4-ac2e-4b208fd8d66a/vcredist_x64.EXE", "/Q"));
            AddVisualCpp(new PackageItem("2008 SP1", "9.0.30729.5677", "x86", "vc2008_x86.exe",
                "https://download.microsoft.com/download/5/D/8/5D8C65CB-C849-4025-8E95-C3966CAFD8AE/vcredist_x86.exe", "/q"));
            AddVisualCpp(new PackageItem("2008 SP1", "9.0.30729.5677", "x64", "vc2008_x64.exe",
                "https://download.microsoft.com/download/5/D/8/5D8C65CB-C849-4025-8E95-C3966CAFD8AE/vcredist_x64.exe", "/q"));
            AddVisualCpp(new PackageItem("2010 SP1", "10.0.40219.325", "x86", "vc2010_x86.exe",
                "https://download.microsoft.com/download/1/6/5/165255E7-1014-4D0A-B094-B6A430A6BFFC/vcredist_x86.exe", "/q /norestart"));
            AddVisualCpp(new PackageItem("2010 SP1", "10.0.40219.325", "x64", "vc2010_x64.exe",
                "https://download.microsoft.com/download/1/6/5/165255E7-1014-4D0A-B094-B6A430A6BFFC/vcredist_x64.exe", "/q /norestart"));
            AddVisualCpp(new PackageItem("2012 Update 4", "11.0.61030.0", "x86", "vc2012_x86.exe",
                "https://download.microsoft.com/download/1/6/B/16B06F60-3B20-4FF2-B699-5E9B7962F9AE/VSU_4/vcredist_x86.exe", "/install /quiet /norestart"));
            AddVisualCpp(new PackageItem("2012 Update 4", "11.0.61030.0", "x64", "vc2012_x64.exe",
                "https://download.microsoft.com/download/1/6/B/16B06F60-3B20-4FF2-B699-5E9B7962F9AE/VSU_4/vcredist_x64.exe", "/install /quiet /norestart"));
            AddVisualCpp(new PackageItem("2013", "12.0.40664.0", "x86", "vc2013_x86.exe",
                "https://aka.ms/highdpimfc2013x86enu", "/install /quiet /norestart"));
            AddVisualCpp(new PackageItem("2013", "12.0.40664.0", "x64", "vc2013_x64.exe",
                "https://aka.ms/highdpimfc2013x64enu", "/install /quiet /norestart"));
            AddVisualCpp(new PackageItem("2015-2025 (v14)", "mais recente", "x86", "vc14_x86.exe",
                "https://aka.ms/vc14/vc_redist.x86.exe", "/install /quiet /norestart"));
            AddVisualCpp(new PackageItem("2015-2025 (v14)", "mais recente", "x64", "vc14_x64.exe",
                "https://aka.ms/vc14/vc_redist.x64.exe", "/install /quiet /norestart"));
        }

        private void AddVisualCpp(PackageItem item)
        {
            packages.Add(item);
            displayItems.Add(item);
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
            root.Controls.Add(SectionLabel("Componentes opcionais", 44, 158, 340));
            Panel option = new Panel { Left = 42, Top = 186, Width = 358, Height = 190, BackColor = lightBlue };
            option.Paint += delegate(object sender, PaintEventArgs e) {
                using (Pen p = new Pen(Color.FromArgb(54, 140, 230))) e.Graphics.DrawRectangle(p, 0, 0, option.Width - 1, option.Height - 1);
            };
            option.Controls.Add(new RadioButton { Left = 12, Top = 28, Width = 24, Height = 24, Checked = true, Enabled = false });
            option.Controls.Add(new PackageIcon { Left = 50, Top = 21, Width = 38, Height = 38, ForeColor = blue });
            option.Controls.Add(new Label { Text = "Microsoft Visual C++", Left = 100, Top = 14, Width = 220, Height = 27, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Color.FromArgb(28, 36, 48) });
            option.Controls.Add(new Label { Text = "2005 a 2025 • x86 e x64", Left = 100, Top = 42, Width = 220, Height = 24, ForeColor = muted });
            option.Controls.Add(new Panel { Left = 12, Top = 70, Width = 334, Height = 1, BackColor = border });
            ConfigureOptionalCheckBox(net35CheckBox, "Adicionar .NET Framework 3.5", 76);
            ConfigureOptionalCheckBox(net48CheckBox, "Adicionar .NET Framework 4.8", 101);
            ConfigureOptionalCheckBox(crystal2008CheckBox, "Adicionar Crystal Reports 2008 (x86)", 126);
            ConfigureOptionalCheckBox(windowsServerCheckBox, "Windows Server", 151);
            option.Controls.Add(net35CheckBox);
            option.Controls.Add(net48CheckBox);
            option.Controls.Add(crystal2008CheckBox);
            option.Controls.Add(windowsServerCheckBox);
            root.Controls.Add(option);

            root.Controls.Add(SectionLabel("Ordem dos pacotes", 44, 394, 340));
            packageList.Left = 42; packageList.Top = 422; packageList.Width = 358; packageList.Height = 208;
            packageList.View = View.Details; packageList.FullRowSelect = true; packageList.GridLines = true;
            packageList.HeaderStyle = ColumnHeaderStyle.Nonclickable; packageList.MultiSelect = false;
            packageList.Columns.Add("Componente", 100); packageList.Columns.Add("Versão", 95); packageList.Columns.Add("Arq.", 45); packageList.Columns.Add("Status", 95);
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
            logBox.Left = 420; logBox.Top = 322; logBox.Width = 560; logBox.Height = 308; logBox.ReadOnly = true;
            logBox.ScrollBars = RichTextBoxScrollBars.Vertical; logBox.Font = new Font("Consolas", 9.25F); logBox.BackColor = Color.White;
            logBox.BorderStyle = BorderStyle.FixedSingle; root.Controls.Add(logBox);

            copyLogButton.Text = "Copiar log"; copyLogButton.Left = 862; copyLogButton.Top = 286; copyLogButton.Width = 118; copyLogButton.Height = 30;
            copyLogButton.FlatStyle = FlatStyle.Flat; copyLogButton.FlatAppearance.BorderColor = border; copyLogButton.BackColor = Color.White;
            copyLogButton.Click += delegate { if (!String.IsNullOrWhiteSpace(logBox.Text)) Clipboard.SetText(logBox.Text); };
            root.Controls.Add(copyLogButton);
        }

        private void ConfigureOptionalCheckBox(CheckBox checkBox, string text, int top)
        {
            checkBox.Text = text;
            checkBox.Left = 18;
            checkBox.Top = top;
            checkBox.Width = 320;
            checkBox.Height = 23;
            checkBox.BackColor = lightBlue;
            checkBox.ForeColor = Color.FromArgb(38, 48, 64);
        }

        private void BuildFooter(Control root)
        {
            root.Controls.Add(new Panel { Left = 0, Top = 644, Width = 1024, Height = 1, BackColor = border });
            InfoCircle info = new InfoCircle { Left = 40, Top = 668, Width = 18, Height = 18, ForeColor = blue };
            root.Controls.Add(info);
            statusLabel.Text = "Pronto para iniciar"; statusLabel.Left = 62; statusLabel.Top = 670; statusLabel.Width = 300; statusLabel.Height = 24;
            root.Controls.Add(statusLabel);
            closeWhenDone.Text = "Fechar automaticamente ao finalizar"; closeWhenDone.Left = 364; closeWhenDone.Top = 664;
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
            button.Text = text; button.Left = left; button.Top = 652; button.Width = width; button.Height = 40;
            button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = primary ? Color.FromArgb(0, 76, 170) : border;
            button.BackColor = primary ? Color.FromArgb(0, 104, 210) : Color.White;
            button.ForeColor = primary ? Color.White : Color.FromArgb(38, 48, 64);
            if (primary) button.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        }

        private void PopulatePackageList()
        {
            packageList.Items.Clear();
            foreach (PackageItem p in displayItems)
            {
                ListViewItem row = new ListViewItem(p.Year);
                row.SubItems.Add(p.Version); row.SubItems.Add(p.Arch); row.SubItems.Add(p.Optional ? "Opcional" : "Pendente");
                p.Row = row; packageList.Items.Add(row);
            }
        }

        private void StartInstall()
        {
            if (worker != null && worker.IsBusy) return;
            if (MessageBox.Show("Os itens selecionados serão instalados com os pacotes Visual C++ em ordem de ano, sempre x86 antes de x64. Deseja continuar?",
                "Instalador Microsoft Visual C++", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            List<PackageItem> plan = new List<PackageItem>();
            if (net35CheckBox.Checked) plan.Add(net35Package);
            if (net48CheckBox.Checked) plan.Add(net48Package);
            plan.AddRange(packages);
            if (crystal2008CheckBox.Checked) plan.Add(crystal2008Package);
            if (windowsServerCheckBox.Checked) plan.Add(windowsServerPackage);

            cancelRequested = false; restartRequired = false; failures = 0; logBox.Clear(); SetProgress(0);
            foreach (PackageItem p in displayItems)
            {
                bool selected = plan.Contains(p);
                UpdateRow(p, selected ? "Pendente" : "Não selecionado", selected ? Color.White : Color.FromArgb(245, 245, 245));
            }
            cacheDir = Path.Combine(Path.GetTempPath(), "VisualCppInstaller_Cache");
            Directory.CreateDirectory(cacheDir);
            installButton.Enabled = false; closeButton.Enabled = false; cancelButton.Enabled = true; closeWhenDone.Enabled = false;
            SetOptionalControlsEnabled(false);
            statusLabel.Text = "Instalação em andamento";

            worker = new BackgroundWorker { WorkerReportsProgress = true };
            worker.DoWork += InstallWorker;
            worker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e) {
                SetProgress(e.ProgressPercentage); currentStepLabel.Text = Convert.ToString(e.UserState);
            };
            worker.RunWorkerCompleted += InstallCompleted;
            worker.RunWorkerAsync(plan);
        }

        private void InstallWorker(object sender, DoWorkEventArgs e)
        {
            List<PackageItem> plan = (List<PackageItem>)e.Argument;
            int completed = 0;
            int total = Math.Max(1, plan.Count);
            AppendLog("[INFO] Ordem: ano crescente, x86 e depois x64.");
            AppendLog("[INFO] Cache: " + cacheDir);
            AppendLog("[INFO] Sistema: " + (Environment.Is64BitOperatingSystem ? "64 bits" : "32 bits"));

            foreach (PackageItem p in plan)
            {
                if (cancelRequested) { e.Cancel = true; return; }
                if (p.Arch == "x64" && !Environment.Is64BitOperatingSystem)
                {
                    UpdateRow(p, "Ignorado", Color.FromArgb(245, 245, 245));
                    AppendLog("[IGNORADO] " + p.DisplayName + " — Windows 32 bits.");
                    completed++; worker.ReportProgress((completed * 100) / total, "Ignorado: " + p.DisplayName); continue;
                }

                try
                {
                    UpdateRow(p, "Instalando", Color.FromArgb(255, 249, 220));
                    worker.ReportProgress((completed * 100) / total, "Instalando " + p.DisplayName);
                    AppendLog("[INSTALANDO] " + p.DisplayName);

                    int code;
                    bool alreadyInstalled = false;

                    if (p.Kind == PackageKind.NetFx3 && IsNet35Installed())
                    {
                        code = 0;
                        alreadyInstalled = true;
                    }
                    else if (p.Kind == PackageKind.NetFx48 && IsNet48Installed())
                    {
                        code = 0;
                        alreadyInstalled = true;
                    }
                    else if (p.Kind == PackageKind.Msu && IsKb2999226Installed())
                    {
                        code = 0;
                        alreadyInstalled = true;
                    }
                    else if (p.Kind == PackageKind.NetFx3 && !RequiresStandaloneNet35())
                    {
                        code = RunProcessAndWait(new ProcessStartInfo("dism.exe", "/Online /Enable-Feature /FeatureName:NetFx3 /All /NoRestart")
                        { UseShellExecute = false, CreateNoWindow = true });
                    }
                    else
                    {
                        if (p.Kind == PackageKind.Msu) EnsureWindowsUpdateReady();
                        string local = LocateOrDownload(p);
                        if (cancelRequested) { e.Cancel = true; return; }
                        code = RunProcessAndWait(CreateInstallerProcess(p, local));
                    }

                    if (code == 3010 || code == 1641 || code == 2359301) restartRequired = true;
                    if (code == 2359302) alreadyInstalled = true;
                    if (code != 0 && code != 1638 && code != 3010 && code != 1641 && code != 2359301 && code != 2359302)
                        throw new InvalidOperationException("ExitCode " + code);
                    UpdateRow(p, alreadyInstalled || code == 1638 ? "Já instalado" : "Concluído", Color.FromArgb(232, 250, 238));
                    AppendLog("[OK] " + p.DisplayName + " — " + (alreadyInstalled ? "já estava instalado" : "ExitCode " + code));
                }
                catch (OperationCanceledException)
                {
                    e.Cancel = true;
                    return;
                }
                catch (Exception ex)
                {
                    failures++;
                    UpdateRow(p, "Falhou", Color.FromArgb(255, 232, 232));
                    AppendLog("[ERRO] " + p.DisplayName + " — " + ex.Message);
                }

                completed++;
                worker.ReportProgress((completed * 100) / total, "Processado: " + p.DisplayName);
            }
        }

        private string LocateOrDownload(PackageItem p)
        {
            string appPackages = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "packages");
            string bundled = Path.Combine(appPackages, p.FileName);
            if (IsPackageValid(p, bundled))
            {
                AppendLog("[LOCAL] " + p.FileName);
                return bundled;
            }

            string cached = Path.Combine(cacheDir, p.FileName);
            if (IsPackageValid(p, cached))
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
            if (!IsPackageValid(p, partial)) throw new InvalidDataException("O download não corresponde ao instalador esperado.");
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

        private static bool IsMsi(string path)
        {
            byte[] signature = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length < 1024) return false;
                using (FileStream stream = File.OpenRead(path))
                {
                    for (int i = 0; i < signature.Length; i++)
                        if (stream.ReadByte() != signature[i]) return false;
                }
                return true;
            }
            catch { return false; }
        }

        private static bool IsMsu(string path)
        {
            byte[] signature = new byte[] { 0x4D, 0x53, 0x43, 0x46 };
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length < 1024) return false;
                using (FileStream stream = File.OpenRead(path))
                {
                    for (int i = 0; i < signature.Length; i++)
                        if (stream.ReadByte() != signature[i]) return false;
                }
                return true;
            }
            catch { return false; }
        }

        private static bool IsPackageValid(PackageItem item, string path)
        {
            bool validType = item.Kind == PackageKind.Msi ? IsMsi(path) :
                item.Kind == PackageKind.Msu ? IsMsu(path) : IsExecutable(path);
            if (!validType) return false;
            if (String.IsNullOrWhiteSpace(item.ExpectedSha256)) return true;

            try
            {
                using (SHA256 sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(path))
                {
                    string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                    return String.Equals(actual, item.ExpectedSha256, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { return false; }
        }

        private ProcessStartInfo CreateInstallerProcess(PackageItem item, string localPath)
        {
            if (item.Kind == PackageKind.Msi)
            {
                return new ProcessStartInfo("msiexec.exe", "/i \"" + localPath + "\" /qn /norestart")
                { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(localPath) };
            }

            if (item.Kind == PackageKind.Msu)
            {
                return new ProcessStartInfo("wusa.exe", "\"" + localPath + "\" /quiet /norestart")
                { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(localPath) };
            }

            return new ProcessStartInfo(localPath, item.Arguments)
            { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(localPath) };
        }

        private int RunProcessAndWait(ProcessStartInfo startInfo)
        {
            using (Process process = Process.Start(startInfo))
            {
                runningProcess = process;
                while (!process.WaitForExit(300))
                {
                    if (cancelRequested)
                    {
                        TryKill(process);
                        throw new OperationCanceledException("Cancelado pelo usuário.");
                    }
                }
                runningProcess = null;
                return process.ExitCode;
            }
        }

        private static bool RequiresStandaloneNet35()
        {
            Version os = Environment.OSVersion.Version;
            return os.Major >= 10 && os.Build >= 28000;
        }

        private static bool IsNet35Installed()
        {
            return RegistryInstallFlagIsSet(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v3.5") ||
                RegistryInstallFlagIsSet(@"SOFTWARE\WOW6432Node\Microsoft\NET Framework Setup\NDP\v3.5");
        }

        private static bool IsNet48Installed()
        {
            string[] paths = new string[] {
                @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full",
                @"SOFTWARE\WOW6432Node\Microsoft\NET Framework Setup\NDP\v4\Full"
            };

            foreach (string path in paths)
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(path))
                    {
                        object release = key == null ? null : key.GetValue("Release");
                        if (release != null && Convert.ToInt32(release) >= 528040) return true;
                    }
                }
                catch { }
            }
            return false;
        }

        private static bool IsKb2999226Installed()
        {
            try
            {
                using (RegistryKey packagesKey = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\Packages"))
                {
                    if (packagesKey == null) return false;
                    foreach (string packageName in packagesKey.GetSubKeyNames())
                    {
                        if (packageName.IndexOf("KB2999226", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        using (RegistryKey packageKey = packagesKey.OpenSubKey(packageName))
                        {
                            object state = packageKey == null ? null : packageKey.GetValue("CurrentState");
                            if (state != null && Convert.ToInt32(state) == 112) return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private void EnsureWindowsUpdateReady()
        {
            if (IsWindowsUpdatePending())
                throw new InvalidOperationException("O Windows Update possui uma instalação em andamento ou reinicialização pendente. Conclua-a e reinicie o servidor antes de continuar.");

            AppendLog("[WINDOWS UPDATE] Habilitando o serviço Windows Update.");
            int configCode = RunProcessAndWait(new ProcessStartInfo("sc.exe", "config wuauserv start= demand")
            { UseShellExecute = false, CreateNoWindow = true });
            if (configCode != 0) throw new InvalidOperationException("Não foi possível habilitar o Windows Update. ExitCode " + configCode);

            int startCode = RunProcessAndWait(new ProcessStartInfo("sc.exe", "start wuauserv")
            { UseShellExecute = false, CreateNoWindow = true });
            if (startCode != 0 && startCode != 1056)
                throw new InvalidOperationException("Não foi possível iniciar o Windows Update. ExitCode " + startCode);

            if (IsWindowsUpdatePending())
                throw new InvalidOperationException("O Windows Update possui uma instalação em andamento ou reinicialização pendente. Conclua-a e reinicie o servidor antes de continuar.");
            AppendLog("[WINDOWS UPDATE] Serviço habilitado e sem reinicialização pendente.");
        }

        private static bool IsWindowsUpdatePending()
        {
            try { if (Process.GetProcessesByName("wusa").Length > 0) return true; }
            catch { }

            string[] keys = new string[] {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"
            };
            foreach (string path in keys)
            {
                try { using (RegistryKey key = Registry.LocalMachine.OpenSubKey(path)) { if (key != null) return true; } }
                catch { }
            }

            try
            {
                Type systemInfoType = Type.GetTypeFromProgID("Microsoft.Update.SystemInfo");
                if (systemInfoType != null)
                {
                    object systemInfo = Activator.CreateInstance(systemInfoType);
                    object value = systemInfoType.InvokeMember("RebootRequired", BindingFlags.GetProperty, null, systemInfo, null);
                    if (value != null && Convert.ToBoolean(value)) return true;
                }
            }
            catch { }

            try
            {
                Type sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
                if (sessionType != null)
                {
                    object session = Activator.CreateInstance(sessionType);
                    object installer = sessionType.InvokeMember("CreateUpdateInstaller", BindingFlags.InvokeMethod, null, session, null);
                    object busy = installer.GetType().InvokeMember("IsBusy", BindingFlags.GetProperty, null, installer, null);
                    if (busy != null && Convert.ToBoolean(busy)) return true;
                }
            }
            catch { }
            return false;
        }

        private static bool RegistryInstallFlagIsSet(string path)
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(path))
                {
                    object installed = key == null ? null : key.GetValue("Install");
                    return installed != null && Convert.ToInt32(installed) == 1;
                }
            }
            catch { return false; }
        }

        private void InstallCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            runningProcess = null; installButton.Enabled = true; closeButton.Enabled = true; cancelButton.Enabled = false; closeWhenDone.Enabled = true;
            SetOptionalControlsEnabled(true);
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

        private void SetOptionalControlsEnabled(bool enabled)
        {
            net35CheckBox.Enabled = enabled;
            net48CheckBox.Enabled = enabled;
            crystal2008CheckBox.Enabled = enabled;
            windowsServerCheckBox.Enabled = enabled;
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
        public readonly string DisplayName, ExpectedSha256;
        public readonly PackageKind Kind;
        public readonly bool Optional;
        public ListViewItem Row;

        public PackageItem(string year, string version, string arch, string fileName, string url, string arguments)
            : this(year, version, arch, fileName, url, arguments, "Visual C++ " + year + " " + arch,
                PackageKind.Executable, false, "")
        {
        }

        public PackageItem(string year, string version, string arch, string fileName, string url, string arguments,
            string displayName, PackageKind kind, bool optional, string expectedSha256)
        {
            Year = year;
            Version = version;
            Arch = arch;
            FileName = fileName;
            Url = url;
            Arguments = arguments;
            DisplayName = displayName;
            Kind = kind;
            Optional = optional;
            ExpectedSha256 = expectedSha256;
        }
    }

    internal enum PackageKind
    {
        Executable,
        Msi,
        Msu,
        NetFx3,
        NetFx48
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
