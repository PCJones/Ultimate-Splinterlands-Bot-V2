using System;
using System.IO;
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
        }

        static void Initialize()
        {
            // Setup startup path
            string path = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string directory = System.IO.Path.GetDirectoryName(path);
            Settings.StartupPath = directory;

            // todo
            Settings.DebugMode = true;
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
