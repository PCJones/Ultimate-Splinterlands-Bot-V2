using HiveAPI.CS;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using Pastel;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static HiveAPI.CS.CHived;

namespace Ultimate_Splinterlands_Bot_V2.Classes
{
    public class BotInstanceBlockchain
    {
        private string Username { get; set; }
        private string PostingKey { get; init; }
        private string ActiveKey { get; init; } // only needed for plugins, not used by normal bot
        private int APICounter { get; set; }
        private int PowerCached { get; set; }
        private int LeagueCached { get; set; }
        private int RatingCached { get; set; }
        private double ECRCached { get; set; }
        private (JToken Quest, JToken QuestLessDetails) QuestCached { get; set; }
        private Card[] CardsCached { get; set; }
        public bool CurrentlyActive { get; private set; }

        private object _activeLock;
        private DateTime SleepUntil;
        private DateTime LastCacheUpdate;
        private LogSummary LogSummary;

        private COperations.custom_json CreateCustomJson(bool activeKey, bool postingKey, string methodName, string json)
        {
            COperations.custom_json customJsonOperation = new COperations.custom_json
            {
                required_auths = activeKey ? new string[] { Username } : new string[0],
                required_posting_auths = postingKey ? new string[] { Username } : new string[0],
                id = methodName,
                json = json
            };
            return customJsonOperation;
        }

        private string GetStringForSplinterlandsAPI(CtransactionData oTransaction)
        {
            string json = JsonConvert.SerializeObject(oTransaction.tx);
            string postData = "signed_tx=" + json.Replace("operations\":[{", "operations\":[[\"custom_json\",{")
                .Replace(",\"opid\":18}", "}]");
            return postData;
        }

        private string StartNewMatch()
        {
            string n = Helper.GenerateRandomString(10);
            string json = "{\"match_type\":\"Ranked\",\"app\":\"" + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";
            
            COperations.custom_json custom_Json = CreateCustomJson(false, true, "sm_find_match", json);

            try
            {
                Log.WriteToLog($"{Username}: Finding match...");
                CtransactionData oTransaction = Settings.oHived.CreateTransaction(new object[] { custom_Json }, new string[] { PostingKey });
                var postData = GetStringForSplinterlandsAPI(oTransaction);
                return HttpWebRequest.WebRequestPost(Settings.CookieContainer, postData, "https://battle.splinterlands.com/battle/battle_tx", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:93.0) Gecko/20100101 Firefox/93.0", "", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at finding match: " + ex.ToString(), Log.LogType.Error);
            }
            return null;
        }

