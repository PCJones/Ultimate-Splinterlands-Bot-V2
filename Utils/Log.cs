using Newtonsoft.Json.Linq;
using Pastel;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using Ultimate_Splinterlands_Bot_V2.Config;

namespace Ultimate_Splinterlands_Bot_V2.Utils
{
    class Log
    {
        private static readonly object _ConsoleLock = new();
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
                var t = new TablePrinter("#", "Account", "Result", "Rating", "ECR", "Focus");
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
            var t = new TablePrinter("Mana", "Rulesets", "Quest Prio", "Win %", "Owned Cards", "Team Rank", "Card Settings");

            string winRate;
            string sortWinRate;
            if (team["winrate"] == null)
            {
                // v1 api fallback
                winRate = (Convert.ToDouble(((string)team["summoner_wins"]).Replace(",", "."), CultureInfo.InvariantCulture) * 100).ToString("N2");
                sortWinRate = "";
            }
            else
            {
                winRate = (Convert.ToDouble(((string)team["winrate"]).Replace(",", "."), CultureInfo.InvariantCulture) * 100).ToString("N2");
                sortWinRate = (Convert.ToDouble(((string)team["sort_winrate"]).Replace(",", "."), CultureInfo.InvariantCulture) * 100).ToString("N2");
            }
            string ownedCards = (string)team["owned_cards"] + "/" + (string)team["total_cards"];

            t.AddRow(mana, rulesets, team["play_for_quest"], $"{winRate} ({sortWinRate})", ownedCards, team["teamRank"], team["card_settings"]);
            lock (_ConsoleLock)
            {
                t.Print();
            }
            t = new TablePrinter("Card", "ID", "Name", "Element");
            t.AddRow("Summoner", (string)team["summoner_id"], Settings.CardsDetails[((int)team["summoner_id"]) - 1].name,
            Settings.CardsDetails[((int)team["summoner_id"]) - 1].GetCardColor(false));
            for (int i = 1; i < 7; i++)
            {
                if ((string)team[$"monster_{i}_id"] == "")
                {
                    break;
                }
                t.AddRow($"Monster #{i}", (string)team[$"monster_{i}_id"], Settings.CardsDetails[((int)team[$"monster_{i}_id"]) - 1].name,
                Settings.CardsDetails[((int)team[$"monster_{i}_id"]) - 1].GetCardColor(false));
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
