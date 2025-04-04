using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace PTVersionDownloader.Structures
{
    public class PTVersion
    {
        public static string VersionsFile => $"{Global.assemblyLocation}{Global.s}ptversions.json";
        public static string VersionsFolder => $"{Global.assemblyLocation}{Global.s}Versions{Global.s}";

        static List<PTVersion> ParsePTVersions()
        {
            string versionJSON;
            try
            {
                versionJSON = File.ReadAllText(VersionsFile);
            }
            catch (FileNotFoundException e)
            {
                MessageBox.Show($"""
                Version database not found here: {VersionsFile}
                Please make sure you extracted the whole zip file instead of just running the .exe file directly from it (which places it in a temporary directory).
                """, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new Exception("Could not read file: " + e);
            }
            catch (DirectoryNotFoundException e)
            {
                MessageBox.Show($"""
                Version database not found here: {VersionsFile}
                Please make sure you extracted the whole zip file instead of just running the .exe file directly from it (which places it in a temporary directory).
                """, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new Exception("Could not read file: " + e);
            }
            catch (UnauthorizedAccessException e)
            {
                MessageBox.Show($"""
                Could not get permission to read the version database here: {VersionsFile}
                Please make sure you have read and write permissions for the folder you extracted the program into (for example by moving it to a folder on the desktop).
                """, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new Exception("Could not read file: " + e);
            }
            catch (Exception e)
            {
                MessageBox.Show($"""
                Error when loading version database {VersionsFile}:
                {e}
                """, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new Exception("Could not read file: " + e);
            }

            try
            {
                return JsonSerializer.Deserialize<List<PTVersion>>(versionJSON) ?? new();
            }
            catch (Exception e)
            {
                MessageBox.Show($"""
                Could not parse version database. Maybe it got corrupted somehow? Try redownloading the program.
                {e}
                """, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new Exception("Could not parse file: " + e);
            }
        }
        public static List<PTVersion> Versions = ParsePTVersions();

        public static string AppID = "2231450";
        public static string DepotID = "2231451";


        [JsonPropertyName("manifestID")]
        public string ManifestID { get; set; } = "";


        [JsonIgnore]
        public string SteamDBDateString => $"SteamDB seen date: {SteamDBDate.ToString()}";
        [JsonIgnore]
        public DateTime SteamDBDate => DateTime.ParseExact(SteamDBDateRaw, "yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
        [JsonPropertyName("steamDBDate")]
        public string SteamDBDateRaw { get; set; } = "";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "";
        [JsonIgnore]
        public string DisplayName => Title == "" ? Version : Version + " (" + Title + ")";

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        public string DownloadPath => $"{VersionsFolder}{Version}{Global.s}";

        public bool IsInstalled { get {
                return Directory.Exists($"{Global.assemblyLocation}{Global.s}versions{Global.s}{Version}{Global.s}");
        } }
    }
}
