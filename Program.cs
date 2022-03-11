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
using Ultimate_Splinterlands_Bot_V2.Classes.Utils;
using Ultimate_Splinterlands_Bot_V2.Classes.Config;
using Ultimate_Splinterlands_Bot_V2.Classes.Api;
using Ultimate_Splinterlands_Bot_V2.Classes.Bot;

namespace Ultimate_Splinterlands_Bot_V2
{
    class Program
    {
        private static object _TaskLock = new();
        private static object _SleepInfoLock = new();
        static void Main(string[] args)
        {
            SetStartupPath();

            if (args.Length > 0 && args[0] == "update")
            {
                Helper.KillInstances();
                Helper.UpdateViaArchive(args[2], args[3]);

                string versionText = args[5] + Environment.NewLine + args[4];
                File.WriteAllText(args[3] + "/config/version.usb", versionText);

                Helper.RunProcess(args[1], "");
                Environment.Exit(0);
            }
            else if (Directory.Exists(Settings.StartupPath + "/tmp/"))
            {
                Thread.Sleep(2000);
                Directory.Delete(Settings.StartupPath + "/tmp/", true);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (Environment.OSVersion.Version.Major < 10)
                {
                    Console.WriteLine("Legacy mode for old Windows version activated - please update your Windows to Windows 10 or higher / Windows Server 2016 or higher to get maximum bot speed");
                    Settings.LegacyWindowsMode = true;
                    ConsoleExtensions.Disable();
                }
            }

            Log.WriteStartupInfoToLog();

            // We have to configure the http client early because it might be used in account constructor
            Settings._httpClient.Timeout = new TimeSpan(0, 2, 15);
            Settings._httpClient.DefaultRequestHeaders.Add("User-Agent", "USB");

            if (!ReadConfig() || !ReadAccounts())
            {
                Log.WriteToLog("Press any key to close");
                Console.ReadKey();
                Environment.Exit(0);
            }

            Helper.CheckForUpdate();

            if (Settings.ClaimSeasonReward)
            {
                Log.WriteToLog("Season Reward Claiming mode activated - set CLAIM_SEASON_REWARD=false to disable!", Log.LogType.Warning);
                Log.WriteToLog("The bot will only claim rewards, it will not fight!", Log.LogType.Warning);
                Thread.Sleep(3500);
            }

            Thread.Sleep(1500); // Sleep 1.5 seconds to read config and welcome message

            Initialize();

            CancellationTokenSource cancellationTokenSource = new();
            CancellationToken token = cancellationTokenSource.Token;
            _ = Task.Run(async () => await BotLoopAsync(token)).ConfigureAwait(false);

            string command = "";
            while (true)
            {
                command = Console.ReadLine();

                switch (command)
                {
                    case "stop":
                        Log.WriteToLog("Stopping bot...", Log.LogType.Warning);
                        cancellationTokenSource.Cancel();
                        break;
                    default:
                        break;
                }
            }   
        }

