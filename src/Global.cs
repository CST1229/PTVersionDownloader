using System.IO;
using System.Text.Json;
using System.Windows;

namespace PTVersionDownloader
{
    public class Global
    {
        public static char s = Path.DirectorySeparatorChar;
        public static string assemblyLocation = AppDomain.CurrentDomain.BaseDirectory.Substring(0, AppDomain.CurrentDomain.BaseDirectory.Length - 1);
        public static string ConfigPath = $@"{assemblyLocation}{s}Config.json";

        public static Config config = new();
        public static void SaveConfig()
        {
            string configString = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            try
            {
                File.WriteAllText(ConfigPath, configString);
            }
            catch (Exception e)
            {
                MessageBox.Show($"Couldn't write Config.json: {e}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public static void LoadConfig()
        {
            try
            {
                string configString = File.ReadAllText(ConfigPath);
                config = JsonSerializer.Deserialize<Config>(configString) ?? new();
            } catch(Exception e)
            {
                if (e is not FileNotFoundException)
                    MessageBox.Show($"Couldn't load Config.json. Using default config: {e}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                config = new();
            }
        }
    }

    public class Config
    {
        public bool DownloadRememberPassword { get; set; } = true;
        public bool DownloadAutoMode { get; set; } = false;
        public bool DebugMode { get; set; } = false;
    }
}
