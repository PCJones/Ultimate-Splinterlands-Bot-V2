using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ultimate_Splinterlands_Bot_V2.Classes.Config;
using Ultimate_Splinterlands_Bot_V2.Classes.Http;

namespace Ultimate_Splinterlands_Bot_V2.Classes.Utils
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
                StringBuilder sb = new StringBuilder();
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
            var result = await HttpClient.getInstance().GetAsync(url);
            var response = await result.Content.ReadAsStringAsync();
            // Write status code.
            return response;
        }

        public static string DoQuickRegex(string Pattern, string Match)
        {
            Regex r = new Regex(Pattern, RegexOptions.Singleline);
            return r.Match(Match).Groups[1].Value;
        }

        public static string capitalize(string input) {
            if (input.Length == 0)
            {
                return input;
            }
            else if (input.Length == 1)
            {
                return char.ToUpper(input[0]).ToString();
            }
            return char.ToUpper(input[0]) + input.Substring(1).ToLower();
        }
    }
}
