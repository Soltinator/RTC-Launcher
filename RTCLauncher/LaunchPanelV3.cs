namespace RTCV.Launcher
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Windows.Forms;
    using RTCV.Launcher.Components;

#pragma warning disable CA2213 //Component designer classes generate their own Dispose method
    internal partial class LaunchPanelV3 : Form, ILauncherJsonConfPanel
    {
        private readonly LauncherConfJson lc;
        private readonly Timer sidebarCloseTimer;
        private readonly List<Button> HiddenButtons = new List<Button>();

        public LaunchPanelV3()
        {
            InitializeComponent();
            lbSelectedVersion.Visible = false;

            lc = new LauncherConfJson(MainForm.SelectedVersion);

            sidebarCloseTimer = new Timer
            {
                Interval = 333
            };
            sidebarCloseTimer.Tick += SidebarCloseTimer_Tick;
        }

        public void DisplayVersion()
        {
            var folderPath = Path.Combine(MainForm.launcherDir, "VERSIONS", MainForm.SelectedVersion);
            if (!Directory.Exists(folderPath))
            {
                return;
            }

            Size? btnSize = null;
            HiddenButtons.Clear();

            foreach (var lcji in lc.Items) //.Where(it => !it.HideItem))
            {
                Bitmap btnImage;
                using (var bmpTemp = new Bitmap(new MemoryStream(File.ReadAllBytes(Path.Combine(lc.LauncherAssetLocation, lcji.ImageName)))))
                {
                    btnImage = new Bitmap(bmpTemp);
                    if (btnSize == null)
                    {
                        btnSize = new Size(btnImage.Width + 1, btnImage.Height + 1);
                    }
                }

                var newButton = new Button
                {
                    BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))))
                };
                newButton.FlatAppearance.BorderSize = 0;
                newButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
                newButton.Font = new System.Drawing.Font("Segoe UI Semibold", 8F, System.Drawing.FontStyle.Bold);
                newButton.ForeColor = System.Drawing.Color.Black;
                newButton.Name = lcji.FolderName;
                newButton.Size = (Size)btnSize;
                newButton.TabIndex = 134;
                newButton.TabStop = false;
                newButton.Tag = lcji;
                newButton.Text = string.Empty;
                newButton.UseVisualStyleBackColor = false;

                if (lcji.ImageName == "Add.png")
                {
                    newButton.AllowDrop = true;
                    newButton.MouseDown += AddButton_MouseDown;
                    newButton.DragEnter += AddButton_DragEnter;
                    newButton.DragDrop += AddButton_DragDrop;
                }
                else
                {
                    newButton.Click += this.btnBatchfile_Click;
                }

                newButton.MouseEnter += NewButton_MouseEnter;
                newButton.MouseLeave += NewButton_MouseLeave;

                var isAddon = !string.IsNullOrWhiteSpace(lcji.DownloadVersion);
                var AddonInstalled = false;

                if (isAddon)
                {
                    AddonInstalled = Directory.Exists(Path.Combine(lc.VersionLocation, lcji.FolderName));

                    newButton.MouseDown += (sender, e) =>
                    {
                        if (e.Button == MouseButtons.Right)
                        {
                            var locate = new Point(((Control)sender).Location.X + e.Location.X, ((Control)sender).Location.Y + e.Location.Y);

                            var columnsMenu = new Components.BuildContextMenu();

                            columnsMenu.Items.Add("Open Folder", null, (ob, ev) =>
                            {
                                var addonFolderPath = Path.Combine(MainForm.launcherDir, "VERSIONS", lc.Version, lcji.FolderName);

                                if (Directory.Exists(addonFolderPath))
                                {
                                    Process.Start(addonFolderPath);
                                }
                            }).Enabled = AddonInstalled;
                            columnsMenu.Items.Add(new ToolStripSeparator());
                            columnsMenu.Items.Add("Delete Addon", null, (ob, ev) => DeleteAddon(lcji)).Enabled = (lcji.IsAddon || AddonInstalled);

                            columnsMenu.Show(this, locate);
                        }
                    };
                }

                if (isAddon)
                {
                    var p = new Pen((AddonInstalled ? Color.FromArgb(57, 255, 20) : Color.Red), 1);
                    var b = new System.Drawing.SolidBrush((AddonInstalled ? Color.FromArgb(57, 255, 20) : Color.Red));

                    var x1 = 2;
                    var y1 = btnImage.Height - 6;
                    var x2 = 4;
                    var y2 = 4;
                    // Draw line to screen.
                    using (var graphics = Graphics.FromImage(btnImage))
                    {
                        graphics.FillRectangle(b, x1, y1, x2, y2);
                    }
                }

                newButton.Image = btnImage;

                if (!AddonInstalled && lcji.HideItem)
                {
                    newButton.Size = new Size(0, 0);
                    newButton.Location = new Point(0, 0);

                    HiddenButtons.Add(newButton);
                    continue;
                }

                newButton.Visible = true;
                flowLayoutPanel1.Controls.Add(newButton);
            }

            lbSelectedVersion.Text = lc.Version;
            lbSelectedVersion.Visible = true;
        }

        public void InstallCustomPackages()
        {
            var ofd = new OpenFileDialog
            {
                DefaultExt = "pkg",
                Title = "Open Package files",
                Filter = "PKG files|*.pkg",
                RestoreDirectory = true,
                Multiselect = true
            };

            string[] fileNames;
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                fileNames = ofd.FileNames;
            }
            else
            {
                return;
            }

            if (fileNames != null && fileNames.Length > 0)
            {
                InstallCustomPackages(fileNames);
            }
        }

        internal void InstallCustomPackages(string[] files)
        {
            if (files != null && files.Length > 0)
            {
                var nonPkg = files.Where(it => !it.ToUpper().EndsWith(".PKG")).ToList();
                if (nonPkg.Count > 0)
                {
                    MessageBox.Show("The custom package installer can only process PKG files. Aborting.");
                    return;
                }
            }

            if (files.Length == 0)
            {
                return;
            }
            else if (files.Length == 1 && MessageBox.Show("You are about to install a custom package in your RTC installation. Any changes done by the package will overwrite files in the installation.\n\nDo you wish to continue?", "Custom packge install", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
            {
                return;
            }
            else if (files.Length > 1 && MessageBox.Show("You are about to install multiple custom packages in your RTC installation. Any changes done by the packages will overwrite files in the installation.\n\nDo you wish to continue?", "Custom packge install", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
            {
                return;
            }

            var versionFolder = lc.VersionLocation;
            foreach (var file in files)
            {
                try
                {
                    using (ZipArchive archive = ZipFile.OpenRead(file))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            var entryPath = Path.Combine(versionFolder, entry.FullName).Replace("/", "\\");

                            if (entryPath.EndsWith("\\"))
                            {
                                if (!Directory.Exists(entryPath))
                                {
                                    Directory.CreateDirectory(entryPath);
                                }
                            }
                            else
                            {
                                entry.ExtractToFile(entryPath, true);
                            }
                        }
                    }

                    //System.IO.Compression.ZipFile.ExtractToDirectory(file, versionFolder,);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred during extraction and your RTC installation is possibly corrupted. \n\nYou may need to delete your RTC installation and reinstall it from the launcher. To do so, you can right click the version on the left side panel and select Delete from the menu.\n\nIf you need to backup any downloaded emulator to keep configurations or particular setups, you will find the content to backup by right clicking the card and selecting Open Folder.\n\n{ex}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    return;
                }

                MainForm.mf.RefreshPanel();
            }
        }

        private void AddButton_DragDrop(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;

            var fd = (string[])e.Data.GetData(DataFormats.FileDrop); //file drop

            InstallCustomPackages(fd);
        }

        private void AddButton_MouseDown(object sender, MouseEventArgs e)
        {
            //if (e.Button == MouseButtons.Right)
            //{
            var locate = new Point((sender as Control).Location.X + e.Location.X, (sender as Control).Location.Y + e.Location.Y);

            var columnsMenu = new BuildContextMenu();

            var allControls = new List<Control>();

            allControls.AddRange(flowLayoutPanel1.Controls.Cast<Control>());
            allControls.AddRange(HiddenButtons);

            foreach (var ctrl in allControls)
            {
                if (ctrl is Button btn)
                {
                    if (btn.Tag is LauncherConfJsonItem lcji && lcji.FolderName != null)
                    {
                        var AddonInstalled = Directory.Exists(Path.Combine(lc.VersionLocation, lcji.FolderName));

                        if (lcji.HideItem && !AddonInstalled)
                        {
                            columnsMenu.Items.Add(lcji.ItemName, null, new EventHandler((ob, ev) => btnBatchfile_Click(btn, e)));
                        }
                    }
                }
            }

            if (columnsMenu.Items.Count == 0)
            {
                columnsMenu.Items.Add("No available addons", null, new EventHandler((ob, ev) => { })).Enabled = false;
            }

            columnsMenu.Items.Add(new ToolStripSeparator());
            columnsMenu.Items.Add("Load Custom Package..", null, new EventHandler((ob, ev) => InstallCustomPackages()));

            var title = new ToolStripMenuItem("Extra addons for this RTC version")
            {
                Enabled = false
            };
            var sep = new ToolStripSeparator();

            columnsMenu.Items.Insert(0, sep);
            columnsMenu.Items.Insert(0, title);

            columnsMenu.Show(this, locate);
        }

        private void AddButton_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        private void SidebarCloseTimer_Tick(object sender, EventArgs e)
        {
            sidebarCloseTimer.Stop();

            MainForm.sideinfoForm.Hide();
            MainForm.sideversionForm.Show();
        }

        private void NewButton_MouseLeave(object sender, EventArgs e)
        {
            var currentButton = (Button)sender;
            var lcji = (LauncherConfJsonItem)currentButton.Tag;

            if (!string.IsNullOrWhiteSpace(lcji.ItemName))
            {
                sidebarCloseTimer.Stop();
                sidebarCloseTimer.Start();

                currentButton.FlatAppearance.BorderSize = 0;

                string name = (lcji.ItemName ?? string.Empty);
                string subtitle = (lcji.ItemSubtitle ?? string.Empty);

                if (name.Contains(subtitle))
                    name = name.Replace(subtitle, string.Empty);

                string description = (lcji.ItemDescription ?? string.Empty);

                MainForm.sideinfoForm.lbName.Text = name;
                MainForm.sideinfoForm.lbSubtitle.Text = subtitle;
                MainForm.sideinfoForm.lbDescription.Text = description;
            }
        }

        private void NewButton_MouseEnter(object sender, EventArgs e)
        {
            var currentButton = (Button)sender;
            var lcji = (LauncherConfJsonItem)currentButton.Tag;

            if (!string.IsNullOrWhiteSpace(lcji.ItemName))
            {
                sidebarCloseTimer.Stop();

                currentButton.FlatAppearance.BorderColor = Color.Gray;
                currentButton.FlatAppearance.BorderSize = 1;

                string name = (lcji.ItemName ?? string.Empty);
                string subtitle = (lcji.ItemSubtitle ?? string.Empty);

                if (name.Contains(subtitle))
                    name = name.Replace(subtitle, string.Empty);

                string description = (lcji.ItemDescription ?? string.Empty);

                MainForm.sideinfoForm.lbName.Text = name;
                MainForm.sideinfoForm.lbSubtitle.Text = subtitle;
                MainForm.sideinfoForm.lbDescription.Text = description;

                MainForm.sideinfoForm.Show();
                MainForm.sideversionForm.Hide();
            }
        }

        internal void DeleteAddon(LauncherConfJsonItem lcji)
        {
            var AddonFolderName = lcji.FolderName;
            var targetFolder = Path.Combine(MainForm.launcherDir, "VERSIONS", lc.Version, AddonFolderName);

            if (Directory.Exists(targetFolder))
            {
                string CustomPackage = null;

                if (lcji.IsAddon)
                {
                    CustomPackage = "This addon is a Custom Package\n\n";
                }

                if (MessageBox.Show(CustomPackage + "Deleting this addon will also wipe the configuration and temporary files that it contains.\n\nDo you want to continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                {
                    return;
                }
            }

            try
            {
                if (lcji.IsAddon)
                {
                    var ImageFilename = Path.Combine(MainForm.launcherDir, "VERSIONS", "Launcher", lcji.ImageName);

                    if (File.Exists(lcji.ConfigFilename))
                    {
                        File.Delete(lcji.ConfigFilename);
                    }

                    if (File.Exists(ImageFilename))
                    {
                        File.Delete(ImageFilename);
                    }
                }

                if (Directory.Exists(targetFolder))
                {
                    Directory.Delete(targetFolder, true);
                }
            }
            catch (Exception ex)
            {
                var result = MessageBox.Show($"Could not delete addon {AddonFolderName} because of the following error:\n{ex}", "Error", MessageBoxButtons.RetryCancel, MessageBoxIcon.Error);
                if (result == DialogResult.Retry)
                {
                    DeleteAddon(lcji);
                    return;
                }
            }

            MainForm.RefreshKeepSelectedVersion();
            //MainForm.mf.RefreshInterface();
        }

        private void NewLaunchPanel_Load(object sender, EventArgs e)
        {
            DisplayVersion();
        }

        private void btnBatchfile_Click(object sender, EventArgs e)
        {
            var currentButton = (Button)sender;

            var lcji = (LauncherConfJsonItem)currentButton.Tag;

            if (!string.IsNullOrEmpty(lcji.FolderName) && !Directory.Exists(Path.Combine(lc.VersionLocation, lcji.FolderName)))
            {
                LauncherConfJson lcCandidateForPull = getFolderFromPreviousVersion(lcji.DownloadVersion);
                if (lcCandidateForPull != null)
                {
                    var resultAskPull = MessageBox.Show($"The component {lcji.FolderName} could be imported from {lcCandidateForPull.Version}\nDo you wish import it?", "Import candidate found", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (resultAskPull == DialogResult.Yes)
                    {
                        LauncherConfJsonItem candidate = lcCandidateForPull.Items.FirstOrDefault(it => it.DownloadVersion == lcji.DownloadVersion);
                        //handle it here
                        try
                        {
                            RTC_Extensions.RecursiveCopyNukeReadOnly(new DirectoryInfo(Path.Combine(lcCandidateForPull.VersionLocation, candidate.FolderName)), new DirectoryInfo(Path.Combine(lc.VersionLocation, lcji.FolderName)));
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Couldn't copy {Path.Combine(lcCandidateForPull.VersionLocation, candidate?.FolderName ?? "NULL") ?? "NULL"} to {lcji.FolderName}.\nIs the file in use?\nException:{ex.Message}");
                            try
                            {
                                RTC_Extensions.RecursiveDeleteNukeReadOnly(new DirectoryInfo(Path.Combine(lc.VersionLocation, lcji.FolderName)));
                            }
                            catch (Exception _ex) //f
                            {
                                Console.WriteLine(_ex);
                            }
                            return;
                        }
                        try
                        {
                            RTC_Extensions.RecursiveDeleteNukeReadOnly(new DirectoryInfo(Path.Combine(lcCandidateForPull.VersionLocation, candidate.FolderName)));
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to delete old version {Path.Combine(lcCandidateForPull.VersionLocation, candidate?.FolderName ?? "NULL") ?? "NULL"}. Is the file in use?\nException:{ex.Message}");
                            return;
                        }
                        MainForm.RefreshKeepSelectedVersion();
                        return;
                    }
                }

                if (lcji.IsAddon)
                {
                    MessageBox.Show("This is a card for a missing Custom Package. You can reinstall the package with the PKG file or delete this addon.", "Missing folder", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var result = MessageBox.Show($"The following component is missing: {lcji.FolderName}\nDo you wish to download it?", "Additional download required", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    var downloadUrl = $"{MainForm.webResourceDomain}/rtc/addons/" + lcji.DownloadVersion + ".zip";
                    var downloadedFile = Path.Combine(MainForm.launcherDir, "PACKAGES", lcji.DownloadVersion + ".zip");
                    var extractDirectory = Path.Combine(lc.VersionLocation, lcji.FolderName);

                    MainForm.DownloadFile(new Uri(downloadUrl), downloadedFile, extractDirectory);
                }

                return;
            }

            lcji.Execute();
        }

        private static LauncherConfJson getFolderFromPreviousVersion(string downloadVersion)
        {
            foreach (var ver in MainForm.sideversionForm.lbVersions.Items.Cast<string>())
            {
                if (downloadVersion == ver)
                {
                    continue;
                }

                var _lc = new LauncherConfJson(ver);
                LauncherConfJsonItem lcji = _lc.Items.FirstOrDefault(it => it.DownloadVersion == downloadVersion);
                if (lcji != null)
                {
                    if (Directory.Exists(Path.Combine(_lc.VersionLocation, lcji.FolderName)))
                    {
                        return _lc;
                    }
                }
            }

            return null;
        }

        public LauncherConfJson GetLauncherJsonConf()
        {
            return lc;
        }
    }
}
