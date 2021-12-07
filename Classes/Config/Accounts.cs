using System;
using System.Drawing;
using System.IO;
using System.Linq;
using Pastel;
using Ultimate_Splinterlands_Bot_V2.Classes.Bot;
using Ultimate_Splinterlands_Bot_V2.Classes.Utils;

namespace Ultimate_Splinterlands_Bot_V2.Classes.Config
{
    public static class Accounts
    {
        public static bool Initialize()
        {
            Log.WriteToLog("Reading accounts.txt...");
            string filePathAccounts = Settings.StartupPath + @"/config/accounts.txt";
            string filePathAccessTokens = Settings.StartupPath + @"/config/access_tokens.txt";
            if (!File.Exists(filePathAccounts))
            {
                Log.WriteToLog("No accounts.txt in config folder - see accounts-example.txt!", Log.LogType.CriticalError);
                return false;
            }

            if (!File.Exists(filePathAccessTokens))
            {
                File.WriteAllText(filePathAccessTokens, "#DO NOT SHARE THESE!" + Environment.NewLine);
            }

            //TODO: do not use BotInstances in Settings? settings should only be "settings"
            if (Settings.UseLightningMode)
            {
                Settings.BotInstancesBlockchain = new();
            }
            else
            {
                Settings.BotInstancesBrowser = new();
            }

            string[] accessTokens = File.ReadAllLines(filePathAccessTokens);
            int indexCounter = 0;

            foreach (string loginData in File.ReadAllLines(filePathAccounts))
            {
                if (loginData.Trim().Length == 0 || loginData[0] == '#')
                {
                    continue;
                }
                string[] temp = loginData.Split(':');
                var query = accessTokens.Where(x => x.Split(':')[0] == temp[0]);
                string accessToken = query.Any()? query.First().Split(':')[1] : "";

                if (temp.Length != 2 && temp.Length != 3)
                {
                    Log.WriteToLog($"Invalid account row: {loginData}", Log.LogType.Warning);
                    continue;
                }
                
                string name = temp[0].Trim().ToLower();
                string password = temp[1].Trim();
                int newIndex = indexCounter++;
                string activeKey = "";
                if (temp.Length == 3) 
                {
                    activeKey = temp[2].Trim();
                }
                if (Settings.UseLightningMode)
                {
                    Settings.BotInstancesBlockchain.Add(new BotInstanceBlockchain(name, password, accessToken, newIndex, activeKey: activeKey));
                }
                else
                {
                    Settings.BotInstancesBrowser.Add(new BotInstanceBrowser(name, password, newIndex, key: activeKey));
                }
            }

            if ((Settings.BotInstancesBrowser != null && Settings.BotInstancesBrowser.Count > 0) || Settings.BotInstancesBlockchain.Count > 0)
            {
                //TODO: use multicolor logging?
                Log.WriteToLog($"Loaded {(Settings.UseBrowserMode ? Settings.BotInstancesBrowser.Count.ToString().Pastel(Color.Red) : Settings.BotInstancesBlockchain.Count.ToString().Pastel(Color.Red))} accounts!", Log.LogType.Success);
                return true;
            }
            else
            {
                Log.WriteToLog($"Did not load any account", Log.LogType.CriticalError);
                return false;
            }
        }
    }
}