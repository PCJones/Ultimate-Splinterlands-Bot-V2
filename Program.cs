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
using System.Reflection;

namespace Ultimate_Splinterlands_Bot_V2
{
    class Program
    {
        private static object _TaskLock = new object();
        static void Main(string[] args)
        {if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
                    try
                    {
                        lock (_TaskLock)
                        {
                            if (++nextBotInstance >= Settings.BotInstances.Count)
                            {
                                Log.LogBattleSummaryToTable();
                                Log.WriteSupportInformationToLog();
                                nextBotInstance = 0;
                            }

                            //if (Settings.SleepBetweenBattles > 0
                                //&& Settings.BotInstances.All(x => x.CurrentlyActive
                                while (Settings.RateLimited || Settings.BotInstances.All(x => x.CurrentlyActive
                                || (DateTime)sleepInfo[Settings.BotInstances.IndexOf(x)] > DateTime.Now))
                            {
                                Thread.Sleep(20000);
                                //DateTime sleepUntil = sleepInfo.Where(x =>
                                //!Settings.BotInstances[Array.IndexOf(sleepInfo, x)].CurrentlyActive)
                                //    .OrderBy(x => x).First();

                                //if (sleepUntil > DateTime.Now)
                                //{
                                //    Log.WriteToLog($"All accounts sleeping or currently active - wait until {sleepUntil.ToString().Pastel(Color.Red)}");
                                //    int sleepTime = (int)(sleepUntil - DateTime.Now).TotalMilliseconds;
                                //    instances.Add(Task.Delay(sleepTime));
                                //    break;
                                //}
                            }
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
                            }, CancellationToken.None));
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.WriteToLog("BotLoop Error: " + ex.ToString(), Log.LogType.CriticalError);
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
                    case "DONT_CLAIM_QUEST_NEAR_HIGHER_LEAGUE":
                        Settings.DontClaimQuestNearHigherLeague = Boolean.Parse(temp[1]);
                        break;
                    case "ADVANCE_LEAGUE":
                        Settings.AdvanceLeague = Boolean.Parse(temp[1]);
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
                    case "RENTAL_BOT_DLL_PATH":
                        Settings.RentalBotDllPath = temp[1];
                        break;
                    case "RENTAL_BOT":
                        if (Boolean.Parse(temp[1]))
                        {
                            SetupRentalBot();
                        }
                        break;
                    case "USE_PRIVATE_API":
                        Settings.UsePrivateAPI = Boolean.Parse(temp[1]);
                        if (Settings.UsePrivateAPI)
                        {
                            string[] loginData = File.ReadAllText(Settings.StartupPath + @"/config/login.txt").Split(':');
                            Settings.PrivateAPIUsername = loginData[0];
                            Settings.PrivateAPIPassword = loginData[1];
                        }
                        break;
                    case "PRIVATE_API_SHOP":
                        Settings.PrivateAPIShop = temp[1];
                        break;
                    case "PRIVATE_API_URL":
                        Settings.PrivateAPIUrl = temp[1];
                        break;
                    case "RENT_DAYS":
                        Settings.DaysToRent = Convert.ToInt32(temp[1]);
                        break;
                    case "RENT_POWER":
                        Settings.DesiredRentalPower = Convert.ToInt32(temp[1]);
                        break;
                    case "RENT_MAX_PRICE_PER_500":
                        Settings.MaxRentalPricePer500 = Convert.ToDecimal(temp[1], System.Globalization.CultureInfo.InvariantCulture);
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
                $"REQUEST_NEW_QUEST: {String.Join(",", Settings.BadQuests)}{Environment.NewLine}" +
                $"SLEEP_BETWEEN_BATTLES: {Settings.SleepBetweenBattles}{Environment.NewLine}" +
                $"ECR_THRESHOLD: {Settings.ECRThreshold}{Environment.NewLine}" +
                $"USE_API: {Settings.UseAPI}{Environment.NewLine}" +
                $"HEADLESS: {Settings.Headless}{Environment.NewLine}" +
                $"MAX_BROWSER_INSTANCES: {Settings.MaxBrowserInstances}{Environment.NewLine}");
            return true;
        }

        static void SetupRentalBot()
        {
            var moduleInstance = Activator.CreateInstanceFrom(Settings.RentalBotDllPath, "Splinterlands_Rental_Bot.RentalBot");
            Settings.RentalBot = moduleInstance;
            MethodInfo mi = moduleInstance.Unwrap().GetType().GetMethod("Setup");
            
            mi.Invoke(moduleInstance.Unwrap(), new object[] { Settings._httpClient });
            Settings.RentalBotMethodCheckRentals = moduleInstance.Unwrap().GetType().GetMethod("CheckRentals");
            Settings.RentalBotMethodIsAvailable = moduleInstance.Unwrap().GetType().GetMethod("IsAvailable");
            Settings.RentalBotMethodSetActive = moduleInstance.Unwrap().GetType().GetMethod("SetActive");
            Settings.RentalBotActivated = true;
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
                    Settings.BotInstances.Add(new BotInstance(temp[0].Trim().ToLower(), temp[1].Trim(), indexCounter++));
                }
                else if (temp.Length == 3)
                {
                    Settings.BotInstances.Add(new BotInstance(temp[0].Trim().ToLower(), temp[1].Trim(), indexCounter++, key: temp[2].Trim()));
                }
            }

            if (Settings.BotInstances.Count > 0)
            {
                Log.WriteToLog($"Loaded {Settings.BotInstances.Count.ToString().Pastel(Color.Red)} accounts!", Log.LogType.Success);
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
                Log.WriteToLog($"MAX_BROWSER_INSTANCES is larger than total number of accounts, reducing it to {Settings.BotInstances.Count.ToString().Pastel(Color.Red)}", Log.LogType.Warning);
                Settings.MaxBrowserInstances = Settings.BotInstances.Count;
            }

            Settings.SeleniumInstances = new List<(OpenQA.Selenium.IWebDriver driver, bool isAvailable)>();
            Log.WriteToLog($"Creating {Settings.MaxBrowserInstances.ToString().Pastel(Color.Red)} browser instances...");
            for (int i = 0; i < Settings.MaxBrowserInstances; i++)
            {
                Settings.SeleniumInstances.Add((SeleniumAddons.CreateSeleniumInstance(disableImages: false), true));
                Thread.Sleep(1000);
            }
            Log.WriteToLog("Browser instances created!", Log.LogType.Success);

            Settings.QuestTypes = new Dictionary<string, string>
            {
                { "Defend the Borders", "life" },
                { "Pirate Attacks", "water" },
                { "High Priority Targets", "snipe" },
                { "Lyanna's Call", "earth" },
                { "Stir the Volcano", "fire" },
                { "Rising Dead", "death" },
                { "Stubborn Mercenaries", "neutral" },
                { "Gloridax Revenge", "dragon" },
                { "Stealth Mission", "sneak" }
            };

            Settings.Summoners = new Dictionary<string, string>
            {
                { "224", "dragon" },
                { "27", "earth" },
                { "16", "water" },
                { "156", "life" },
                { "189", "earth" },
                { "167", "fire" },
                { "145", "death" },
                { "5", "fire" },
                { "71", "water" },
                { "114", "dragon" },
                { "178", "water" },
                { "110", "fire" },
                { "49", "death" },
                { "88", "dragon" },
                { "38", "life" },
                { "239", "life" },
                { "74", "death" },
                { "78", "dragon" },
                { "260", "fire" },
                { "70", "fire" },
                { "109", "death" },
                { "111", "water" },
                { "112", "earth" },
                { "130", "dragon" },
                { "72", "earth" },
                { "235", "dragon" },
                { "56", "dragon" },
                { "113", "life" },
                { "200", "dragon" },
                { "236", "fire" },
                { "240", "dragon" },
                { "254", "water" },
                { "257", "water" },
                { "258", "death" },
                { "259", "earth" },
                { "261", "life" },
                { "262", "dragon" },
                { "278", "earth" },
                { "73", "life" }
            };

            Settings.CardsDetails = Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(Settings.StartupPath + @"/data/cardsDetails.json"));

            Settings.LogSummaryList = new List<(int index, string account, string battleResult, string rating, string ECR, string questStatus)>();

            Settings._httpClient.Timeout = new TimeSpan(0, 3, 0);
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
