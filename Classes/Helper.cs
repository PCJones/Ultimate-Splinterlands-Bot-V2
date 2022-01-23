using System;
using System.Collections.Generic;
using System.Linq;
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
    }
}
