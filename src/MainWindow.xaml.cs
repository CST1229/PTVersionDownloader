using PTVersionDownloader.Structures;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PTVersionDownloader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public List<PTVersion> Versions { get; set; } = PTVersion.Versions;

        public MainWindow()
        {
            Global.LoadConfig();
            InitializeComponent();
            VersionGrid.ItemsSource = Versions;
        }

        private void DoSearch()
        {
            string searchString = SearchBar.Text.Trim();
            if (searchString == "")
            {
                Versions = PTVersion.Versions;
                VersionGrid.ItemsSource = Versions;
                return;
            }
            Versions = PTVersion.Versions.Where(v => v.DisplayName.Contains(searchString, StringComparison.InvariantCultureIgnoreCase)).ToList();
            VersionGrid.ItemsSource = Versions;
        }
        private T? GetContainerOfType<T>(FrameworkElement? container) where T : FrameworkElement
        {
            while (container is not null && container is not T)
            {
                container = container.Parent as FrameworkElement;
            }
            if (container is T returned) return returned;
            return null;
        }
        private void UpdateVersionContainer(FrameworkElement? container, PTVersion version)
        {
            container = GetContainerOfType<Border>(container);
            if (container is null) return;
            bool isInstalled = version.IsInstalled;
            if (container.FindName("OpenFolderButton") is MenuItem openFolder)
                openFolder.IsEnabled = isInstalled;
            if (container.FindName("DeleteButton") is MenuItem del)
                del.IsEnabled = isInstalled;
            if (container.FindName("RestoreButton") is MenuItem restore)
                restore.IsEnabled = isInstalled;
            if (container.FindName("DownloadOrPlayButton") is Button download)
                download.Content = isInstalled ? "Play" : "Download";

        }

        private void SearchBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyboardDevice.IsKeyDown(Key.Enter))
                DoSearch();
        }
        private void SearchBar_TextChanged(object sender, EventArgs e)
        {
            DoSearch();
        }
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            DoSearch();
        }

        private void Clear_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SearchBar.Clear();
            DoSearch();
        }

        private void DownloadOrPlay_Click(object sender, RoutedEventArgs e)
        {
            FrameworkElement? button = sender as FrameworkElement;
            if (button is null) return;
            PTVersion? version = button.DataContext as PTVersion;
            if (version is null) return;
            if (version.IsInstalled)
            {
                PlayVersion(version);
            }
            else
            {
                DownloadVersion(version, false);
            }
            UpdateVersionContainer(button, version);
        }
        private void PlayVersion(PTVersion version)
        {
            string exePath = $"{version.DownloadPath}PizzaTower.exe";
            if (!File.Exists(exePath))
            {
                MessageBox.Show("PizzaTower.exe not found in the version's folder. Try redownloading it in the \"...\" menu.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = false;
            startInfo.UseShellExecute = false;
            startInfo.FileName = $"{version.DownloadPath}PizzaTower.exe";
            startInfo.WindowStyle = ProcessWindowStyle.Normal;
            startInfo.WorkingDirectory = version.DownloadPath;
            startInfo.Arguments = DebugMode.IsChecked.GetValueOrDefault() ? "-debug" : "";
            using Process process = new Process();
            process.StartInfo = startInfo;
            process.Start();
        }
        private async void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            FrameworkElement? button = sender as FrameworkElement;
            if (button is null) return;
            PTVersion? version = button.DataContext as PTVersion;
            if (version is null) return;

            IsEnabled = false;
            string lastFile = "";
            try
            {
                await Task.Run(() =>
                {
                    foreach (string file in Directory.EnumerateFiles(version.DownloadPath, "*.po", new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                    }))
                    {
                        lastFile = file;
                        File.Move(file, file.Substring(0, file.Length - 3), true);
                    }
                });
            }
            catch (Exception err)
            {
                if (lastFile == "")
                {
                    MessageBox.Show("Encountered error:\n" + err.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show("Encountered error while trying to move " + lastFile + ":\n" + err.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            IsEnabled = true;
        }
        private void DownloadVersion_Click(object sender, RoutedEventArgs e)
        {
            FrameworkElement? button = sender as FrameworkElement;
            if (button is null) return;
            PTVersion? version = button.DataContext as PTVersion;
            if (version is null) return;
            DownloadVersion(version, true);
            UpdateVersionContainer(button, version);
        }
        private void DownloadVersion(PTVersion version, bool force)
        {
            string ptExe = $"{version.DownloadPath}PizzaTower.exe";
            string dataWin = $"{version.DownloadPath}data.win";
            if (force || !File.Exists(dataWin) || !File.Exists(ptExe))
            {
                if (!DownloadDepot(PTVersion.AppID, PTVersion.DepotID, version.ManifestID, version.DownloadPath)) return;

                // Disable Steam support so that launching the game doesn't run the original version
                try
                {
                    File.Move($"{version.DownloadPath}steam_api64.dll", $"{version.DownloadPath}_steam_api64.dll");
                }
                catch { }
            }
        }
        private void OpenVersionFolder_Click(object sender, RoutedEventArgs e)
        {
            FrameworkElement? button = sender as FrameworkElement;
            if (button is null) return;
            PTVersion? version = button.DataContext as PTVersion;
            if (version is null) return;
            if (!version.IsInstalled)
            {
                button.IsEnabled = false;
                UpdateVersionContainer(button, version);
                return;
            }
            Process process = Process.Start("explorer.exe", version.DownloadPath);
        }

        private bool DownloadDepot(string appID, string depotID, string manifestID, string outputDir)
        {
            if (Global.config.DownloadAutoMode)
            {
                if (VersionDownloadAuto.TryAutoDownload(appID, depotID, manifestID, outputDir, out bool downloadedDepot))
                {
                    return downloadedDepot;
                }
                return new VersionDownloadAuto(appID, depotID, manifestID, outputDir).ShowForDepot();
            }
            else
                return new VersionDownloadManual(appID, depotID, manifestID, outputDir).ShowForDepot();
        }

        private void VersionBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount > 1)
            {
                DownloadOrPlay_Click(sender, e);
            }
        }

        private void OpenVersionsFolder_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(PTVersion.VersionsFolder);
            Process process = Process.Start("explorer.exe", PTVersion.VersionsFolder);
        }
        private void DeleteVersion_Click(object sender, RoutedEventArgs e)
        {

        }

        private void VersionContextMenu_Click(object sender, RoutedEventArgs e)
        {
            Border? container = GetContainerOfType<Border>(sender as FrameworkElement);
            if (container?.ContextMenu is ContextMenu menu)
            {
                menu.IsOpen = true;
            }
        }

        private void VersionItem_Initialized(object sender, EventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is PTVersion version)
            {
                UpdateVersionContainer(el, version);
                el.ContextMenu.DataContext = el.DataContext;
            }
        }
    }
}