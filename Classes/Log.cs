using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ultimate_Splinterlands_Bot_V2.Classes
{
    class Log
    {
        private static object _ConsoleLock = new object();
        public enum LogType
        {
            Success,
            Information,
            Error,
            CriticalError,
            Warning
        };

        /// <summary>
        /// Writes text to the log
        /// </summary>
        /// <param name="Message">The text to write to the log</param>
        /// <param name="logType">1 = Success / Green, 2 = Information / Default Color, 3 = Error / Red, 4 = Critical Error / DarkViolet, 5 = Warning / Orange, default = Default Color</param>
        public static void WriteToLog(string message, LogType logType = LogType.Information, bool debugOnly = false)
        {
            if (debugOnly && !Settings.DebugMode)
            {
                return;
            }

            string messagePrefix = $"[{ DateTime.Now }] ";

            ConsoleColor textColor;

            switch (logType)
            {
                case LogType.Success:
                    textColor = ConsoleColor.Green;
                    break;
                case LogType.Information:
                    textColor = Console.ForegroundColor;
                    break;
                case LogType.Error:
                    textColor = ConsoleColor.Red;
                    messagePrefix += "Error: ";
                    break;
                case LogType.CriticalError:
                    textColor = ConsoleColor.Magenta;
                    messagePrefix += "Critical Error: ";
                    break;
                case LogType.Warning:
                    textColor = ConsoleColor.Yellow;
                    //messagePrefix += "Warning: ";
                    break;
                default:
                    textColor = Console.ForegroundColor;
                    break;
            }

            lock (_ConsoleLock)
            {
                Console.ForegroundColor = textColor;
                Console.WriteLine(messagePrefix + message);
                Console.ResetColor();

                if (Settings.WriteLogToFile)
                {
                    System.IO.File.AppendAllText(Settings.StartupPath + @"/log.txt", messagePrefix + message + Environment.NewLine);
                }
            }
        }

        /// <summary>
        /// Writes startup information to log
        /// </summary>
        public static void WriteStartupInfoToLog()
        {
            WriteToLog("--------------------------------------------------------------");
            WriteToLog("Ultimate Splinderlands Bot V2 by PC Jones");
            WriteToLog("Join the telegram group https://t.me/ultimatesplinterlandsbot");
            WriteToLog("Join the discord server https://discord.gg/hwSr7KNGs9");
            WriteToLog("               Close this window to stop the bot");
            WriteToLog("-------------------------------------------------------------");
        }

        /// <summary>
        /// Writes support information to log
        /// </summary>
        public static void WriteSupportInformationToLog()
        {
            WriteToLog("-------------------------------------------------------------");
            WriteToLog("Ultimate Splinderlands Bot V2 by PC Jones");
            WriteToLog("Join the telegram group https://t.me/ultimatesplinterlandsbot");
            WriteToLog("Join the discord server https://discord.gg/hwSr7KNGs9");
            WriteToLog("               Close this window to stop the bot");
            WriteToLog("-------------------------------------------------------------");
        }
    }
}
