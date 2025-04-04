using Microsoft.Win32;
using PTVersionDownloader.Structures;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
namespace PTVersionDownloader
{
    /// <summary>
    /// Interaction logic for VersionDownloadManual.xaml
    /// </summary>
    public partial class VersionDownloadManual : Window
    {
        public string AppID { get; set; } = "";
        public string DepotID { get; set; } = "";
        public string ManifestID { get; set; } = "";

        public string DepotPath { get; set; } = "";
        public string OutputDir { get; set; } = "";
        public bool DownloadedDepot = false;

        public VersionDownloadManual(string appID, string depotID, string manifestID, string outputDir)
        {
            AppID = appID;
            DepotID = depotID;
            ManifestID = manifestID;
            OutputDir = outputDir;

            DepotPath = GetDepotPath();
            InitializeComponent();
            DownloadCommand.Text = $"download_depot {AppID} {DepotID} {ManifestID}";
            if (DepotPath != "")
            {
                NoPathWarning.Visibility = Visibility.Collapsed;
            }
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(DownloadCommand.Text);
        }

        private void OpenSteam_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("steam://open/console") { UseShellExecute = true });
        }

        private string GetDepotPath() {
            var key = Registry.CurrentUser.OpenSubKey($@"SOFTWARE\Valve\Steam");
            if (key != null)
                if (!String.IsNullOrEmpty(key.GetValue("SteamPath") as string))
                    return Path.GetFullPath($"{key.GetValue("SteamPath") as string}{Global.s}steamapps{Global.s}content{Global.s}app_{AppID}{Global.s}depot_{DepotID}\\");
            return "";
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DownloadedDepot = false;
            DepotPath = "";
            Close();
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            Close();
            if (PerformFinalMove(DepotPath)) return;
            OpenFolderDialog dialog = new OpenFolderDialog();
            dialog.Title = "Select the path to the downloaded depot";
            if (dialog.ShowDialog() != true) {
                DownloadedDepot = false;
                MessageBox.Show("Did not select the depot path; version will not be added.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            };
            string folder = dialog.FolderName;
            if (PerformFinalMove(folder)) return;
            MessageBox.Show("You somehow selected a folder that doesn't exist???", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private bool PerformFinalMove(string folder)
        {
            if (Directory.Exists(folder))
            {
                Directory.CreateDirectory(PTVersion.VersionsFolder);
                try
                {
                    MoveDirectory(folder, OutputDir, true);
                    DownloadedDepot = true;
                } catch (Exception ex)
                {
                    MessageBox.Show($"Could not move the folder to the downloader's folder: {ex}\n" +
                        "Try using the Automatic mode or running the downloader in administrator mode.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return true;
            }
            return false;
        }

        // https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories
        // (modified)
        static void MoveDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.MoveTo(targetFilePath, true);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    MoveDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
            dir.Delete(true);
        }

        private void AutoMode_Click(object sender, RoutedEventArgs e)
        {
            Close();
            Global.config.DownloadAutoMode = true;
            Global.SaveConfig();
            if (VersionDownloadAuto.TryAutoDownload(AppID, DepotID, ManifestID, OutputDir, out DownloadedDepot)) return;
            DownloadedDepot = new VersionDownloadAuto(AppID, DepotID, ManifestID, OutputDir).ShowForDepot(Owner);
        }

        public bool ShowForDepot(Window owner) {
            Owner = owner;
            ShowDialog();
            return DownloadedDepot;
        }
    }
}
