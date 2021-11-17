using HiveAPI.CS;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using Pastel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public bool RankedBanned { get; private set; }
        public int RankedBanCounter { get; private set; }

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

        private async Task<bool> WaitForEnemyPick(string tx, Stopwatch stopwatch)
        {
            int counter = 0;
            do
            {
                var enemyHasPicked = await API.CheckEnemyHasPickedAsync(Username, tx);
                if (enemyHasPicked.enemyHasPicked)
                {
                    return enemyHasPicked.surrender;
                }
                if (!Settings.DontShowWaitingLog)
                {
                    Log.WriteToLog($"{Username}: Waiting 15 seconds for enemy to pick #{++counter}");
                }
                await Task.Delay(stopwatch.Elapsed.TotalSeconds > 170 ? 2500 : 15000);
            } while (stopwatch.Elapsed.TotalSeconds < 183);
            return false;
        }

        private void SubmitTeam(string trxId, JToken matchDetails, JToken team)
        {
            try
            {
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
            RankedBanned = false;
            RankedBanCounter = 0;
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
                    Log.WriteToLog($"{Username}: Skipping account, fast lightning blockchain mode works only if you login via username:posting_key", Log.LogType.Error);
                    SleepUntil = DateTime.Now.AddMinutes(180);
                    return SleepUntil;
                }
                LogSummary.Reset();
                if (SleepUntil > DateTime.Now)
                {
                    Log.WriteToLog($"{Username}: is sleeping until {SleepUntil.ToString().Pastel(Color.Red)}");
                    return SleepUntil;
                }

                if (RankedBanned && Settings.AutoUnban)
                {
                    Log.WriteToLog($"{Username}: UNBAN Mode!", Log.LogType.Warning);
                    BotInstanceBrowser instance = new BotInstanceBrowser(Username, PostingKey, 0);
                    bool battleFailed = (await instance.DoBattleAsync(-1, false, -1, true)).battleFailed;
                    if (!battleFailed)
                    {
                        if (++RankedBanCounter > 3)
                        {
                            Log.WriteToLog($"{Username}: Account unbanned!", Log.LogType.Success);
                            SleepUntil = DateTime.Now.AddMinutes(Settings.SleepBetweenBattles);
                            RankedBanCounter = 0;
                            RankedBanned = false;
                            return SleepUntil;
                        }
                    }
                    SleepUntil = DateTime.Now.AddMinutes(61);
                    return SleepUntil;
                }

                if (Settings.RentalBotActivated && Convert.ToBoolean(Settings.RentalBotMethodIsAvailable.Invoke(Settings.RentalBot.Unwrap(), new object[] { })))
                {
                    Settings.RentalBotMethodSetActive.Invoke(Settings.RentalBot.Unwrap(), new object[] { true });
                    try
                    {
                        Log.WriteToLog($"{Username}: Starting rental bot!");
                        Settings.RentalBotMethodCheckRentals.Invoke(Settings.RentalBot.Unwrap(), new object[] { null, Settings.MaxRentalPricePer500, Settings.DesiredRentalPower, Settings.DaysToRent, Username, ActiveKey });
                    }
                    catch (Exception ex)
                    {
                        Log.WriteToLog($"{Username}: Error at rental bot: {ex}", Log.LogType.CriticalError);
                    }
                    finally
                    {
                        Settings.RentalBotMethodSetActive.Invoke(Settings.RentalBot.Unwrap(), new object[] { false });
                    }
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
                    ECRCached = await GetECRFromAPIAsync();
                }
                else if (APICounter % 3 == 0)
                {
                    if ((int)QuestCached.Quest["completed"] != 5)
                    {
                        QuestCached = await API.GetPlayerQuestAsync(Username);
                    }
                    ECRCached = await GetECRFromAPIAsync();
                }

                LogSummary.Rating = RatingCached.ToString();
                LogSummary.ECR = ECRCached.ToString();

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


                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                string trxId = StartNewMatch();
                if (trxId == null || !trxId.Contains("success"))
                {
                    int sleepTime = 5;
                    Log.WriteToLog($"{Username}: Creating match was not successful: " + trxId, Log.LogType.Warning);
                    Log.WriteToLog($"{Username}: Sleeping for { sleepTime } minutes", Log.LogType.Warning);
                    SleepUntil = DateTime.Now.AddMinutes(sleepTime);
                    return SleepUntil;
                }
                Log.WriteToLog($"{Username}: Splinterlands Response: {trxId}");
                trxId = Helper.DoQuickRegex("id\":\"(.*?)\"", trxId);

                SleepUntil = DateTime.Now.AddMinutes(Settings.SleepBetweenBattles);

                JToken matchDetails = await WaitForMatchDetails(trxId);
                if (matchDetails == null)
                {
                    Log.WriteToLog($"{Username}: Banned from ranked? Sleeping for 30 minutes!", Log.LogType.Warning);
                    SleepUntil = DateTime.Now.AddMinutes(30);
                    RankedBanned = true;
                    return SleepUntil;
                }

                JToken team = await GetTeamAsync(matchDetails);
                if (team == null)
                {
                    Log.WriteToLog($"{Username}: API didn't find any team - Skipping Account", Log.LogType.CriticalError);
                    SleepUntil = DateTime.Now.AddMinutes(Settings.SleepBetweenBattles / 2);
                }

                var surrender = await WaitForEnemyPick(trxId, stopwatch);
                stopwatch.Stop();
                if (!surrender)
                {
                    SubmitTeam(trxId, matchDetails, team);
                }
                else
                {
                    Log.WriteToLog($"{Username}: Looks like enemy surrendered - don't submit a team", Log.LogType.Warning);
                }

                Log.WriteToLog($"{Username}: Finished battle!");

                if (Settings.ShowBattleResults)
                {
                    await ShowBattleResult(trxId);
                }
                else
                {
                    await Task.Delay(1000);
                }
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

        private async Task<JToken> GetTeamAsync(JToken matchDetails)
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

                JToken team = await API.GetTeamFromAPIAsync(mana, rulesets, allowedSplinters.ToArray(), CardsCached, QuestCached.Quest, QuestCached.QuestLessDetails, Username);
                if (team == null || (string)team["summoner_id"] == "")
                {
                    return null;
                }

                Log.WriteToLog($"{Username}: API Response:");
                Log.LogTeamToTable(team, mana, rulesets);
                return team;
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at requesting team: {ex}", Log.LogType.Error);
                return null;
            }
        }

        private async Task ShowBattleResult(string tx)
        {
            (int newRating, int ratingChange, decimal decReward, int result) battleResult = new();
            for (int i = 0; i < 14; i++)
            {
                await Task.Delay(6000);
                battleResult = await API.GetBattleResultAsync(Username, tx);
                if (battleResult.result >= 0)
                {
                    break;
                }
                if (!Settings.DontShowWaitingLog)
                {
                    Log.WriteToLog($"{Username}: Waiting 15 seconds for battle result #{i + 1}/14");
                }
                await Task.Delay(9000);
            }

            if (battleResult.result == -1)
            {
                Log.WriteToLog($"{Username}: Could not get battle result");
                return;
            }

            string logTextBattleResult = "";

            switch (battleResult.result)
            {
                case 2:
                    logTextBattleResult = "DRAW";
                    Log.WriteToLog($"{Username}: { logTextBattleResult}");
                    Log.WriteToLog($"{Username}: Rating has not changed ({ battleResult.newRating })");
                    break;
                case 1:
                    logTextBattleResult = $"You won! Reward: { battleResult.decReward } DEC";
                    Log.WriteToLog($"{Username}: { logTextBattleResult.Pastel(Color.Green) }");
                    Log.WriteToLog($"{Username}: New rating is { battleResult.newRating } ({ ("+" + battleResult.ratingChange.ToString()).Pastel(Color.Green) })");
                    break;
                case 0:
                    logTextBattleResult = $"You lost :(";
                    Log.WriteToLog($"{Username}: { logTextBattleResult.Pastel(Color.Red) }");
                    Log.WriteToLog($"{Username}: New rating is { battleResult.newRating } ({ battleResult.ratingChange.ToString().Pastel(Color.Red) })");
                    //API.ReportLoss(winner, Username); disabled for now
                    break;
                default:
                    break;
            }

            LogSummary.Rating = $"{ battleResult.newRating } ({ battleResult.ratingChange })";
            LogSummary.BattleResult = logTextBattleResult;
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

        private async Task<double> GetECRFromAPIAsync()
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
