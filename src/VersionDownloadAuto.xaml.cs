using Microsoft.Win32;
using PTVersionDownloader.Structures;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace PTVersionDownloader
{
    /// <summary>
    /// Interaction logic for VersionDownloadAuto.xaml
    /// </summary>
    public partial class VersionDownloadAuto : Window
    {
        public string AppID { get; set; }
        public string DepotID { get; set; }
        public string ManifestID { get; set; }

        public string OutputDir { get; set; }
        public bool DownloadedDepot = false;

        public VersionDownloadAuto(string appID, string depotID, string manifestID, string outputDir)
        {
            AppID = appID;
            DepotID = depotID;
            ManifestID = manifestID;
            OutputDir = outputDir;
            InitializeComponent();
            RememberPassword.IsChecked = Global.config.DownloadRememberPassword;

            UsernameInput.Text = GetSteamUsername();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Download_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameInput.Text.Trim().Replace("\"", "^\"").Replace("&", "^&");

            if (username == "")
            {
                MessageBox.Show("You must enter your Steam username!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            Close();

            DownloadedDepot = DoDownload(RememberPassword.IsChecked.GetValueOrDefault(), AppID, DepotID, ManifestID, username, OutputDir);
        }

        private static bool DoDownload(bool rememberPassword, string appID, string depotID, string manifestID, string username, string outputDir)
        {
            string depotdownloader = $"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}DepotDownloader.exe";
            if (!File.Exists(depotdownloader))
            {
                MessageBox.Show($"{depotdownloader} was not found.\nPlease try redownloading the program.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            string rememberPasswordArg = rememberPassword ? "-remember-password " : "";

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = false;
            startInfo.UseShellExecute = true;
            startInfo.FileName = depotdownloader;
            startInfo.WindowStyle = ProcessWindowStyle.Normal;
            startInfo.WorkingDirectory = Global.assemblyLocation;
            startInfo.Arguments = $@"-app {appID} -depot {depotID} -manifest {manifestID} {rememberPasswordArg}-username ""{username}"" -dir {outputDir}";
            using Process process = new Process();
            process.StartInfo = startInfo;

            process.Start();
            process.WaitForExit();

            try
            {
                Directory.Delete($"{outputDir}.DepotDownloader{Global.s}", true);
            }
            catch { }

            if (process.ExitCode != 0)
            {
                MessageBox.Show($"Could not download the depot.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        private void ManualMode_Click(object sender, RoutedEventArgs e)
        {
            Close();
            Global.config.DownloadAutoMode = false;
            Global.SaveConfig();
            DownloadedDepot = new VersionDownloadManual(AppID, DepotID, ManifestID, OutputDir).ShowForDepot();
        }
        public bool ShowForDepot()
        {
            ShowDialog();
            return DownloadedDepot;
        }

        public static bool TryAutoDownload(string appID, string depotID, string manifestID, string outputDir, out bool downloadedDepot)
        {
            downloadedDepot = false;
            if (!(Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))) return false;
            string username = GetSteamUsername();
            if (username == "") return false;
            downloadedDepot = DoDownload(Global.config.DownloadRememberPassword, PTVersion.AppID, PTVersion.DepotID, manifestID, username, outputDir);
            return true;
        }

        private static string GetSteamUsername()
        {
            var key = Registry.CurrentUser.OpenSubKey($@"SOFTWARE\Valve\Steam");
            if (key != null)
                if (!String.IsNullOrEmpty(key.GetValue("AutoLoginUser") as string))
                    return (string)key.GetValue("AutoLoginUser", "") ?? "";
            return "";
        }

        private void RememberPassword_Checked(object sender, RoutedEventArgs e)
        {
            Global.config.DownloadRememberPassword = RememberPassword.IsChecked.GetValueOrDefault();
            Global.SaveConfig();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.ToString()) { UseShellExecute = true });
        }
    }
}
