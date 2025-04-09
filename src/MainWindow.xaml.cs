using Microsoft.Win32;
using PTVersionDownloader.Structures;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;
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

        public FrameworkElement? LastMenuOpened = null;

        public MainWindow()
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(OnError);
            Global.LoadConfig();
            InitializeComponent();
            DebugMode.IsChecked = Global.config.DebugMode;
            VersionGrid.ItemsSource = Versions;
        }

        void OnError(object sender, UnhandledExceptionEventArgs ev)
        {
            MessageBox.Show($"Fatal error: {ev.ExceptionObject}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
            Application.Current.Shutdown();
        }

        private async Task DoSearch()
        {
            try
            {
                string searchString = SearchBar.Text.Trim();

                // secrets
                if (searchString.Equals("/patchidentifier", StringComparison.InvariantCultureIgnoreCase))
                {
                    searchString = "";
                    IsEnabled = false;
                    MessageBoxResult result = MessageBox.Show(
                        "Populate patchidentifier.json with installed versions? (This may take a really long time. You might also want to run /identifiersums after this.)",
                        "Secret Debug Command", MessageBoxButton.YesNo, MessageBoxImage.Question
                    );
                    if (result == MessageBoxResult.Yes)
                    {
                        ProgressWindow progress = new("Populating patch identifier...")
                        {
                            Owner = this
                        };
                        try
                        {
                            await Task.Run(() => PatchIdentifier.Populate(progress));
                        }
                        finally
                        {
                            progress.Close();
                        }
                        PatchIdentifier.Save();
                        MessageBox.Show("Done.", "Secret Debug Command");
                    }
                    IsEnabled = true;
                    return;
                }
                if (searchString.Equals("/identifiersums", StringComparison.InvariantCultureIgnoreCase))
                {
                    // just in case
                    PatchIdentifier.Files = PatchIdentifier.ParseData();
                    searchString = "";
                    IsEnabled = false;
                    MessageBoxResult result = MessageBox.Show(
                        "Populate patchidentifier.json with xdelta checksums? (This will require the min version of every identifiable file to be installed.)",
                        "Secret Debug Command", MessageBoxButton.YesNo, MessageBoxImage.Question
                    );
                    if (result == MessageBoxResult.Yes)
                    {
                        ProgressWindow progress = new("Populating patch identifier...")
                        {
                            Owner = this
                        };
                        try
                        {
                            await Task.Run(() => PatchIdentifier.PopulateSums(progress));
                        }
                        finally
                        {
                            progress.Close();
                        }
                        PatchIdentifier.Save();
                        MessageBox.Show("Done.", "Secret Debug Command");
                    }
                    IsEnabled = true;
                    return;
                }

                if (searchString == "")
                {
                    Versions = PTVersion.Versions;
                    VersionGrid.ItemsSource = Versions;
                    return;
                }

                Versions = [.. PTVersion.Versions.Where(v => v.DisplayName.Contains(searchString, StringComparison.InvariantCultureIgnoreCase))];
                VersionGrid.ItemsSource = Versions;
            }
            catch(Exception e)
            {
                MessageBox.Show($"Error: {e}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsEnabled = true;
            }
        }
        private static T? GetContainerOfType<T>(FrameworkElement? container) where T : FrameworkElement
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
            if (container is null) {
                // for context menus
                container = LastMenuOpened;
                if (container is null) return;
            }
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

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
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
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

        private void DownloadOrPlay_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement button) return;
            if (button.DataContext is not PTVersion version) return;
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
        private static void PlayVersion(PTVersion version)
        {
            string exePath = $"{version.DownloadPath}PizzaTower.exe";
            if (!File.Exists(exePath))
            {
                MessageBox.Show("PizzaTower.exe not found in the version's folder. Try redownloading it in the \"...\" menu.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            ProcessStartInfo startInfo = new()
            {
                CreateNoWindow = false,
                UseShellExecute = false,
                FileName = $"{version.DownloadPath}PizzaTower.exe",
                WindowStyle = ProcessWindowStyle.Normal,
                WorkingDirectory = version.DownloadPath,
                Arguments = Global.config.DebugMode ? "-debug" : ""
            };
            using Process process = new();
            process.StartInfo = startInfo;
            process.Start();
        }
        private async void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement button) return;
            if (button.DataContext is not PTVersion version) return;

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
                        File.Move(file, file[..^3], true);
                    }
                });
            }
            catch (Exception err)
            {
                if (lastFile == "")
                {
                    MessageBox.Show($"Encountered error:\n{err}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show($"Encountered error while trying to delete {lastFile}:\n{err}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            IsEnabled = true;
        }
        private void DownloadVersion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement button) return;
            if (button.DataContext is not PTVersion version) return;
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
            if (sender is not FrameworkElement button) return;
            if (button.DataContext is not PTVersion version) return;
            if (!version.IsInstalled)
            {
                button.IsEnabled = false;
                UpdateVersionContainer(button, version);
                return;
            }
            Process.Start("explorer.exe", version.DownloadPath);
        }

        private bool DownloadDepot(string appID, string depotID, string manifestID, string outputDir)
        {
            if (Global.config.DownloadAutoMode)
            {
                if (VersionDownloadAuto.TryAutoDownload(appID, depotID, manifestID, outputDir, out bool downloadedDepot))
                {
                    return downloadedDepot;
                }
                return new VersionDownloadAuto(appID, depotID, manifestID, outputDir).ShowForDepot(this);
            }
            else
                return new VersionDownloadManual(appID, depotID, manifestID, outputDir).ShowForDepot(this);
        }

        private void VersionBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount > 1)
            {
                DownloadOrPlay_Click(sender, e);
            }
        }

        private void IdentifyPatch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog dialog = new()
                {
                    FileName = "patch.xdelta",
                    DefaultExt = ".xdelta",
                    Filter = "xdelta patches (*.xdelta)|*.xdelta|All files (*.*)|*",
                };
                bool? result = dialog.ShowDialog();
                if (!result.GetValueOrDefault()) return;
                Tuple<string, PTVersion> message = PatchIdentifier.Identify(dialog.FileName);
                if (Versions.Contains(message.Item2)) {
                    VersionGrid.SelectedValue = message.Item2;
                }
                MessageBox.Show(message.Item1, "Result", MessageBoxButton.OK);
            }
            catch(Exception ex)
            {
                MessageBox.Show($"Could not identify patch: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenVersionsFolder_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(PTVersion.VersionsFolder);
            Process.Start("explorer.exe", PTVersion.VersionsFolder);
        }
        private void DeleteVersion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement button) return;
            if (button.DataContext is not PTVersion version) return;

            MessageBoxResult result = MessageBox.Show(
                $"Are you sure you want to delete the game files for {version.Version}? (You can always redownload it, and save data will be unaffected.)",
                "Deleting Game Files", MessageBoxButton.YesNo, MessageBoxImage.Exclamation
            );
            if (result != MessageBoxResult.Yes) return;
            try
            {
                Directory.Delete(version.DownloadPath, true);
            }
            catch (Exception err)
            {
                MessageBox.Show($"Encountered error while trying to delete {version.Version}:\n{err}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            UpdateVersionContainer(button, version);
        }

        private void CopyManifestIDButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement button) return;
            if (button.DataContext is not PTVersion version) return;
            Clipboard.SetText(version.ManifestID);
        }

        private void VersionContextMenu_Click(object sender, RoutedEventArgs e)
        {
            Border? container = GetContainerOfType<Border>(sender as FrameworkElement);
            if (container?.ContextMenu is ContextMenu menu)
            {
                LastMenuOpened = container;
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

        private void DebugMode_Checked(object sender, RoutedEventArgs e)
        {
            Global.config.DebugMode = DebugMode.IsChecked.GetValueOrDefault();
            Global.SaveConfig();
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            var window = new AboutWindow
            {
                Owner = this
            };
            window.ShowDialog();
        }
    }
}