        static async Task BotLoopAsync(CancellationToken token)
        {
            var instances = new HashSet<Task>();
            int nextBotInstance = -1;
            bool firstRuntrough = true;

            DateTime[] sleepInfo = new DateTime[Settings.BotInstances.Count];

            var ts = new CancellationTokenSource();
            var cancellationToken = ts.Token;
            //DateTime lastResetTime = DateTime.Now;

            while (!token.IsCancellationRequested)
            {
                while (instances.Count < (Settings.Threads) && !token.IsCancellationRequested)
                {
                    try
                    {
                        lock (_TaskLock)
                        {
                            if (++nextBotInstance >= (Settings.BotInstances.Count))
                            {
                                firstRuntrough = false;
                                Log.LogBattleSummaryToTable();
                                Log.WriteSupportInformationToLog();
                                Thread.Sleep(5000);
                                nextBotInstance = 0;
                                while (SplinterlandsAPI.CheckForMaintenance().Result)
                                {
                                    Log.WriteToLog("Splinterlands maintenance - waiting 3 minutes");
                                    Thread.Sleep(3 * 60000);
                                }
                            }

                            bool sleep = true;
                            do
                            {
                                lock (_SleepInfoLock)
                                {
                                    if (!Settings.BotInstances.All(x => x.CurrentlyActive
                                        || ((DateTime)sleepInfo[Settings.BotInstances.IndexOf(x)] > DateTime.Now
                                        && !Settings.PlannedPowerTransfers.ContainsKey(x.Username))))
                                    {
                                        sleep = false;
                                    }
                                }

                                if (sleep)
                                {
                                    Thread.Sleep(20 * 1000);
                                }
                            } while (sleep && !token.IsCancellationRequested);
                        }

                        lock (_TaskLock)
                        {
                            if (firstRuntrough && !Settings.ClaimSeasonReward)
                            {
                                // Delay accounts to avoid them fighting each other
                                if (Settings.Threads >= 5)
                                {
                                    Thread.Sleep(Settings._Random.Next(1000, 6000));
                                }
                                else
                                {
                                    Thread.Sleep(Settings._Random.Next(500, 2000));
                                }
                            }

                            while (Settings.BotInstances.ElementAt(nextBotInstance).CurrentlyActive)
                            {
                                nextBotInstance++;
                                nextBotInstance = nextBotInstance >= Settings.BotInstances.Count ? 0 : nextBotInstance;
                            }
                            // create local copies for thread safety
                            int botInstance = nextBotInstance;

                            instances.Add(Task.Run(async () =>
                            {
                                var result = await Settings.BotInstances[botInstance].DoBattleAsync();
                                lock (_SleepInfoLock)
                                {
                                    sleepInfo[nextBotInstance] = result;
                                }
                            }, cancellationToken));
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

            await Task.WhenAll(instances);
            Log.WriteToLog("Bot stopped!");
        }

        static bool ReadConfig()
        {
            string folder = Settings.StartupPath + @"/config/";
            string filePathConfig = folder + "config.txt";
            string filePathCardSettings = folder + "card_settings.txt";

            if (!File.Exists(filePathConfig))
            {
                Log.WriteToLog("No config.txt in config folder - see config-example.txt!", Log.LogType.CriticalError);
                return false;
            }
            //if (!File.Exists(filePathCardSettings))
            //{
            //    Log.WriteToLog("No card_settings.txt in config folder!", Log.LogType.CriticalError);
            //    return false;
            //}

            //Settings.CardSettings = new CardSettings(File.ReadAllText(filePathCardSettings));
            Settings.CardSettings = new("USE_CARD_SETTINGS=false");

            Log.WriteToLog("Reading config...");
            foreach (string setting in File.ReadAllLines(filePathConfig))
            {
                string[] temp = setting.Split('=');
                if (temp.Length != 2 || setting[0] == '#')
                {
                    continue;
                }

                switch (temp[0].Trim().ToUpper())
                {
                    case "PRIORITIZE_QUEST":
                        Settings.PrioritizeQuest = bool.Parse(temp[1]);
                        break;          
                    case "IGNORE_ECR_FOR_QUEST":
                        Settings.IgnoreEcrForQuest = bool.Parse(temp[1]);
                        break;
                    case "SLEEP_BETWEEN_BATTLES":
                        Settings.SleepBetweenBattles = Convert.ToInt32(temp[1]);
                        break;
                    // legacy
                    case "ECR_THRESHOLD":
                        Settings.StopBattleBelowECR = Convert.ToInt32(temp[1]);
                        break;
                    // legacy:
                    case "ERC_THRESHOLD":
                        Settings.StopBattleBelowECR = Convert.ToInt32(temp[1]);
                        break;
                    case "STOP_BATTLE_BELOW_ECR":
                        Settings.StopBattleBelowECR = Convert.ToInt32(temp[1]);
                        break;
                    case "START_BATTLE_ABOVE_ECR":
                        Settings.StartBattleAboveECR = Convert.ToInt32(temp[1]);
                        break;
                    case "MINIMUM_BATTLE_POWER":
                        Settings.MinimumBattlePower = Convert.ToInt32(temp[1]);
                        break;
                    case "CLAIM_SEASON_REWARD":
                        Settings.ClaimSeasonReward = bool.Parse(temp[1]);
                        break;
                    case "CLAIM_QUEST_REWARD":
                        Settings.ClaimQuestReward = bool.Parse(temp[1]);
                        break;
                    case "DONT_CLAIM_QUEST_NEAR_HIGHER_LEAGUE":
                        Settings.DontClaimQuestNearHigherLeague = bool.Parse(temp[1]);
                        break;
                    case "WAIT_FOR_MISSING_CP_AT_QUEST_CLAIM":
                        Settings.WaitForMissingCPAtQuestClaim = bool.Parse(temp[1]);
                        break;
                    case "ADVANCE_LEAGUE":
                        Settings.AdvanceLeague = bool.Parse(temp[1]);
                        break;
                    case "REQUEST_NEW_QUEST":
                        Settings.BadQuests = temp[1].Split(',');
                        break;
                    case "SHOW_BATTLE_RESULTS":
                        Settings.ShowBattleResults = bool.Parse(temp[1]);
                        break;
                    case "THREADS":
                        Settings.Threads = Convert.ToInt32(temp[1]);
                        break;
                    case "USE_BROWSER_MODE":
                        if (bool.Parse(temp[1]))
                        {
                            Log.WriteToLog("Browser mode is no longer supported - will use lightning mode!", Log.LogType.Warning);
                        }
                        break;
                    case "USE_API":
                        Settings.UseAPI = bool.Parse(temp[1]);
                        break;
                    case "API_URL":
                        Settings.PublicAPIUrl = temp[1];
                        break;
                    case "DEBUG":
                        Settings.DebugMode = bool.Parse(temp[1]);
                        break;
                    case "WRITE_LOG_TO_FILE":
                        Settings.WriteLogToFile = bool.Parse(temp[1]);
                        break;
                    case "AUTO_UPDATE":
                        Settings.AutoUpdate = bool.Parse(temp[1]);
                        break;
                    case "DISABLE_CONSOLE_COLORS":
                        if (bool.Parse(temp[1]))
                        {
                            Log.WriteToLog("Console colors disabled!");
                            ConsoleExtensions.Disable();
                        }
                        break;
                    case "SHOW_API_RESPONSE":
                        Settings.ShowAPIResponse = bool.Parse(temp[1]);
                        break;
                    case "RENTAL_BOT_DLL_PATH":
                        Settings.RentalBotDllPath = temp[1];
                        break;
                    case "RENTAL_BOT":
                        if (bool.Parse(temp[1]))
                        {
                            SetupRentalBot();
                        }
                        break;
                    case "USE_PRIVATE_API":
                        Settings.UsePrivateAPI = bool.Parse(temp[1]);
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
                    case "POWER_TRANSFER_BOT":
                        Settings.PowerTransferBot = bool.Parse(temp[1]);
                        if (Settings.PowerTransferBot)
                        {
                            Settings.AvailablePowerTransfers = new();
                        }
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
                $"MODE: {"LIGHTNING (blockchain)"}{Environment.NewLine}" +
                $"DEBUG: {Settings.DebugMode}{Environment.NewLine}" +
                $"AUTO_UPDATE: {Settings.AutoUpdate}{Environment.NewLine}" +
                $"WRITE_LOG_TO_FILE: {Settings.WriteLogToFile}{Environment.NewLine}" +
                $"SHOW_API_RESPONSE: {Settings.ShowAPIResponse}{Environment.NewLine}" +
                $"PRIORITIZE_QUEST: {Settings.PrioritizeQuest}{Environment.NewLine}" +
                $"IGNORE_ECR_FOR_QUEST: {Settings.IgnoreEcrForQuest}{Environment.NewLine}" +
                $"CLAIM_QUEST_REWARD: {Settings.ClaimQuestReward}{Environment.NewLine}" +
                $"CLAIM_SEASON_REWARD: {Settings.ClaimSeasonReward}{Environment.NewLine}" +
                $"REQUEST_NEW_QUEST: {string.Join(",", Settings.BadQuests)}{Environment.NewLine}" +
                $"DONT_CLAIM_QUEST_NEAR_HIGHER_LEAGUE: {Settings.DontClaimQuestNearHigherLeague}{Environment.NewLine}" +
                $"WAIT_FOR_MISSING_CP_AT_QUEST_CLAIM: {Settings.WaitForMissingCPAtQuestClaim}{Environment.NewLine}" +
                $"ADVANCE_LEAGUE: {Settings.AdvanceLeague}{Environment.NewLine}" +
                $"SLEEP_BETWEEN_BATTLES: {Settings.SleepBetweenBattles}{Environment.NewLine}" +
                $"START_BATTLE_ABOVE_ECR: {Settings.StartBattleAboveECR}{Environment.NewLine}" +
                $"STOP_BATTLE_BELOW_ECR: {Settings.StopBattleBelowECR}{Environment.NewLine}" +
                $"USE_API: {Settings.UseAPI}{Environment.NewLine}" +
                $"USE_PRIVATE_API: {Settings.UsePrivateAPI}{ Environment.NewLine}" +
                $"POWER_TRANSFER_BOT: {Settings.PowerTransferBot} {Environment.NewLine}");
                $"{Settings.CardSettings}");
                
                Console.Write($"SHOW_BATTLE_RESULTS: {Settings.ShowBattleResults}{Environment.NewLine}");
                Console.Write($"THREADS: {Settings.Threads}{Environment.NewLine}");
            return true;
        }

        static void SetupRentalBot()
        {
            var moduleInstance = Activator.CreateInstanceFrom(Settings.RentalBotDllPath, "Splinterlands_Rental_Bot.RentalBot");
            Settings.RentalBot = moduleInstance;
            MethodInfo mi = moduleInstance.Unwrap().GetType().GetMethod("Setup");
            
            mi.Invoke(moduleInstance.Unwrap(), new object[] { Settings._httpClient, false });
            Settings.RentalBotMethodCheckRentals = moduleInstance.Unwrap().GetType().GetMethod("CheckRentals");
            Settings.RentalBotMethodIsAvailable = moduleInstance.Unwrap().GetType().GetMethod("IsAvailable");
            Settings.RentalBotMethodSetActive = moduleInstance.Unwrap().GetType().GetMethod("SetActive");
            Settings.RentalBotActivated = true;
        }
        static bool ReadAccounts()
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

            Settings.BotInstances = new();
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

                if (temp.Length == 2)
                {
                    Settings.BotInstances.Add(new BotInstance(temp[0].Trim().ToLower(), temp[1].Trim(), accessToken, indexCounter++));
                }
                else if (temp.Length >= 3)
                {
                    Settings.BotInstances.Add(new BotInstance(temp[0].Trim().ToLower(), temp[1].Trim(), accessToken, indexCounter++, activeKey: temp[2].Trim()));
                }
            }

            if (Settings.BotInstances.Count > 0)
            {
                Log.WriteToLog($"Loaded { Settings.BotInstances.Count.ToString().Pastel(Color.Red) } accounts!", Log.LogType.Success);
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
            if (Settings.Threads > Settings.BotInstances.Count)
            {
                Log.WriteToLog($"THREADS is larger than total number of accounts, lowering it to {Settings.BotInstances.Count.ToString().Pastel(Color.Red)}", Log.LogType.Warning);
                Settings.Threads = Settings.BotInstances.Count;
            }

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

            Settings.CardsDetails = Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(Settings.StartupPath + @"/data/cardsDetails.json"));

            Settings.Summoners = new Dictionary<string, string>
            {
                { "260", "fire" },
                { "257", "water" },
                { "437", "water" },
                { "224", "dragon" },
                { "189", "earth" },
                { "145", "death" },
                { "240", "dragon" },
                { "167", "fire" },
                { "438", "death" },
                { "156", "life" },
                { "440", "fire" },
                { "114", "dragon" },
                { "441", "life" },
                { "439", "earth" },
                { "262", "dragon" },
                { "261", "life" },
                { "178", "water" },
                { "258", "death" },
                { "27", "earth" },
                { "38", "life" },
                { "49", "death" },
                { "5", "fire" },
                { "70", "fire" },
                { "73", "life" },
                { "259", "earth" },
                { "74", "death" },
                { "72", "earth" },
                { "442", "dragon" },
                { "71", "water" },
                { "88", "dragon" },
                { "78", "dragon" },
                { "200", "dragon" },
                { "16", "water" },
                { "239", "life" },
                { "254", "water" },
                { "235", "death" },
                { "113", "life" },
                { "109", "death" },
                { "110", "fire" },
                { "291", "dragon" },
                { "278", "earth" },
                { "236", "fire" },
                { "56", "dragon" },
                { "112", "earth" },
                { "111", "water" },
                { "205", "dragon" },
                { "130", "dragon" }
            };

            Settings.LogSummaryList = new List<(int index, string account, string battleResult, string rating, string ECR, string questStatus)>();
            Settings.oHived = new HiveAPI.CS.CHived(Settings._httpClient, "https://api.deathwing.me");
        }

        static void SetStartupPath()
        {
            // Setup startup path
            string path = Assembly.GetExecutingAssembly().Location;
            string directory = Path.GetDirectoryName(path);
            if (directory.Length == 0)
            {
                directory = AppDomain.CurrentDomain.BaseDirectory;
            }
            Settings.StartupPath = directory;
        }
    }
}
