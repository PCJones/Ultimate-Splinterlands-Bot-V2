using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Ultimate_Splinterlands_Bot_V2.Classes;
using Ultimate_Splinterlands_Bot_V2.Classes.Api;
using Ultimate_Splinterlands_Bot_V2.Classes.Config;
using Ultimate_Splinterlands_Bot_V2.Classes.Bots;
using Ultimate_Splinterlands_Bot_V2.Classes.Utils;
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
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                handler = new ConsoleEventDelegate(ConsoleEventCallback);
                SetConsoleCtrlHandler(handler, true);
                if (Environment.OSVersion.Version.Major < 10)
                {
                    Console.WriteLine("Legacy mode for old Windows version activated - please update your Windows to Windows 10 or higher / Windows Server 2016 or higher to get maximum bot speed");
                    Settings.LegacyWindowsMode = true;
                    ConsoleExtensions.Disable();
                }
            }

            Log.WriteStartupInfoToLog();

            Initialize();

            Thread.Sleep(1500); // Sleep 1.5 to display welcome message

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
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

        static void Initialize() {
            if (!Settings.Initialize() || (Settings.UseBrowserMode && !CheckForChromeDriver()) || !Accounts.Initialize())
            {
                Log.WriteToLog("Press any key to close");
                Console.ReadKey();
                Environment.Exit(0);
            }
            Bots.Initialize();
        }

        static async Task BotLoopAsync(CancellationToken token)
        {
            var instances = new HashSet<Task>();
            int nextBrowserInstance = -1;
            int nextBotInstance = -1;
            bool firstRuntrough = true;

            bool logoutNeeded = Settings.UseBrowserMode ? Settings.BotInstancesBrowser.Count != Settings.MaxBrowserInstances : false;

            DateTime[] sleepInfo = new DateTime[Settings.UseLightningMode ? Settings.BotInstancesBlockchain.Count : Settings.BotInstancesBrowser.Count];

            while (!token.IsCancellationRequested)
            {
                while (instances.Count < (Settings.UseBrowserMode ? Settings.MaxBrowserInstances : Settings.Threads) && !token.IsCancellationRequested)
                {
                    try
                    {
                        lock (_TaskLock)
                        {
                            if (++nextBotInstance >= (Settings.UseLightningMode ? Settings.BotInstancesBlockchain.Count : Settings.BotInstancesBrowser.Count))
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

                            if (Settings.UseLightningMode)
                            {
                                while (Settings.BotInstancesBlockchain.All(x => x.CurrentlyActive
                                    || (DateTime)sleepInfo[Settings.BotInstancesBlockchain.IndexOf(x)] > DateTime.Now))
                                {
                                    Thread.Sleep(20000);
                                }
                            }
                            else
                            {
                                while (Settings.BotInstancesBrowser.All(x => x.CurrentlyActive
                                    || (DateTime)sleepInfo[Settings.BotInstancesBrowser.IndexOf(x)] > DateTime.Now))
                                {
                                    Thread.Sleep(20000);
                                }
                            }
                        }

                        lock (_TaskLock)
                        {
                            if (firstRuntrough)
                            {
                                // Delay accounts to avoid them fighting each other
                                Thread.Sleep(Settings._Random.Next(1000, 6000));
                            }

                            if (Settings.UseLightningMode)
                            {
                                while (Settings.BotInstancesBlockchain.ElementAt(nextBotInstance).CurrentlyActive)
                                {
                                    nextBotInstance++;
                                    nextBotInstance = nextBotInstance >= Settings.BotInstancesBlockchain.Count ? 0 : nextBotInstance;
                                }
                                // create local copies for thread safety
                                int botInstance = nextBotInstance;
                                int browserInstance = nextBrowserInstance;

                                instances.Add(Task.Run(async () =>
                                {
                                    var result = await Settings.BotInstancesBlockchain[botInstance].DoBattleAsync(browserInstance, logoutNeeded, botInstance);
                                    lock (_TaskLock)
                                    {
                                        sleepInfo[nextBotInstance] = result;
                                    }
                                }, CancellationToken.None));
                            }
                            else
                            {
                                while (Settings.BotInstancesBrowser.ElementAt(nextBotInstance).CurrentlyActive)
                                {
                                    nextBotInstance++;
                                    nextBotInstance = nextBotInstance >= Settings.BotInstancesBrowser.Count ? 0 : nextBotInstance;
                                }
                                nextBrowserInstance = ++nextBrowserInstance >= Settings.MaxBrowserInstances ? 0 : nextBrowserInstance;
                                while (!Settings.SeleniumInstances.ElementAt(nextBrowserInstance).isAvailable)
                                {
                                    nextBrowserInstance++;
                                    nextBrowserInstance = nextBrowserInstance >= Settings.MaxBrowserInstances ? 0 : nextBrowserInstance;
                                }

                                Settings.SeleniumInstances[nextBrowserInstance] = (Settings.SeleniumInstances[nextBrowserInstance].driver, false);

                                // create local copies for thread safety
                                int botInstance = nextBotInstance;
                                int browserInstance = nextBrowserInstance;

                                instances.Add(Task.Run(async () =>
                                {
                                    var result = await Settings.BotInstancesBrowser[botInstance].DoBattleAsync(browserInstance, logoutNeeded, botInstance);
                                    lock (_TaskLock)
                                    {
                                        sleepInfo[nextBotInstance] = result.sleepTime;
                                        Settings.SeleniumInstances[browserInstance] = (Settings.SeleniumInstances[browserInstance].driver, true);
                                    }
                                }, CancellationToken.None));
                            }
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
            if (Settings.UseBrowserMode)
            {
                _ = Task.Run(() => Parallel.ForEach(Settings.SeleniumInstances, x => x.driver.Quit())).Result;
            }
            Log.WriteToLog("Bot stopped!");
        }

        static bool CheckForChromeDriver()
        {
            var chromeDriverFileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "chromedriver.exe" : "chromedriver";
            if (!File.Exists(Settings.StartupPath + @"/" + chromeDriverFileName))
            {
                Log.WriteToLog("No ChromeDriver installed - please download from https://chromedriver.chromium.org/ and insert .exe into bot folder or use lightning mode", Log.LogType.CriticalError);
                return false;
            }

            return true;
        }

        static bool ConsoleEventCallback(int eventType)
        {
            if (Settings.UseBrowserMode && eventType == 2)
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
