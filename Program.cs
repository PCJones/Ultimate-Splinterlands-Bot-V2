using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Ultimate_Splinterlands_Bot_V2.Classes;

namespace Ultimate_Splinterlands_Bot_V2
{
    class Program
    {
        private static object _TaskLock = new object();
        static void Main(string[] args)
        {
            handler = new ConsoleEventDelegate(ConsoleEventCallback);
            SetConsoleCtrlHandler(handler, true);

            Log.WriteStartupInfoToLog();
            SetStartupPath();
            if (!CheckForChromeDriver() || !ReadConfig() || !ReadAccounts())
            {
                Log.WriteToLog("Press any key to close");
                Console.ReadKey();
            }

            Initialize();

            _ = Task.Run(async () => await BotLoop()).ConfigureAwait(false);

            while (true)
            {
                Console.ReadLine();
            }
        }

        static async Task BotLoop()
        {
            var instances = new HashSet<Task>();
            int nextBrowserInstance = 0;
            int nextBotInstance = 0;

            bool logoutNeeded = Settings.BotInstances.Count != Settings.MaxBrowserInstances;

            for (int i = 0; i < Settings.MaxBrowserInstances; i++)
            {
                instances.Add(Task.Run(async () =>
                await Settings.BotInstances[nextBotInstance++].DoBattleAsync(Settings.SeleniumInstances[nextBrowserInstance++], logoutNeeded)));
            }

            while (true)
            {
                Task t = await Task.WhenAny(instances);
                instances.Remove(t);
                if (nextBotInstance >= Settings.BotInstances.Count)
                {
                    Log.WriteSupportInformationToLog();
                    nextBotInstance = 0;
                }
                nextBrowserInstance = nextBrowserInstance >= Settings.MaxBrowserInstances ? 0 : nextBrowserInstance;
                instances.Add(Task.Run(async () => 
                await Settings.BotInstances[nextBotInstance++].DoBattleAsync(Settings.SeleniumInstances[nextBrowserInstance++], logoutNeeded)));
            }
        }

        static bool ReadConfig()
        {
            string filePath = Settings.StartupPath + @"/config/config.txt";
            if (!File.Exists(filePath))
            {
                Log.WriteToLog("No config.txt in config folder - see config-example.txt!", Log.LogType.CriticalError);
                return false;
            }

            Log.WriteToLog("Reading config...");
            foreach (string setting in File.ReadAllLines(filePath))
            {
                string[] temp = setting.Split('=');
                if (setting[0] == '#' || temp.Length != 2)
                {
                    continue;
                }

                switch (temp[0])
                {
                    case "PRIORITIZE_QUEST":
                        Settings.PrioritizeQuest = Boolean.Parse(temp[1]);
                        break;
                    case "SLEEP_BETWEEN_BATTLES":
                        Settings.SleepBetweenBattles = Convert.ToInt32(temp[1]);
                        break;
                    case "ERC_THRESHOLD":
                        Settings.ERCThreshold = Convert.ToInt32(temp[1]);
                        break;
                    case "MAX_BROWSER_INSTANCES":
                        Settings.MaxBrowserInstances = Convert.ToInt32(temp[1]);
                        break;
                    case "CLAIM_SEASON_REWARD":
                        Settings.ClaimSeasonReward = Boolean.Parse(temp[1]);
                        break;
                    case "CLAIM_QUEST_REWARD":
                        Settings.ClaimQuestReward = Boolean.Parse(temp[1]);
                        break;
                    case "HEADLESS":
                        Settings.Headless = Boolean.Parse(temp[1]);
                        break;
                    case "USE_API":
                        Settings.UseAPI = Boolean.Parse(temp[1]);
                        break;
                    case "API_URL":
                        Settings.APIUrl = temp[1];
                        break;
                    case "DEBUG":
                        Settings.DebugMode = Boolean.Parse(temp[1]);
                        break;
                    default:
                        break;
                }
            }

            Log.WriteToLog("Config loaded!", Log.LogType.Success);
            Log.WriteToLog($"Config parameters:{Environment.NewLine}" +
                $"DEBUG: {Settings.DebugMode}{Environment.NewLine}" +
                $"PRIORITIZE_QUEST: {Settings.PrioritizeQuest}{Environment.NewLine}" +
                $"CLAIM_QUEST_REWARD: {Settings.ClaimQuestReward}{Environment.NewLine}" +
                $"CLAIM_SEASON_REWARD: {Settings.ClaimSeasonReward}{Environment.NewLine}" +
                $"SLEEP_BETWEEN_BATTLES: {Settings.SleepBetweenBattles}{Environment.NewLine}" +
                $"ERC_THRESHOLD: {Settings.ERCThreshold}{Environment.NewLine}" +
                $"USE_API: {Settings.UseAPI}{Environment.NewLine}" +
                $"HEADLESS: {Settings.Headless}{Environment.NewLine}");
            return true;
        }

