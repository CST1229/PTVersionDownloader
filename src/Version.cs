using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PTVersionDownloader.Structures
{
    public class PTVersion
    {
        public static string VersionsFile => $"{Global.assemblyLocation}{Global.s}ptversions.json";
        public static string VersionsFolder => $"{Global.assemblyLocation}{Global.s}Versions{Global.s}";

        static List<PTVersion> ParsePTVersions()
        {
            string versionJSON = File.ReadAllText(VersionsFile);

            return JsonSerializer.Deserialize<List<PTVersion>>(versionJSON) ?? new();
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
