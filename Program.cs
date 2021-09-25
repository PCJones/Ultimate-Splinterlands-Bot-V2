using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Ultimate_Splinterlands_Bot_V2.Classes;

namespace Ultimate_Splinterlands_Bot_V2
{
    class Program
    {
        static void Main(string[] args)
        {
            Log.WriteStartupInfoToLog();
            CheckForChromeDriver();
            Initialize();

            _ = Task.Run(async () => await BotLoop()).ConfigureAwait(false);
        }

        static async Task BotLoop()
        {
            var instances = new HashSet<Task>();
            int nextBotInstance = 0;
            for (int i = 0; i < Settings.MaxBrowserInstances; i++)
            {
                instances.Add(Settings.BotInstances[i].DoBattleAsync());
                Settings.SeleniumInstances[i] = SeleniumAddons.CreateSeleniumInstance();
                nextBotInstance++;
            }

            while (true)
            {
                Task t = await Task.WhenAny(instances);
                instances.Remove(t);
                nextBotInstance = nextBotInstance >= Settings.BotInstances.Length ? 0 : nextBotInstance;
                instances.Add(Settings.BotInstances[nextBotInstance++].DoBattleAsync());
            }
        }

        static void Initialize()
        {
            // Setup startup path
            string path = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string directory = System.IO.Path.GetDirectoryName(path);
            Settings.StartupPath = directory;

            // todo
            Settings.DebugMode = true;

            if (Settings.MaxBrowserInstances > Settings.BotInstances.Length)
            {
                Settings.MaxBrowserInstances = Settings.BotInstances.Length;
            }
        }

        static void ReadConfig()
        {

        }
        static void CheckForChromeDriver()
        {
            if (!File.Exists(Settings.StartupPath + @"/chromedriver.exe"))
            {
                Log.WriteToLog("No ChromeDriver installed - please download from https://chromedriver.chromium.org/ and insert .exe into bot folder", Log.LogType.CriticalError);
                Log.WriteToLog("Press any key to close");
                Console.ReadKey();
            }
        }
    }
}