        static bool ReadAccounts()
        {
            Log.WriteToLog("Reading accounts.txt...");
            string filePath = Settings.StartupPath + @"/config/accounts.txt";
            if (!File.Exists(filePath))
            {
                Log.WriteToLog("No accounts.txt in config folder - see accounts-example.txt!", Log.LogType.CriticalError);
                return false;
            }

            Settings.BotInstances = new List<BotInstance>();

            foreach (string loginData in File.ReadAllLines(filePath))
            {
                string[] temp = loginData.Split(':');
                if (temp.Length == 2)
                {
                    Settings.BotInstances.Add(new BotInstance(temp[0].Trim().ToLower(), temp[1].Trim()));
                }
            }

            if (Settings.BotInstances.Count > 0)
            {
                Log.WriteToLog($"Loaded {Settings.BotInstances.Count} accounts!", Log.LogType.Success);
                return true;
            }
            else
            {
                Log.WriteToLog($"Did not load any account", Log.LogType.CriticalError);
                return false;
            }
        }

        static void Initialize()
        {
            if (Settings.MaxBrowserInstances > Settings.BotInstances.Count)
            {
                Log.WriteToLog($"MAX_BROWSER_INSTANCES is larger than total number of accounts, reducing it to {Settings.BotInstances.Count}", Log.LogType.Warning);
                Settings.MaxBrowserInstances = Settings.BotInstances.Count;
            }

            Settings.SeleniumInstances = new OpenQA.Selenium.IWebDriver[Settings.MaxBrowserInstances];
            Log.WriteToLog($"Creating {Settings.MaxBrowserInstances} browser instances...");
            for (int i = 0; i < Settings.MaxBrowserInstances; i++)
            {
                Settings.SeleniumInstances[i] = SeleniumAddons.CreateSeleniumInstance();
            }
            Log.WriteToLog("Browser instances created!", Log.LogType.Success);

            Settings.QuestTypes = new Dictionary<string, string>();
            Settings.QuestTypes.Add("Defend the Borders", "life");
            Settings.QuestTypes.Add("Pirate Attacks", "water");
            Settings.QuestTypes.Add("High Priority Targets", "snipe");
            Settings.QuestTypes.Add("Lyanna's Call", "earth");
            Settings.QuestTypes.Add("Stir the Volcano", "fire");
            Settings.QuestTypes.Add("Rising Dead", "death");
            Settings.QuestTypes.Add("Stubborn Mercenaries", "neutral");
            Settings.QuestTypes.Add("Gloridax Revenge", "dragon");
            Settings.QuestTypes.Add("Stealth Mission", "sneak");

            Settings.Summoners = new Dictionary<string, string>();
            Settings.Summoners.Add("224", "dragon");
            Settings.Summoners.Add("27", "earth");
            Settings.Summoners.Add("16", "water");
            Settings.Summoners.Add("156", "life");
            Settings.Summoners.Add("189", "earth");
            Settings.Summoners.Add("167", "fire");
            Settings.Summoners.Add("145", "death");
            Settings.Summoners.Add("5", "fire");
            Settings.Summoners.Add("71", "water");
            Settings.Summoners.Add("114", "dragon");
            Settings.Summoners.Add("178", "water");
            Settings.Summoners.Add("110", "fire");
            Settings.Summoners.Add("49", "death");
            Settings.Summoners.Add("88", "dragon");
            Settings.Summoners.Add("38", "life");
            Settings.Summoners.Add("239", "life");
            Settings.Summoners.Add("74", "death");
            Settings.Summoners.Add("78", "dragon");
            Settings.Summoners.Add("260", "fire");
            Settings.Summoners.Add("70", "fire");
            Settings.Summoners.Add("109", "death");
            Settings.Summoners.Add("111", "water");
            Settings.Summoners.Add("112", "earth");
            Settings.Summoners.Add("130", "dragon");
            Settings.Summoners.Add("72", "earth");
            Settings.Summoners.Add("235", "dragon");
            Settings.Summoners.Add("56", "dragon");
            Settings.Summoners.Add("113", "life");
            Settings.Summoners.Add("200", "dragon");
            Settings.Summoners.Add("236", "fire");
            Settings.Summoners.Add("240", "dragon");
            Settings.Summoners.Add("254", "water");
            Settings.Summoners.Add("257", "water");
            Settings.Summoners.Add("258", "death");
            Settings.Summoners.Add("259", "earth");
            Settings.Summoners.Add("261", "life");
            Settings.Summoners.Add("262", "dragon");
            Settings.Summoners.Add("278", "earth");
            Settings.Summoners.Add("73", "life");

            Settings.CardsDetails = Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(Settings.StartupPath + @"/data/cardsDetails.json"));
        }

        static void SetStartupPath()
        {
            // Setup startup path
            string path = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string directory = System.IO.Path.GetDirectoryName(path);
            Settings.StartupPath = directory;
        }
        static bool CheckForChromeDriver()
        {
            if (!File.Exists(Settings.StartupPath + @"/chromedriver.exe"))
            {
                Log.WriteToLog("No ChromeDriver installed - please download from https://chromedriver.chromium.org/ and insert .exe into bot folder", Log.LogType.CriticalError);
                return false;
            }

            return true;
        }

        static bool ConsoleEventCallback(int eventType)
        {
            if (eventType == 2)
            {
#pragma warning disable CS1998
                _ = Task.Run(async () => Parallel.ForEach(Settings.SeleniumInstances, x => x.Quit())).ConfigureAwait(false);
#pragma warning restore CS1998
                Log.WriteToLog("Closing browsers...");
                System.Threading.Thread.Sleep(4500);
            }
            return false;
        }
        static ConsoleEventDelegate handler;   // Keeps it from getting garbage collected
                                               // Pinvoke
        private delegate bool ConsoleEventDelegate(int eventType);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);
    }
}
