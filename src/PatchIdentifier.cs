﻿using PTVersionDownloader.Structures;
using System.Diagnostics;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace PTVersionDownloader
{
    public partial class PatchIdentifier
    {
        public static string XDeltaPath => $"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}xdelta.exe";
        public static string DataFile => $"{Global.assemblyLocation}{Global.s}patchidentifier.json";

        public static List<FileData> ParseData()
        {
            string dataJSON;
            try
            {
                dataJSON = File.ReadAllText(DataFile);
            }
            catch (FileNotFoundException e)
            {
                MessageBox.Show($"""
                Patch identifier database not found here: {DataFile}
                Please make sure you extracted the whole zip file instead of just running the .exe file directly from it (which places it in a temporary directory).
                """, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new Exception("Could not read file: " + e);
            }
            catch (DirectoryNotFoundException e)
            {
                MessageBox.Show($"""
                Patch identifier database not found here: {DataFile}
                Please make sure you extracted the whole zip file instead of just running the .exe file directly from it (which places it in a temporary directory).
                """, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new Exception("Could not read file: " + e);
            }
            catch (UnauthorizedAccessException e)
            {
                MessageBox.Show($"""
                Could not get permission to read the patch identifier database here: {DataFile}
                Please make sure you have read and write permissions for the folder you extracted the program into (for example by moving it to a folder on the desktop).
                """, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new Exception("Could not read file: " + e);
            }
            catch (Exception e)
            {
                MessageBox.Show($"""
                Error when loading patch identifier database {DataFile}:
                {e}
                """, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new Exception("Could not read file: " + e);
            }

            try
            {
                return JsonSerializer.Deserialize<List<FileData>>(dataJSON) ?? [];
            }
            catch (Exception e)
            {
                MessageBox.Show($"""
                Could not parse patch identifier database. Maybe it got corrupted somehow? Try redownloading the program.
                {e}
                """, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new Exception("Could not parse file: " + e);
            }
        }
        public static List<FileData> Files = ParseData();
        public class FileData
        {
            public string Name { get; set; } = "";
            public string MinVersion { get; set; } = "";
            public string MaxVersion { get; set; } = "";
            public long Size { get; set; } = 0;
            // this is actually used for the copy window length. any unique-ish part of an xdelta patch, basically
            public string FirstChecksum { get; set; } = "";
            public string MD5 { get; set; } = "";
        }

        private static readonly string[] CheckFileNames = [
            "PizzaTower.exe",
            "data.win",
            "sound/Desktop/Master.bank",
            "sound/Desktop/Master.strings.bank",
            "sound/Desktop/music.bank",
            "sound/Desktop/sfx.bank",
        ];

        public static void Populate(ProgressWindow? progress)
        {
            Action incrementer = () => { };
            if (progress is not null)
            {
                progress.Dispatcher.Invoke(() =>
                {
                    progress.Show();
                    progress.SetMax(PTVersion.Versions.Count * CheckFileNames.Length);
                });
                incrementer = () => {
                    progress.Dispatcher.Invoke(progress.Increment);
                };
            }
            Parallel.ForEach(PTVersion.Versions, ver => PopulateVersion(ver, incrementer));
        }
        private static void PopulateVersion(PTVersion version, Action incrementer)
        {
            if (!version.IsInstalled) return;
            Parallel.ForEach(CheckFileNames, fileName => PopulateFile(version, fileName, incrementer));
        }
        private static void PopulateFile(PTVersion version, string fileName, Action incrementer)
        {
            string filePath = Path.Join(version.DownloadPath, fileName);
            string hash = GetMD5(filePath);
            long fileSize = new FileInfo(filePath).Length;

            lock (Files)
            {
                FileData? existingFile = Files.FirstOrDefault(o => (o is not null && o.MD5 == hash), null);

                FileData file = existingFile is not null ? existingFile : new FileData();
                file.Name = fileName;
                file.MinVersion = ComparedVersion(file.MinVersion, version.Version);
                file.MaxVersion = ComparedVersion(file.MaxVersion, version.Version, true);
                file.Size = fileSize;
                file.MD5 = hash;
                if (existingFile is null) Files.Add(file);

                if (incrementer is not null) incrementer();
            }
        }

        public static PTVersion? GetVersionFromString(string version)
        {
            return PTVersion.Versions.FirstOrDefault(v => v is not null && v.Version == version, null);
        }
        public static DateTime? GetVersionSteamDBDate(string version)
        {
            var ver = GetVersionFromString(version);
            if (ver is null) return null;
            return ver.SteamDBDate;
        }
        public static string ComparedVersion(string a, string b, bool max = false)
        {
            var dateA = GetVersionSteamDBDate(a);
            var dateB = GetVersionSteamDBDate(b);
            if (dateA is null && dateB is not null) return b;
            if (dateB is null) return a;
            if (max)
            {
                return dateA > dateB ? a : b;
            }
            else
            {
                return dateA < dateB ? a : b;
            }
        }
        public static string MinVersion(string a, string b)
        {
            if (a == "" && b != "") return b;
            return b.CompareTo(a) < 0 ? b : a;
        }


        private static string GetMD5(string file)
        {
            using MD5 hashAlgo = MD5.Create();
            using var fileStream = new BufferedStream(File.OpenRead(file), 10 * 1000000);
            fileStream.Position = 0;
            return BytesToHex(hashAlgo.ComputeHash(fileStream));
        }
        private static string BytesToHex(byte[] data)
        {
            // https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.hashalgorithm.computehash?view=net-9.0
            var sBuilder = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }
            return sBuilder.ToString();
        }


        public static void PopulateSums(ProgressWindow? progress)
        {
            Action incrementer = () => { };
            if (progress is not null)
            {
                progress.Dispatcher.Invoke(() =>
                {
                    progress.Show();
                    progress.SetMax(Files.Count);
                });
                incrementer = () => {
                    progress.Dispatcher.Invoke(progress.Increment);
                };
            }
            Parallel.ForEach(Files, (file) =>
            {
                file.FirstChecksum = GetAdlerChunkSum(Path.Join(PTVersion.VersionsFolder, file.MinVersion, file.Name));
                incrementer();
            });
        }

        public readonly static Regex SumRegex = SumRegexInternal();

        [GeneratedRegex("VCDIFF copy window length:\\s+(\\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
        private static partial Regex SumRegexInternal();

        public static string GetAdlerChunkSum(string fileName)
        {
            if (!File.Exists(fileName)) return "";

            ProcessStartInfo createInfo = new()
            {
                CreateNoWindow = true,
                FileName = XDeltaPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.Latin1,
                WorkingDirectory = Path.GetDirectoryName(fileName),
                ArgumentList = {
                    "-c", "-e", "-s", fileName, fileName,
                },
            };
            ProcessStartInfo printInfo = new()
            {
                CreateNoWindow = true,
                FileName = XDeltaPath,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                StandardInputEncoding = Encoding.Latin1,
                WorkingDirectory = Path.GetDirectoryName(fileName),
                ArgumentList = {
                    "printhdr", "-c", "-v", "-v",
                },
            };

            using Process create = new();
            create.StartInfo = createInfo;
            create.Start();
            string deltaOutput = create.StandardOutput.ReadToEnd();
            create.WaitForExit();
            if (create.ExitCode != 0)
            {
                throw new Exception($"Patch creation exited with code {create.ExitCode}. Error output:\n{create.StandardOutput.ReadToEnd()}");
            }

            using Process print = new();
            print.StartInfo = printInfo;
            print.Start();
            print.StandardInput.Write(deltaOutput);
            print.StandardInput.Close();
            string output = print.StandardOutput.ReadToEnd();
            print.WaitForExit();
            if (print.ExitCode != 0)
            {
                throw new Exception($"Patch analysis exited with code {print.ExitCode}. Error output:\n{print.StandardOutput.ReadToEnd()}");
            }

            var sumResult = SumRegex.Match(output);
            if (sumResult.Success) return sumResult.Groups[1].Value;
            return "";
        }

        public static Tuple<string, PTVersion> Identify(string fileName)
        {
            ProcessStartInfo printInfo = new()
            {
                CreateNoWindow = true,
                FileName = XDeltaPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(fileName),
                ArgumentList = {
                    "printhdr", "-v", "-v", fileName,
                },
            };

            using Process print = new();
            print.StartInfo = printInfo;
            print.Start();
            string output = print.StandardOutput.ReadToEnd();
            print.WaitForExit();
            if (print.ExitCode != 0)
            {
                throw new Exception($"Patch analysis exited with code {print.ExitCode}. Error output:\n{print.StandardOutput.ReadToEnd()}");
            }

            var sumResult = SumRegex.Match(output);
            string sum = "not found";
            if (sumResult.Success)
            {
                sum = sumResult.Groups[1].Value;
                var file = Files.FirstOrDefault(v => v is not null && v.FirstChecksum == sum, null);
                if (file is not null)
                {
                    if (GetVersionFromString(file.MaxVersion) is PTVersion ver)
                    {
                        return new Tuple<string, PTVersion>(MessageFor(file), ver);
                    }
                    throw new Exception($"Checksum and file found, but no matching version. Sum:\n{sum}\nMax ver:{file.MaxVersion}");
                }
                throw new Exception($"Checksum found, but no matching file. Output:\n{output}");
            }

            throw new Exception($"No checksum found. Output:\n{output}");
        }

        private static string MessageFor(FileData file)
        {
            if (file.MinVersion == file.MaxVersion) {
                return $"This patch seems to be made for {file.MinVersion} {file.Name}";
            }
            return $"This patch seems to be made for {file.MinVersion}-{file.MaxVersion} {file.Name}";
        }


        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
        public static void Save()
        {
            string str = JsonSerializer.Serialize(Files, SerializerOptions);
            try
            {
                File.WriteAllText(DataFile, str);
            }
            catch (Exception e)
            {
                MessageBox.Show($"Couldn't save patchidentifier.json: {e}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
