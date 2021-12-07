using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using Pastel;
using Ultimate_Splinterlands_Bot_V2.Classes.Config;
using Ultimate_Splinterlands_Bot_V2.Classes.Http;
using Ultimate_Splinterlands_Bot_V2.Classes.Utils;

namespace Ultimate_Splinterlands_Bot_V2.Classes.Bots
{
    public static class Bots
    {
        public static void Initialize()
        {
            Log.WriteToLog("Initializing bot instances...");
            if (Settings.UseLightningMode)
            {
                if (Settings.Threads > Settings.BotInstancesBlockchain.Count)
                {
                    Log.WriteToLog($"THREADS is larger than total number of accounts, reducing it to {Settings.BotInstancesBlockchain.Count.ToString().Pastel(Color.Red)}", Log.LogType.Warning);
                    Settings.Threads = Settings.BotInstancesBlockchain.Count;
                }
                Console.Write($"SHOW_BATTLE_RESULTS: {Settings.ShowBattleResults}{Environment.NewLine}");
                Console.Write($"THREADS: {Settings.Threads}{Environment.NewLine}");
                Settings.oHived = new HiveAPI.CS.CHived(HttpClient.getInstance(), Settings.HIVE_NODE);
            }
            else
            {
                if (Settings.MaxBrowserInstances > Settings.BotInstancesBrowser.Count)
                {
                    Log.WriteToLog($"MAX_BROWSER_INSTANCES is larger than total number of accounts, reducing it to {Settings.BotInstancesBrowser.Count.ToString().Pastel(Color.Red)}", Log.LogType.Warning);
                    Settings.MaxBrowserInstances = Settings.BotInstancesBrowser.Count;
                }
                Console.Write($"HEADLESS: {Settings.Headless}{Environment.NewLine}");
                Console.Write($"MAX_BROWSER_INSTANCES: {Settings.MaxBrowserInstances}{Environment.NewLine}");
                
                Settings.SeleniumInstances = new List<(OpenQA.Selenium.IWebDriver driver, bool isAvailable)>();
                Log.WriteToLog($"Creating {Settings.MaxBrowserInstances.ToString().Pastel(Color.Red)} browser instances...");
                for (int i = 0; i < Settings.MaxBrowserInstances; i++)
                {
                    Settings.SeleniumInstances.Add((SeleniumAddons.CreateSeleniumInstance(disableImages: false), true));
                    Thread.Sleep(1000);
                }
                Log.WriteToLog("Browser instances created!", Log.LogType.Success);
            }
        }
    }
}