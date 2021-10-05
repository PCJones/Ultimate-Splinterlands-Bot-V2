using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Ultimate_Splinterlands_Bot_V2.Classes;
using System.Threading;
using Pastel;
using System.Drawing;

namespace Ultimate_Splinterlands_Bot_V2
{
    class Program
    {
        private static object _TaskLock = new object();
        static void Main(string[] args)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                handler = new ConsoleEventDelegate(ConsoleEventCallback);
                SetConsoleCtrlHandler(handler, true);
            }

            Log.WriteStartupInfoToLog();
            SetStartupPath();
            if (!CheckForChromeDriver() || !ReadConfig() || !ReadAccounts())
            {
                Log.WriteToLog("Press any key to close");
                Console.ReadKey();
            }

            Initialize();

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = cancellationTokenSource.Token;
            _ = Task.Run(async () => await BotLoop(token)).ConfigureAwait(false);

            string command = "";
            while (true)
            {
                command = Console.ReadLine();

                switch (command)
                {
                    case "stop":
                        cancellationTokenSource.Cancel();
                        break;
                    default:
                        break;
                }
            }   
        }

        static async Task BotLoop(CancellationToken token)
        {
            var instances = new HashSet<Task>();
            int nextBrowserInstance = -1;
            int nextBotInstance = -1;

            bool logoutNeeded = Settings.BotInstances.Count != Settings.MaxBrowserInstances;

            DateTime[] sleepInfo = new DateTime[Settings.BotInstances.Count];

            while (!token.IsCancellationRequested)
            {
                while (instances.Count < Settings.MaxBrowserInstances && !token.IsCancellationRequested)
                {
                    int sleepTime = 0;
                    lock (_TaskLock)
                    {
                        if (++nextBotInstance >= Settings.BotInstances.Count)
                        {
                            Log.LogBattleSummaryToTable();
                            Log.WriteSupportInformationToLog();
                            nextBotInstance = 0;
                        }

                        if (Settings.SleepBetweenBattles > 0
                            && Settings.BotInstances.All(x => x.CurrentlyActive
                            || (DateTime)sleepInfo[Settings.BotInstances.IndexOf(x)] > DateTime.Now))
                        {
                            DateTime sleepUntil = sleepInfo.Where(x => 
                            !Settings.BotInstances[Array.IndexOf(sleepInfo, x)].CurrentlyActive)
                                .OrderBy(x => x).First();

                            if (sleepUntil > DateTime.Now)
                            {
                                Log.WriteToLog($"All accounts sleeping or currently active - wait until {sleepUntil}");
                                sleepTime = (int)(sleepUntil - DateTime.Now).TotalMilliseconds;
                            }
                        }
                    }

                    if (sleepTime != 0)
                    {
                        await Task.Delay(sleepTime);
                    }

                    lock (_TaskLock)
                    {
                        nextBrowserInstance = ++nextBrowserInstance >= Settings.MaxBrowserInstances ? 0 : nextBrowserInstance;
                        while (!Settings.SeleniumInstances.ElementAt(nextBrowserInstance).isAvailable)
                        {
                            nextBrowserInstance++;
                            nextBrowserInstance = nextBrowserInstance >= Settings.MaxBrowserInstances ? 0 : nextBrowserInstance;
                        }

                        while (Settings.BotInstances.ElementAt(nextBotInstance).CurrentlyActive)
                        {
                            nextBotInstance++;
                            nextBotInstance = nextBotInstance >= Settings.BotInstances.Count ? 0 : nextBotInstance;
                        }

                        Settings.SeleniumInstances[nextBrowserInstance] = (Settings.SeleniumInstances[nextBrowserInstance].driver, false);

                        // create local copies for thread safety
                        int botInstance = nextBotInstance;
                        int browserInstance = nextBrowserInstance;

                        instances.Add(Task.Run(async () =>
                        {
                            var result = await Settings.BotInstances[botInstance].DoBattleAsync(browserInstance, logoutNeeded, botInstance);
                            lock (_TaskLock)
                            {
                                sleepInfo[nextBotInstance] = result;
                                Settings.SeleniumInstances[browserInstance] = (Settings.SeleniumInstances[browserInstance].driver, true);
                            }
                        }));
                    }
                }

                _ = await Task.WhenAny(instances);
                instances.RemoveWhere(x => x.IsCompleted);
            }

            Log.WriteToLog("Stopping bot...");
            await Task.WhenAll(instances);
            _ = Task.Run(async () => Parallel.ForEach(Settings.SeleniumInstances, x => x.driver.Quit())).Result;
            Log.WriteToLog("Bot stopped!");
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
                if (temp.Length != 2 || setting[0] == '#')
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
                    case "ECR_THRESHOLD":
                        Settings.ECRThreshold = Convert.ToInt32(temp[1]);
                        break;
                    // legacy:
                    case "ERC_THRESHOLD":
                        Settings.ECRThreshold = Convert.ToInt32(temp[1]);
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
                    case "REQUEST_NEW_QUEST":
                        Settings.BadQuests = temp[1].Split(',');
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
                    case "WRITE_LOG_TO_FILE":
                        Settings.WriteLogToFile = Boolean.Parse(temp[1]);
                        break;
                    case "CHROME_BINARY_PATH":
                        Settings.ChromeBinaryPath = temp[1];
                        break;
                    case "CHROME_DRIVER_PATH":
                        Settings.ChromeDriverPath = temp[1];
                        break;
                    case "CHROME_NO_SANDBOX":
                        Settings.ChromeNoSandbox = Boolean.Parse(temp[1]);
                        break;
                    default:
                        break;
                }
            }

            Log.WriteToLog("Config loaded!", Log.LogType.Success);
            Log.WriteToLog($"Config parameters:{Environment.NewLine}" +
                $"DEBUG: {Settings.DebugMode}{Environment.NewLine}" +
                $"WRITE_LOG_TO_FILE: {Settings.WriteLogToFile}{Environment.NewLine}" +
                $"PRIORITIZE_QUEST: {Settings.PrioritizeQuest}{Environment.NewLine}" +
                $"CLAIM_QUEST_REWARD: {Settings.ClaimQuestReward}{Environment.NewLine}" +
                $"CLAIM_SEASON_REWARD: {Settings.ClaimSeasonReward}{Environment.NewLine}" +
                $"REQUEST_NEW_QUEST: {String.Join(",", Settings.BadQuests )}{Environment.NewLine}" +
                $"SLEEP_BETWEEN_BATTLES: {Settings.SleepBetweenBattles}{Environment.NewLine}" +
                $"ECR_THRESHOLD: {Settings.ECRThreshold}{Environment.NewLine}" +
                $"USE_API: {Settings.UseAPI}{Environment.NewLine}" +
                $"HEADLESS: {Settings.Headless}{Environment.NewLine}" +
                $"MAX_BROWSER_INSTANCES: {Settings.MaxBrowserInstances}{Environment.NewLine}");
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

            int indexCounter = 0;
            foreach (string loginData in File.ReadAllLines(filePath))
            {
                if (loginData.Trim().Length == 0 || loginData[0] == '#')
                {
                    continue;
                }
                string[] temp = loginData.Split(':');
                if (temp.Length == 2)
                {
                    Settings.BotInstances.Add((new BotInstance(temp[0].Trim().ToLower(), temp[1].Trim(), indexCounter++)));
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

            Settings.SeleniumInstances = new List<(OpenQA.Selenium.IWebDriver driver, bool isAvailable)>();
            Log.WriteToLog($"Creating {Settings.MaxBrowserInstances} browser instances...");
            for (int i = 0; i < Settings.MaxBrowserInstances; i++)
            {
                Settings.SeleniumInstances.Add((SeleniumAddons.CreateSeleniumInstance(), true));
                Thread.Sleep(1000);
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

            Settings.LogSummaryList = new List<(int index, string account, string battleResult, string rating, string ECR, string questStatus)>();

            API._httpClient.Timeout = new TimeSpan(0, 3, 0);
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
                _ = Task.Run(async () => Parallel.ForEach(Settings.SeleniumInstances, x => x.driver.Quit())).ConfigureAwait(false);
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