        private async Task SubmitTeam(string trxId, JToken matchDetails)
        {
            try
            {
                int mana = (int)matchDetails["mana_cap"];
                string rulesets = (string)matchDetails["ruleset"];
                string[] inactive = ((string)matchDetails["inactive"]).Split(',');
                // blue, black
                List<string> allowedSplinters = new() { "fire", "water", "earth", "life", "death", "dragon" };
                foreach (string inactiveSplinter in inactive)
                {
                    if (inactiveSplinter.Length == 0)
                    {
                        continue;
                    }
                    switch (inactiveSplinter.ToLower())
                    {
                        case "blue":
                            allowedSplinters.Remove("water");
                            break;
                        case "green":
                            allowedSplinters.Remove("earth");
                            break;
                        case "black":
                            allowedSplinters.Remove("death");
                            break;
                        case "white":
                            allowedSplinters.Remove("life");
                            break;
                        case "gold":
                            allowedSplinters.Remove("dragon");
                            break;
                        case "red":
                            allowedSplinters.Remove("fire");
                            break;
                        default:
                            break;
                    }
                }

                var team = await API.GetTeamFromAPIAsync(mana, rulesets, allowedSplinters.ToArray(), CardsCached, QuestCached.Quest, QuestCached.QuestLessDetails, Username);
                if (team == null || (string)team["summoner_id"] == "")
                {
                    Log.WriteToLog($"{Username}: API didn't find any team - Skipping Account", Log.LogType.CriticalError);
                    SleepUntil = DateTime.Now.AddMinutes(Settings.SleepBetweenBattles / 2);
                }
                Log.LogTeamToTable(team, mana, rulesets);

                string summoner = CardsCached.Where(x => x.card_detail_id == (string)team["summoner_id"]).First().card_long_id;
                string monsters = "";
                for (int i = 0; i < 6; i++)
                {
                    var monster = CardsCached.Where(x => x.card_detail_id == (string)team[$"monster_{i + 1}_id"]).FirstOrDefault();
                    if (monster.card_detail_id.Length == 0)
                    {
                        break;
                    }

                    monsters += "\"" + monster.card_long_id + "\",";
                }
                monsters = monsters[..^1];

                string secret = Helper.GenerateRandomString(10);
                string n = Helper.GenerateRandomString(10);

                string monsterClean = monsters.Replace("\"", "");

                string teamHash = Helper.GenerateMD5Hash(summoner + "," + monsterClean + "," + secret);

                string json = "{\"trx_id\":\"" + trxId + "\",\"team_hash\":\"" + teamHash + "\",\"summoner\":\"" + summoner + "\",\"monsters\":[" + monsters + "],\"secret\":\"" + secret + "\",\"app\":\"" + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";

                COperations.custom_json custom_Json = CreateCustomJson(false, true, "sm_team_reveal", json);

                Log.WriteToLog($"{Username}: Submitting team...");
                CtransactionData oTransaction = Settings.oHived.CreateTransaction(new object[] { custom_Json }, new string[] { PostingKey });
                var postData = GetStringForSplinterlandsAPI(oTransaction);
                var result = HttpWebRequest.WebRequestPost(Settings.CookieContainer, postData, "https://api2.splinterlands.com/battle/battle_tx", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:93.0) Gecko/20100101 Firefox/93.0", "", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at submitting team: " + ex.ToString(), Log.LogType.Error);
            }
        }

        private async Task<JToken> WaitForMatchDetails(string trxId)
        {
            for (int i = 0; i < 10; i++) // 10 * 20 = 200seconds, so 3mins + 20sec 
            {
                try
                {
                    await Task.Delay(7500);
                    JToken matchDetails = JToken.Parse(await Helper.DownloadPageAsync("https://api.splinterlands.io/battle/status?id=" + trxId));
                    if (i > 2 && ((string)matchDetails).Contains("no battle"))
                    {
                        Log.WriteToLog($"{Username}: Error at waiting for match details: " + matchDetails.ToString(), Log.LogType.Error);
                        return null;
                    }
                    if (matchDetails["mana_cap"].Type != JTokenType.Null)
                    {
                        return matchDetails;
                    }
                    await Task.Delay(12500);
                }
                catch (Exception ex)
                {
                    if (i > 9)
                    {
                        Log.WriteToLog($"{Username}: Error at waiting for match details: " + ex.ToString(), Log.LogType.Error);
                    }
                }
            }
            return null;
        }
        public BotInstanceBlockchain(string username, string password, int index, string key = "")
        {
            Username = username;
            PostingKey = password;
            ActiveKey = key;
            SleepUntil = DateTime.Now.AddMinutes((Settings.SleepBetweenBattles + 1) * -1);
            LastCacheUpdate = DateTime.MinValue;
            LogSummary = new LogSummary(index, username);
            _activeLock = new object();
            APICounter = 100;
        }

        public async Task<DateTime> DoBattleAsync(int browserInstance, bool logoutNeeded, int botInstance)
        {
            lock (_activeLock)
            {
                if (CurrentlyActive)
                {
                    Log.WriteToLog($"{Username} Skipped account because it is currently active", debugOnly: true);
                    return DateTime.Now.AddSeconds(30);
                }
                CurrentlyActive = true;
            }
            try
            {
                if (Username.Contains("@"))
                {
                    Log.WriteToLog($"{Username}: Skipping account, fast lightning blockchain mode works if you login via username:posting_key", Log.LogType.Error);
                    SleepUntil = DateTime.Now.AddMinutes(180);
                    return SleepUntil;
                }
                LogSummary.Reset();
                if (SleepUntil > DateTime.Now)
                {
                    Log.WriteToLog($"{Username}: is sleeping until {SleepUntil.ToString().Pastel(Color.Red)}");
                    return SleepUntil;
                }

                APICounter++;
                if (APICounter >= 6 || (DateTime.Now - LastCacheUpdate).TotalMinutes >= 40)
                {
                    APICounter = 0;
                    LastCacheUpdate = DateTime.Now;
                    var playerDetails = await API.GetPlayerDetailsAsync(Username);
                    PowerCached = playerDetails.power;
                    RatingCached = playerDetails.rating;
                    LeagueCached = playerDetails.league;
                    QuestCached = await API.GetPlayerQuestAsync(Username);
                    CardsCached = await API.GetPlayerCardsAsync(Username);
                    if (Settings.UsePrivateAPI)
                    {
                        API.UpdateCardsForPrivateAPI(Username, CardsCached);
                    }
                    ECRCached = await GetECRFromAPI();
                } else if (APICounter % 3 == 0) {
                    if ((int)QuestCached.Quest["completed"] != 5)
                    {
                        QuestCached = await API.GetPlayerQuestAsync(Username);
                    }
                    ECRCached = await GetECRFromAPI();
                }

                LogSummary.Rating = RatingCached.ToString();
                LogSummary.ECR=  ECRCached.ToString();

                Log.WriteToLog($"{Username}: Deck size: {(CardsCached.Length - 1).ToString().Pastel(Color.Red)} (duplicates filtered)"); // Minus 1 because phantom card array has an empty string in it
                Log.WriteToLog($"{Username}: Quest details: {JsonConvert.SerializeObject(QuestCached.QuestLessDetails).Pastel(Color.Yellow)}");

                AdvanceLeague();
                RequestNewQuestViaAPI();
                ClaimQuestReward();

                Log.WriteToLog($"{Username}: Current Energy Capture Rate is { (ECRCached >= 50 ? ECRCached.ToString("N3").Pastel(Color.Green) : ECRCached.ToString("N3").Pastel(Color.Red)) }%");
                if (ECRCached < Settings.ECRThreshold)
                {
                    Log.WriteToLog($"{Username}: ERC is below threshold of {Settings.ECRThreshold}% - skipping this account.", Log.LogType.Warning);
                    SleepUntil = DateTime.Now.AddMinutes(5);
                    await Task.Delay(1500); // Short delay to not spam splinterlands api
                    return SleepUntil;
                }

                string trxId = StartNewMatch();
                if (trxId == null || !trxId.Contains("success"))
                {
                    Log.WriteToLog($"{Username}: Creating match was not successful: " + trxId, Log.LogType.Warning);
                    SleepUntil = DateTime.Now.AddMinutes(Settings.SleepBetweenBattles / 2);
                    return SleepUntil;
                }
                Log.WriteToLog($"{Username}: Splinterlands Response: {trxId}");
                trxId = Helper.DoQuickRegex("id\":\"(.*?)\"", trxId);

                SleepUntil = DateTime.Now.AddMinutes(Settings.SleepBetweenBattles >= 3 ? Settings.SleepBetweenBattles : 3);

                JToken matchDetails = await WaitForMatchDetails(trxId);
                if (matchDetails == null)
                {
                    SleepUntil = DateTime.Now.AddMinutes(Settings.SleepBetweenBattles / 2);
                    return SleepUntil;
                }
                await SubmitTeam(trxId, matchDetails);
                Log.WriteToLog($"{Username}: Finished battle!");

                // todo: determine winner, show summary etc

                await Task.Delay(5000);
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: {ex}{Environment.NewLine}Skipping Account", Log.LogType.CriticalError);
            }
            finally
            {
                Settings.LogSummaryList.Add((LogSummary.Index, LogSummary.Account, LogSummary.BattleResult, LogSummary.Rating, LogSummary.ECR, LogSummary.QuestStatus));
                lock (_activeLock)
                {
                    CurrentlyActive = false;
                }
            }
            return SleepUntil;
        }

        private void ClaimQuestReward()
        {
            try
            {
                string logText;
                if ((int)QuestCached.Quest["total_items"] == (int)QuestCached.Quest["completed_items"]
                    && QuestCached.Quest["rewards"].Type == JTokenType.Null)
                {
                    logText = "Quest reward can be claimed";
                    Log.WriteToLog($"{Username}: {logText.Pastel(Color.Green)}");
                    // short logText:
                    logText = "Quest reward available!";
                    if (Settings.ClaimQuestReward)
                    {
                        if (Settings.DontClaimQuestNearHigherLeague)
                        {
                            if (RatingCached == -1)
                            {
                                return;
                            }

                            int rating = RatingCached;
                            bool waitForHigherLeague = (rating is >= 300 and < 400) && (PowerCached is >= 1000 || (Settings.RentalBotActivated && Settings.DesiredRentalPower >= 1000)) || // bronze 2
                                (rating is >= 600 and < 700) && (PowerCached is >= 5000 || (Settings.RentalBotActivated && Settings.DesiredRentalPower >= 5000)) || // bronze 1 
                                (rating is >= 840 and < 1000) && (PowerCached is >= 15000 || (Settings.RentalBotActivated && Settings.DesiredRentalPower >= 15000)) || // silver 3
                                (rating is >= 1200 and < 1300) && (PowerCached is >= 40000 || (Settings.RentalBotActivated && Settings.DesiredRentalPower >= 40000)) || // silver 2
                                (rating is >= 1500 and < 1600) && (PowerCached is >= 70000 || (Settings.RentalBotActivated && Settings.DesiredRentalPower >= 70000)) || // silver 1
                                (rating is >= 1800 and < 1900) && (PowerCached is >= 100000 || (Settings.RentalBotActivated && Settings.DesiredRentalPower >= 100000)); // gold 

                            if (waitForHigherLeague)
                            {
                                Log.WriteToLog($"{Username}: Don't claim quest - wait for higher league");
                                return;
                            }
                        }

                        string n = Helper.GenerateRandomString(10);
                        string json = "{\"type\":\"quest\",\"quest_id\":\"" + (string)QuestCached.Quest["id"] +"\",\"app\":\"" + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";

                        COperations.custom_json custom_Json = CreateCustomJson(false, true, "sm_claim_reward", json);

                        string tx = Settings.oHived.broadcast_transaction(new object[] { custom_Json }, new string[] { PostingKey });
                        Log.WriteToLog($"{Username}: { "Claimed quest reward:".Pastel(Color.Green) } {tx}");
                        //CtransactionData oTransaction = Settings.oHived.CreateTransaction(new object[] { custom_Json }, new string[] { PostingKey });
                        //var postData = GetStringForSplinterlandsAPI(oTransaction);
                        //string response = HttpWebRequest.WebRequestPost(Settings.CookieContainer, postData, "https://broadcast.splinterlands.com/send", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:93.0) Gecko/20100101 Firefox/93.0", "", Encoding.UTF8);

                        APICounter = 100; // set api counter to 100 to reload quest
                    }
                }
                else
                {
                    Log.WriteToLog($"{Username}: No quest reward to be claimed");
                    // short logText:
                    logText = "No quest reward...";
                }

                LogSummary.QuestStatus = $" { (string)QuestCached.Quest["completed_items"] }/{ (string)QuestCached.Quest["total_items"] } {logText}";
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at claiming quest rewards: {ex}", Log.LogType.Error);
            }
        }

        private async Task<double> GetECRFromAPI()
        {
            var balanceInfo = ((JArray)await API.GetPlayerBalancesAsync(Username)).Where(x => (string)x["token"] == "ECR").First();
            var captureRate = (int)balanceInfo["balance"];
            DateTime lastRewardTime = (DateTime)balanceInfo["last_reward_time"];
            double ecrRegen = 0.0868;
            double ecr = captureRate + (new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds() - new DateTimeOffset(lastRewardTime).ToUnixTimeMilliseconds()) / 3000 * ecrRegen;
            return Math.Min(ecr, 10000) / 100;
        }

        private void AdvanceLeague()
        {
            try
            {
                if (!Settings.AdvanceLeague || RatingCached == -1|| RatingCached < 1000)
                {
                    return;
                }

                int highestPossibleLeage = GetMaxLeagueByRankAndPower();
                if (highestPossibleLeage > LeagueCached)
                {
                    Log.WriteToLog($"{Username}: { "Advancing to higher league!".Pastel(Color.Green)}");
                    APICounter = 100; // set api counter to 100 to reload details

                    string n = Helper.GenerateRandomString(10);
                    string json = "{\"notify\":\"true\",\"app\":\"" + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";

                    COperations.custom_json custom_Json = CreateCustomJson(false, true, "sm_advance_league", json);

                    string tx = Settings.oHived.broadcast_transaction(new object[] { custom_Json }, new string[] { PostingKey });
                    Log.WriteToLog($"{Username}: { "Advanced league: ".Pastel(Color.Green) } {tx}");
                }

            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at advancing league: {ex}");
            }
        }

        private static string GetSummonerColor(string id)
        {
            return (string)Settings.CardsDetails[Convert.ToInt32(id) - 1]["color"];
        }

        private void RequestNewQuestViaAPI()
        {
            try
            {
                if (Settings.BadQuests.Contains((string)QuestCached.QuestLessDetails["splinter"])
                    && (int)QuestCached.QuestLessDetails["completed"] == 0)
                {
                    string n = Helper.GenerateRandomString(10);
                    string json = "{\"type\":\"daily\",\"app\":\"" + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";

                    COperations.custom_json custom_Json = CreateCustomJson(false, true, "sm_refresh_quest", json);

                    string tx = Settings.oHived.broadcast_transaction(new object[] { custom_Json }, new string[] { PostingKey });
                    Log.WriteToLog($"{Username}: Requesting new quest because of bad quest: {tx}");
                    APICounter = 100; // set api counter to 100 to reload quest
                } else if (QuestCached.Quest["claim_trx_id"].Type != JTokenType.Null
                    && (DateTime.Now - ((DateTime)QuestCached.Quest["created_date"]).ToLocalTime()).TotalHours > 23)
                {
                    string n = Helper.GenerateRandomString(10);
                    string json = "{\"type\":\"daily\",\"app\":\"" + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";

                    COperations.custom_json custom_Json = CreateCustomJson(false, true, "sm_start_quest", json);

                    string tx = Settings.oHived.broadcast_transaction(new object[] { custom_Json }, new string[] { PostingKey });
                    Log.WriteToLog($"{Username}: Requesting new quest because 23 hours passed: {tx}");
                    APICounter = 100; // set api counter to 100 to reload quest
                }
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at changing quest: {ex}", Log.LogType.Error);
            }
        }
      
        private int GetMaxLeagueByRankAndPower()
        {
            // bronze
            if ((RatingCached is >= 100 and <= 399) && (PowerCached is >= 0))
            {
                return 1;
            }
            if ((RatingCached is >= 400 and <= 699) && (PowerCached is >= 1000))
            {
                return 2;
            }
            if ((RatingCached is >= 700 and <= 999) && (PowerCached is >= 5000))
            {
                return 3;
            }
            // silver
            if ((RatingCached is >= 1000 and <= 1299) && (PowerCached is >= 15000))
            {
                return 4;
            }
            if ((RatingCached is >= 1300 and <= 1599) && (PowerCached is >= 40000))
            {
                return 5;
            }
            if ((RatingCached is >= 1600 and <= 1899) && (PowerCached is >= 70000))
            {
                return 6;
            }
            // gold
            if ((RatingCached is >= 1900 and <= 2199) && (PowerCached is >= 100000))
            {
                return 7;
            }
            if ((RatingCached is >= 2200 and <= 2499) && (PowerCached is >= 150000))
            {
                return 8;
            }
            if ((RatingCached is >= 2500 and <= 2799) && (PowerCached is >= 200000))
            {
                return 9;
            }

            return 0;
        }
    }
}
