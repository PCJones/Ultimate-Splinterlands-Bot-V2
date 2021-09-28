using Pastel;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
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

            Color textColor;

            switch (logType)
            {
                case LogType.Success:
                    textColor = Color.Green;
                    break;
                case LogType.Information:
                    textColor = Color.LightGray;
                    break;
                case LogType.Error:
                    textColor = Color.Red;
                    messagePrefix += "Error: ".Pastel(textColor);
                    break;
                case LogType.CriticalError:
                    textColor = Color.Magenta;
                    messagePrefix += "Critical Error: ".Pastel(textColor);
                    break;
                case LogType.Warning:
                    textColor = Color.Yellow;
                    messagePrefix += "Warning: ".Pastel(textColor);
                    break;
                default:
                    textColor = Color.LightGray;
                    break;
            }

            lock (_ConsoleLock)
            {
                Console.WriteLine(messagePrefix + message.Pastel(textColor));

                if (Settings.WriteLogToFile)
                {
                    System.IO.File.AppendAllText(Settings.StartupPath + @"/log.txt", messagePrefix + message + Environment.NewLine);
                }
            }
        }

        public static void LogBattleSummaryToTable()
        {
            var t = new TablePrinter("#", "Account", "Result", "Rating", "ECR", "QuestStatus");
            Settings.LogSummaryList.ForEach(x => t.AddRow(x.index, x.account, x.battleResult, x.rating, x.ECR, x.questStatus));
            Settings.LogSummaryList.Clear();
            lock (_ConsoleLock)
            {
                t.Print();
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
