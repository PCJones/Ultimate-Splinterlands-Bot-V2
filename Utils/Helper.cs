using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ultimate_Splinterlands_Bot_V2.Config;

namespace Ultimate_Splinterlands_Bot_V2.Utils
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
                int index = Settings._Random.Next(Settings.CharSubset.Length);
                buf[i] = Settings.CharSubset[index];
            }

            return new string(buf);
        }
        public async static Task<string> DownloadPageAsync(string url)
        {
            // Use static HttpClient to avoid exhausting system resources for network connections.
            var result = await Settings._httpClient.GetAsync(url);
            if (result.StatusCode == HttpStatusCode.TooManyRequests)
            {
                int sleepingTime = Settings._Random.Next(60, 300);
                Log.WriteToLog($"Splinterlands rate limit - sleeping for {sleepingTime} seconds", Log.LogType.Warning);
                await Task.Delay(sleepingTime * 1000).ConfigureAwait(false);
                return await DownloadPageAsync(url);
            }
            else if (result.StatusCode == HttpStatusCode.BadGateway || result.StatusCode == HttpStatusCode.GatewayTimeout)
            {
                int sleepingTime = 10;
                Log.WriteToLog($"Splinterlands API error - sleeping for {sleepingTime} seconds", Log.LogType.Warning);
                await Task.Delay(sleepingTime * 1000).ConfigureAwait(false);
                return await DownloadPageAsync(url);
            }
            var response = await result.Content.ReadAsStringAsync();
            // Write status code.
            return response;
        }

        public static string DoQuickRegex(string Pattern, string Match)
        {
            Regex r = new(Pattern, RegexOptions.Singleline);
            return r.Match(Match).Groups[1].Value;
        }

        public static void SerializeBotInstances()
        {
            string filePathAccountData = Settings.StartupPath + @"/config/account_data.json";
            File.WriteAllText(filePathAccountData, JsonConvert.SerializeObject(Settings.BotInstances));
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
                
                string gitHubUrl = $"https://api.github.com/repos/{Settings.BOT_GITHUB_REPO}/releases";
                string releasesRaw = DownloadPageAsync(gitHubUrl).Result;
                string[] localVersion = File.ReadAllLines(versionFilePath);
                DateTime currentVersionPublishDate = DateTime.ParseExact(localVersion[0].Trim(), "yyyy-MM-dd' 'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                string publishDateRaw = "";
                string name = "";
                JToken newestRelease;
                using (JsonReader reader = new JsonTextReader(new StringReader(releasesRaw)))
                {
                    reader.DateParseHandling = DateParseHandling.None;
                    newestRelease = JArray.Load(reader)[0];
                    publishDateRaw = (string)newestRelease["published_at"];
                    name = (string)newestRelease["name"];
                }
                if (name.Contains("beta") || name.Contains("alpha") || name.Contains("test"))
                {
                    return;
                }
                DateTime releasePublishDate = DateTime.ParseExact(publishDateRaw, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

                if (releasePublishDate > currentVersionPublishDate)
                {
                    bool autoUpdate = Settings.AutoUpdate;

                    if (Settings.AutoUpdate)
                    {
                        var asset = newestRelease["assets"].Where(x => (string)x["name"] == localVersion[1].Trim() + ".zip").FirstOrDefault();
                        if (asset == null)
                        {
                            autoUpdate = false;
                        }
                        else
                        {
                            Log.WriteToLog("New bot update available!", Log.LogType.Warning);
                            Log.WriteToLog("New bot update available!", Log.LogType.Warning);
                            Log.WriteToLog("New bot update available!", Log.LogType.Warning);
                            Log.WriteToLog("Press any key to start the update...");
                            Console.ReadKey();
                            string url = (string)asset["browser_download_url"];

                            // create temp folder
                            DirectoryInfo d = new DirectoryInfo(Settings.StartupPath);
                            string tmpFolderPath = Settings.StartupPath + "/tmp/";
                            Directory.CreateDirectory(tmpFolderPath);
                            string fileNameExe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Ultimate Splinterlands Bot V2.exe" : "Ultimate Splinterlands Bot V2";
                            foreach (var file in d.GetFiles("*.*"))
                            {
                                File.Copy(file.FullName, tmpFolderPath + "/" + file.Name);
                            }

                            string tmpApplicationPath = tmpFolderPath + "/" + fileNameExe;
                            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                            {
                                LinuxExec("chmod -R 775 " + tmpApplicationPath.Replace(" ", "\\ "));
                            }
                            RunProcess(tmpApplicationPath, "update \"" + Settings.StartupPath + "/" + fileNameExe + "\" \"" + url + "\" \"" + Settings.StartupPath 
                                + "\" \"" + localVersion[1].Trim() + "\" \"" + releasePublishDate.ToUniversalTime().ToString("u") + "\"");
                            Environment.Exit(0);
                        }
                    }

                    if(!autoUpdate)
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

        public static void LinuxExec(string cmd)
        {
            var escapedArgs = cmd.Replace("\"", "\\\"");

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{escapedArgs}\""
                }
            };

            process.Start();
            process.WaitForExit();
        }

        public static void KillInstances()
        {
            foreach (var process in System.Diagnostics.Process.GetProcessesByName("Ultimate Splinterlands Bot V2"))
            {
                try
                {
                    if (process.Id == System.Diagnostics.Process.GetCurrentProcess().Id)
                    {
                        continue;
                    }
                    System.Threading.Thread.Sleep(2000);
                    Console.WriteLine("Killing running bot process...");
                    process.Kill();
                }
                catch (Exception ex)
                {
                    Log.WriteToLog(ex.Message, Log.LogType.Error);
                }
            }
        }

        public static void UpdateViaArchive(string downloadUrl, string extractTarget)
        {
            Console.WriteLine("Beginning update.");
            Console.WriteLine("Downloading archive...");
            string downloadDestination = Path.GetTempFileName();
            WebClient downloadifier = new WebClient();
            downloadifier.DownloadFile(downloadUrl, downloadDestination);
            Console.WriteLine("Downloading finished.");
            Console.Write("Extracting archive... ");

            ZipArchive archive = ZipFile.Open(downloadDestination, ZipArchiveMode.Read);
            foreach (ZipArchiveEntry file in archive.Entries)
            {
                string completeFileName = Path.Combine(extractTarget, file.FullName);

                if (!Directory.Exists(Path.GetDirectoryName(completeFileName)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(completeFileName));
                }

                if (completeFileName[^1..] != "/")
                {
                    try
                    {
                        file.ExtractToFile(completeFileName, true);
                    }
                    catch (Exception ex)
                    {
                        Log.WriteToLog(ex.Message, Log.LogType.CriticalError);
                        Console.ReadLine();
                    }
                }
            }

            Console.WriteLine("done.");
        }
    }
}
