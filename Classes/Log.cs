using Newtonsoft.Json.Linq;
using Pastel;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
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
            try
            {
                var t = new TablePrinter("#", "Account", "Result", "Rating", "ECR", "QuestStatus");
                Settings.LogSummaryList.ForEach(x => t.AddRow(x.index, x.account, x.battleResult, x.rating, x.ECR, x.questStatus));
                Settings.LogSummaryList.Clear();
                lock (_ConsoleLock)
                {
                    t.Print();
                }
            }
            catch (Exception ex)
            {
                Log.WriteToLog("Error at Battle Summary: " + ex.ToString(), LogType.Error);
            }
        }

        public static void LogTeamToTable(JToken team, int mana, string rulesets)
        {
            var t = new TablePrinter("Mana", "Rulesets", "Quest Prio", "Win %", "Team Rank");
            t.AddRow(mana, rulesets, team["play_for_quest"], (Convert.ToDouble(((string)team["summoner_wins"]).Replace(",", "."), CultureInfo.InvariantCulture) * 100).ToString("N2"), team["teamRank"]);
            lock (_ConsoleLock)
            {
                t.Print();
            }
            t = new TablePrinter("Card", "ID", "Name", "Element");
            t.AddRow("Summoner", (string)team["summoner_id"], (string)Settings.CardsDetails[((int)team["summoner_id"]) - 1]["name"],
            ((string)Settings.CardsDetails[((int)team["summoner_id"]) - 1]["color"])
            .Replace("Red", "Fire").Replace("Blue", "Water").Replace("White", "Life").Replace("Black", "Death").Replace("Green", "Earth").Replace("Gold", "Dragon"));
            for (int i = 1; i < 7; i++)
            {
                if ((string)team[$"monster_{i}_id"] == "")
                {
                    break;
                }
                t.AddRow($"Monster #{i}", (string)team[$"monster_{i}_id"], (string)Settings.CardsDetails[((int)team[$"monster_{i}_id"]) - 1]["name"],
                ((string)Settings.CardsDetails[((int)team[$"monster_{i}_id"]) - 1]["color"])
                .Replace("Red", "Fire").Replace("Blue", "Water").Replace("White", "Life").Replace("Black", "Death").Replace("Green", "Earth")
                .Replace("Gray", "Neutral").Replace("Gold", "Dragon"));
            }

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
            WriteToLog("Ultimate Splinterlands Bot V2 by PC Jones");
            WriteToLog("Join the telegram group https://t.me/ultimatesplinterlandsbot");
            WriteToLog("Join the discord server https://discord.gg/hwSr7KNGs9");
            WriteToLog("               Close this window to stop the bot");
            WriteToLog("   Or write stop and press enter to stop the bot");
            WriteToLog("-------------------------------------------------------------");
        }

        /// <summary>
        /// Writes support information to log
        /// </summary>
        public static void WriteSupportInformationToLog()
        {
            WriteToLog("-------------------------------------------------------------");
            WriteToLog("Ultimate Splinterlands Bot V2 by PC Jones");
            WriteToLog("Join the telegram group https://t.me/ultimatesplinterlandsbot");
            WriteToLog("Join the discord server https://discord.gg/hwSr7KNGs9");
            WriteToLog("-------------------------------------------------------------");
        }
    }
}
