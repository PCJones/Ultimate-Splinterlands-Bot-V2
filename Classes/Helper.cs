using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ultimate_Splinterlands_Bot_V2.Classes
{
    public static class Helper
    {
        public static string GenerateMD5Hash(string input)
        {
            // Use input string to calculate MD5 hash
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Convert the byte array to hexadecimal string
                StringBuilder sb = new();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }
        public static string GenerateRandomString(int n)
        {
            char[] buf = new char[n];
            for (int i = 0; i < buf.Length; i++)
            {
                int index = Settings._Random.Next(Settings.Subset.Length);
                buf[i] = Settings.Subset[index];
            }

            return new string(buf);
        }
        public async static Task<string> DownloadPageAsync(string url)
        {
            // Use static HttpClient to avoid exhausting system resources for network connections.
            var result = await Settings._httpClient.GetAsync(url);
            var response = await result.Content.ReadAsStringAsync();
            // Write status code.
            return response;
        }

        public static string DoQuickRegex(string Pattern, string Match)
        {
            Regex r = new(Pattern, RegexOptions.Singleline);
            return r.Match(Match).Groups[1].Value;
        }

        public static bool RunProcessWithResult(string file, string args)
        {
            Log.WriteToLog("PowerTransferDebug: Run Process: " + file);
            Log.WriteToLog("PowerTransferDebug: Args: " + args);
            System.Diagnostics.Process process = new();
            process.StartInfo.FileName = file;
            process.StartInfo.Arguments = args;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            //process.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            process.Start();

            string transferBotLog = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            Log.WriteToLog(transferBotLog.Trim());
            return process.ExitCode == 0;
        }
        public static void RunProcess(string file, string args)
        {
            System.Diagnostics.Process process = new();
            process.StartInfo.FileName = file;
            process.StartInfo.Arguments = args;
            process.StartInfo.UseShellExecute = true;
            //process.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            process.Start();
        }

        public static void CheckForUpdate()
        {
            try
            {
                string versionFilePath = Settings.StartupPath + "/config/version.usb";
                if (!File.Exists(versionFilePath))
                {
                    Log.WriteToLog("Automatic update check is not possible because the version.usb file is missing from config folder!", Log.LogType.Error);
                    Log.WriteToLog("Automatic update check is not possible because the version.usb file is missing from config folder!", Log.LogType.Error);
                    Log.WriteToLog("Automatic update check is not possible because the version.usb file is missing from config folder!", Log.LogType.Error);
                    return;
                }
                WebClient webClient = new();
                string gitHubUrl = $"https://api.github.com/repos/{Settings.BOT_GITHUB_REPO}/releases";
                string releasesRaw = DownloadPageAsync(gitHubUrl).Result;
                JToken newestRelease = JArray.Parse(releasesRaw)[0];
                string[] localVersion = File.ReadAllLines(versionFilePath);
                DateTime currentVersionPublishDate = DateTime.Parse(localVersion[1]);
                DateTime releasePublishDate = (DateTime)newestRelease["published_at"];
                if (releasePublishDate > currentVersionPublishDate)
                {
                    if (Settings.AutoUpdate)
                    {
                        Log.WriteToLog("New bot update available!");
                        Log.WriteToLog("New bot update available!");
                        Log.WriteToLog("New bot update available!");
                        Log.WriteToLog("Press any key to start the update...");
                        Console.ReadKey();
                        UpdateViaArchive(gitHubUrl);
                        string fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Ultimate Splinterlands Bot V2.exe" : "Ultimate Splinterlands Bot V2";
                        string tempFileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "tempUltimate Splinterlands Bot V2.exe" : "tempUltimate Splinterlands Bot V2";
                        string tempFileName2 = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "temp2Ultimate Splinterlands Bot V2.exe" : "temp2Ultimate Splinterlands Bot V2";
                        RunProcess(Settings.StartupPath + "/" + fileName, "update " + fileName + " " + tempFileName + " " + tempFileName2);
                        Environment.Exit(0);
                    }
                    else
                    {
                        Log.WriteToLog("New bot update available! Please download at https://github.com/PCJones/Ultimate-Splinterlands-Bot-V2/releases", Log.LogType.Warning);
                        Log.WriteToLog("New bot update available! Please download at https://github.com/PCJones/Ultimate-Splinterlands-Bot-V2/releases", Log.LogType.Warning);
                        Log.WriteToLog("New bot update available! Please download at https://github.com/PCJones/Ultimate-Splinterlands-Bot-V2/releases", Log.LogType.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error at checking for update: " + ex.Message);
            }
        }

        public static void KillInstances()
        {
            foreach (var process in System.Diagnostics.Process.GetProcessesByName("Ultimate Splinterlands Bot V2"))
            {
                try
                {
                    System.Threading.Thread.Sleep(2000);
                    Console.WriteLine("Killing running bot process...");
                }
                catch (Exception)
                {
                    process.Kill();
                }
            }
        }

        public static void UpdateViaArchive(string downloadUrl)
        {
            Console.WriteLine("Beginning update.");
            Console.WriteLine("Downloading archive...");
            string downloadDestination = Path.GetTempFileName();
            WebClient downloadifier = new WebClient();
            downloadifier.DownloadFile(downloadUrl, downloadDestination);
            Console.WriteLine("Downloading finished.");
            Console.Write("Extracting archive... ");
            string extractTarget = Settings.StartupPath;

            ZipArchive archive = ZipFile.Open(downloadDestination, ZipArchiveMode.Read);
            foreach (ZipArchiveEntry file in archive.Entries)
            {
                string completeFileName;
                if (file.FullName.StartsWith("Ultimate Splinterlands Bot V2"))
                {
                    completeFileName = Path.Combine(extractTarget, "temp2" + file.FullName);
                    file.ExtractToFile(completeFileName, true);
                    completeFileName = Path.Combine(extractTarget, "temp" + file.FullName);
                }
                else
                {
                    completeFileName = Path.Combine(extractTarget, file.FullName);
                }
                if (!Directory.Exists(Path.GetDirectoryName(completeFileName)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(completeFileName));
                }

                file.ExtractToFile(completeFileName, true);
            }
            Console.WriteLine("done.");

            using (FileStream fs = new FileStream(Path.GetTempFileName(),
               FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None,
               4096, FileOptions.RandomAccess | FileOptions.DeleteOnClose))
            {
                // temp file exists
            }

            
        }
    }
}
