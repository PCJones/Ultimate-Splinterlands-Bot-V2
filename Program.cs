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

            bool logoutNeeded = Settings.BotInstances.Count != Settings.MaxBrowserInstances;

            for (int i = 0; i < Settings.MaxBrowserInstances; i++)
            {
                instances.Add(Task.Run(async () => 
                await Settings.BotInstances[i].DoBattleAsync(Settings.SeleniumInstances[nextBrowserInstance++], logoutNeeded)));
            }

            int nextBotInstance = nextBrowserInstance;

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
