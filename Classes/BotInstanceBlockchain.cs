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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Websocket.Client;
using static HiveAPI.CS.CHived;

namespace Ultimate_Splinterlands_Bot_V2.Classes
{
    public class BotInstanceBlockchain
    {
        private string Username { get; set; }
        private string PostingKey { get; init; }
        private string ActiveKey { get; init; } // only needed for plugins, not used by normal bot
        private string AccessToken { get; init; } // used for websocket authentication
        private int APICounter { get; set; }
        private int PowerCached { get; set; }
        private int LeagueCached { get; set; }
        private int RatingCached { get; set; }
        private double ECRCached { get; set; }
        private (JToken Quest, JToken QuestLessDetails) QuestCached { get; set; }
        private Card[] CardsCached { get; set; }
        private Dictionary<GameState, JToken> GameStates { get; set; }
        public bool CurrentlyActive { get; private set; }

        private object _activeLock;
        private DateTime SleepUntil;
        private DateTime LastCacheUpdate;
        private LogSummary LogSummary;

        private void HandleWebsocketMessage(ResponseMessage message)
        {
            if (message.MessageType != System.Net.WebSockets.WebSocketMessageType.Text
                || !message.Text.Contains("\"id\""))
            {
                return;
            }
            JToken json = JToken.Parse(message.Text);
            if (Enum.TryParse(json["id"].ToString(), out GameState state))
            {
                if (GameStates.ContainsKey(state))
                {
                    GameStates[state] = json["data"];
                }
                else
                {
                    GameStates.Add(state, json["data"]);
                }
            }
            else if(json["data"]["trx_info"] != null 
                && !(bool)json["data"]["trx_info"]["success"])
            {
                Log.WriteToLog($"{Username}: Transaction error: " + message.Text, Log.LogType.Warning);
            }
            else
            {
                Log.WriteToLog($"{Username}: UNKNOWN Message received: {message.Text}", Log.LogType.Warning);
            }
            
            Log.WriteToLog($"{Username}: Message received: {message.Text}", debugOnly: true);
        }

        private async Task<bool> WaitForGameState(GameState state, int secondsToWait = 0)
        {
            int maxI = secondsToWait > 0 ? secondsToWait : 1;
            for (int i = 0; i < maxI; i++)
            {
                if (secondsToWait > 0)
                {
                    await Task.Delay(1000);
                }
                if (GameStates.ContainsKey(state))
                {
                    return true;
                }
            }
            return false;
        }
            
        private async Task<bool> WaitForTransactionSuccess(string tx, int secondsToWait)
        {
            if (tx.Length == 0)
            {
                return false;
            } else if(Settings.LegacyWindowsMode)
            {
                return true;
            }

            for (int i = 0; i < secondsToWait * 2; i++)
            {
                await Task.Delay(500);
                if (GameStates.ContainsKey(GameState.transaction_complete) 
                    && (string)GameStates[GameState.transaction_complete]["trx_info"]["id"] == tx)
                {
                    if ((bool)GameStates[GameState.transaction_complete]["trx_info"]["success"])
                    {
                        return true;
                    }
                    else
                    {
                        Log.WriteToLog($"{Username}: Transaction error: " + tx + " - " + (string)GameStates[GameState.transaction_complete]["trx_info"]["error"], Log.LogType.Warning);
                        return false;
                    }
                }
            }
            return false;
        }

        private async Task WebsocketPingLoop(IWebsocketClient wsClient)
        {
            while (CurrentlyActive)
            {
                try
                {
                    await Task.Delay(60 * 1000);
                    Log.WriteToLog($"{Username}: ping", debugOnly: true);
                    wsClient.Send("{\"type\":\"ping\"}");
                }
                catch (Exception)
                {
                }
            }
        }

        private void WebsocketAuthenticate(IWebsocketClient wsClient)
        {
            string sessionID = Helper.GenerateRandomString(10);
            string message = "{\"type\":\"auth\",\"player\":\"" + Username + "\",\"access_token\":\"" + AccessToken + "\",\"session_id\":\"" + sessionID + "\"}";
            wsClient.Send(message);
        }
        private COperations.custom_json CreateCustomJson(bool activeKey, bool postingKey, string methodName, string json)
        {
            COperations.custom_json customJsonOperation = new COperations.custom_json
            {
                required_auths = activeKey ? new string[] { Username } : Array.Empty<string>(),
                required_posting_auths = postingKey ? new string[] { Username } : Array.Empty<string>(),
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
            return "";
        }

        private async Task<bool> WaitForEnemyPick(string tx, Stopwatch stopwatch)
        {
            int counter = 0;
            do
            {
                var enemyHasPicked = await SplinterlandsAPI.CheckEnemyHasPickedAsync(Username, tx);
                if (enemyHasPicked.enemyHasPicked)
                {
                    return enemyHasPicked.surrender;
                }
                Log.WriteToLog($"{Username}: Waiting 15 seconds for enemy to pick #{++counter}");
                await Task.Delay(stopwatch.Elapsed.TotalSeconds > 170 ? 2500 : 15000);
            } while (stopwatch.Elapsed.TotalSeconds < 179);
            return false;
        }

        private async Task<(string secret, string tx)> SubmitTeamAsync(string tx, JToken matchDetails, JToken team, bool secondTry = false)
        {
            try
            {
                string summoner = CardsCached.Where(x => x.card_detail_id == (string)team["summoner_id"]).First().card_long_id;
                string monsters = "";
                for (int i = 0; i < 6; i++)
                {
                    var monster = CardsCached.Where(x => x.card_detail_id == (string)team[$"monster_{i + 1}_id"]).FirstOrDefault();
                    if (monster == null)
                    {
                        if (Settings.UsePrivateAPI && !secondTry)
                        {
                            Log.WriteToLog($"{Username}: Requesting team from public API - private API needs card update!", Log.LogType.Warning);
                            CardsCached = await SplinterlandsAPI.GetPlayerCardsAsync(Username);
                            team = await GetTeamAsync(matchDetails, ignorePrivateAPI: true);
                            if (Settings.UsePrivateAPI)
                            {
                                BattleAPI.UpdateCardsForPrivateAPI(Username, CardsCached);
                            }
                            return await SubmitTeamAsync(tx, matchDetails, team, secondTry: true);
                        }
                        else
                        {
                            continue;
                        }
                    }
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

                string json = "{\"trx_id\":\"" + tx + "\",\"team_hash\":\"" + teamHash + "\",\"app\":\"" + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";

                COperations.custom_json custom_Json = CreateCustomJson(false, true, "sm_submit_team", json);

                Log.WriteToLog($"{Username}: Submitting team...");
                CtransactionData oTransaction = Settings.oHived.CreateTransaction(new object[] { custom_Json }, new string[] { PostingKey });
                var postData = GetStringForSplinterlandsAPI(oTransaction);
                var response = HttpWebRequest.WebRequestPost(Settings.CookieContainer, postData, "https://battle.splinterlands.com/battle/battle_tx", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:93.0) Gecko/20100101 Firefox/93.0", "https://splinterlands.com/", Encoding.UTF8);
                string responseTx = Helper.DoQuickRegex("id\":\"(.*?)\"", response);
                return (secret, responseTx);
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at submitting team: " + ex.ToString(), Log.LogType.Error);
                // update cards for private API
                APICounter = 100; 
            }
            return ("", "");
        }

        private void RevealTeam(string tx, JToken matchDetails, JToken team, string secret)
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

                //string secret = Helper.GenerateRandomString(10);
                string n = Helper.GenerateRandomString(10);

                string monsterClean = monsters.Replace("\"", "");

                //string teamHash = Helper.GenerateMD5Hash(summoner + "," + monsterClean + "," + secret);

                string json = "{\"trx_id\":\"" + tx + "\",\"summoner\":\"" + summoner + "\",\"monsters\":[" + monsters + "],\"secret\":\"" + secret + "\",\"app\":\"" + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";

                COperations.custom_json custom_Json = CreateCustomJson(false, true, "sm_team_reveal", json);

                Log.WriteToLog($"{Username}: Revealing team...");
                CtransactionData oTransaction = Settings.oHived.CreateTransaction(new object[] { custom_Json }, new string[] { PostingKey });
                var postData = GetStringForSplinterlandsAPI(oTransaction);
                var response = HttpWebRequest.WebRequestPost(Settings.CookieContainer, postData, "https://battle.splinterlands.com/battle/battle_tx", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:93.0) Gecko/20100101 Firefox/93.0", "https://splinterlands.com/", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at submitting team: " + ex.ToString(), Log.LogType.Error);
            }
        }

        private async Task<JToken> WaitForMatchDetails(string trxId)
        {
            for (int i = 0; i < 9; i++) // 9 * 20 = 180, so 3mins
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

        private string GetAccessToken()
        {
            if (Settings.LegacyWindowsMode)
            {
                return "none";
            }
            string filePathAccessTokens = Settings.StartupPath + @"/config/access_tokens.txt";
            IWebDriver driver = SeleniumAddons.CreateSeleniumInstance();
            try
            {
                Log.WriteToLog($"{Username}: Requesting access token... (this only happens once per account)");
                BotInstanceBrowser instance = new(Username, PostingKey, 0);

                if (instance.Login(driver, false))
                {
                    IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                    string token = (string)js.ExecuteScript("return SM.Player.token");
                    File.AppendAllText(filePathAccessTokens, Username + ":" + token + Environment.NewLine);

                    return token;
                }
                else
                {
                    return "";
                }
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at requesting access token: {ex}", Log.LogType.Error);
                return "";
            }
            finally
            {
                driver.Quit();
            }
        }
        public BotInstanceBlockchain(string username, string password, string accessToken, int index, string activeKey = "")
        {
            Username = username;
            PostingKey = password;
            ActiveKey = activeKey;
            AccessToken = accessToken.Length > 0 ? accessToken : GetAccessToken();
            SleepUntil = DateTime.Now.AddMinutes((Settings.SleepBetweenBattles + 1) * -1);
            while (AccessToken.Length == 0)
            {
                Log.WriteToLog($"{Username}: Could not get Access Token for this account - trying again", Log.LogType.Error);
                AccessToken = GetAccessToken();
            }
            LastCacheUpdate = DateTime.MinValue;
            LogSummary = new LogSummary(index, username);
            _activeLock = new object();
            APICounter = 100;
            GameStates = new Dictionary<GameState, JToken>();
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

            GameStates.Clear();
            var wsClient = Settings.LegacyWindowsMode ? null : new WebsocketClient(new Uri(Settings.SPLINTERLANDS_WEBSOCKET_URL));
            if (!Settings.LegacyWindowsMode) wsClient.ReconnectTimeout = new TimeSpan(0, 5, 0);         

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

                if (!Settings.LegacyWindowsMode)
                {
                    wsClient.MessageReceived.Subscribe(msg => HandleWebsocketMessage(msg));
                    await wsClient.Start();
                    wsClient.ReconnectionHappened.Subscribe(info =>
                        Log.WriteToLog($"Reconnection happened, type: {info.Type}"));
                    _ = WebsocketPingLoop(wsClient).ConfigureAwait(false);
                    WebsocketAuthenticate(wsClient);
                }

                if (Settings.RentalBotActivated && Convert.ToBoolean(Settings.RentalBotMethodIsAvailable.Invoke(Settings.RentalBot.Unwrap(), Array.Empty<object>())))
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
                if ((Settings.LegacyWindowsMode && APICounter >= 6) || APICounter >= 10 || (DateTime.Now - LastCacheUpdate).TotalMinutes >= 50)
                {
                    APICounter = 0;
                    LastCacheUpdate = DateTime.Now;
                    var playerDetails = await SplinterlandsAPI.GetPlayerDetailsAsync(Username);
                    PowerCached = playerDetails.power;
                    RatingCached = playerDetails.rating;
                    LeagueCached = playerDetails.league;
                    QuestCached = await SplinterlandsAPI.GetPlayerQuestAsync(Username);
                    CardsCached = await SplinterlandsAPI.GetPlayerCardsAsync(Username);
                    if (Settings.UsePrivateAPI)
                    {
                        BattleAPI.UpdateCardsForPrivateAPI(Username, CardsCached);
                    }
                    ECRCached = await GetECRFromAPIAsync();
                }

                LogSummary.Rating = RatingCached.ToString();
                LogSummary.ECR = ECRCached.ToString();

                Log.WriteToLog($"{Username}: Deck size: {(CardsCached.Length - 1).ToString().Pastel(Color.Red)} (duplicates filtered)"); // Minus 1 because phantom card array has an empty string in it
                Log.WriteToLog($"{Username}: Quest details: {JsonConvert.SerializeObject(QuestCached.QuestLessDetails).Pastel(Color.Yellow)}");

                await AdvanceLeague();
                RequestNewQuestViaAPI();
                await ClaimQuestReward();
                // claim season reward
                //signed_tx={"ref_block_num":1642,"ref_block_prefix":2848869945,"expiration":"2021-11-21T19:34:48","operations":[["custom_json",{"required_auths":[],"required_posting_auths":["username"],"id":"sm_claim_reward","json":"{\"type\":\"league_season\",\"season\":74,\"app\":\"splinterlands/0.7.139\",\"n\":\"oKt0H53ZsS\"}"}]],"extensions":[],"signatures":["1f6c4ef8937995b2f318fc7c9651bd52772e89073a4bb13afbccd45f326cd9f5a10113940fa9f55fd7bc73e72d0bd0fdcb23cd9cb44c1578cbfc821cc309188b06"]}

                Log.WriteToLog($"{Username}: Current Energy Capture Rate is { (ECRCached >= 50 ? ECRCached.ToString("N3").Pastel(Color.Green) : ECRCached.ToString("N3").Pastel(Color.Red)) }%");
                if (ECRCached < Settings.ECRThreshold)
                {
                    Log.WriteToLog($"{Username}: ECR is below threshold of {Settings.ECRThreshold}% - skipping this account.", Log.LogType.Warning);
                    SleepUntil = DateTime.Now.AddMinutes(5);
                    await Task.Delay(1500); // Short delay to not spam splinterlands api
                    return SleepUntil;
                }


                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                string jsonResponsePlain = StartNewMatch();
                string tx = Helper.DoQuickRegex("id\":\"(.*?)\"", jsonResponsePlain);
                bool submitTeam = true;
                JToken matchDetails = null;

                if (jsonResponsePlain == "" || !jsonResponsePlain.Contains("success") || !await WaitForTransactionSuccess(tx, 30))
                {
                    var outstandingGame = await Helper.DownloadPageAsync(Settings.SPLINTERLANDS_API_URL + "/players/outstanding_match?username=" + Username);
                    if (outstandingGame != "null")
                    {
                        tx = Helper.DoQuickRegex("\"id\":\"(.*?)\"", outstandingGame);
                        var teamHash = Helper.DoQuickRegex("\"team_hash\":\"(.*?)\"", outstandingGame);
                        Log.WriteToLog($"{Username}: Outstanding game: " + tx, Log.LogType.Warning);
                        if (teamHash.Length == 0)
                        {
                            Log.WriteToLog($"{Username}: Picking up outstanding game!", Log.LogType.Warning);
                            matchDetails = JToken.Parse(outstandingGame);
                        }
                        else
                        {
                            Log.WriteToLog($"{Username}: Team for outstanding game is already submitted!", Log.LogType.Warning);
                            submitTeam = false;
                        }
                    }
                    else
                    {
                        var sleepTime = 5;
                        Log.WriteToLog($"{Username}: Creating match was not successful: " + tx, Log.LogType.Warning);
                        Log.WriteToLog($"{Username}: Sleeping for { sleepTime } minutes", Log.LogType.Warning);
                        SleepUntil = DateTime.Now.AddMinutes(sleepTime);
                        return SleepUntil;
                    }
                }
                Log.WriteToLog($"{Username}: Splinterlands Response: {jsonResponsePlain}");

                SleepUntil = DateTime.Now.AddMinutes(Settings.SleepBetweenBattles);

                if (submitTeam)
                {
                    if (matchDetails == null)
                    {
                        if (Settings.LegacyWindowsMode)
                        {
                            matchDetails = await WaitForMatchDetails(tx);
                            if (matchDetails == null)
                            {
                                Log.WriteToLog($"{Username}: Banned from ranked? Sleeping for 10 minutes!", Log.LogType.Warning);
                                SleepUntil = DateTime.Now.AddMinutes(10);
                                return SleepUntil;
                            }
                        }
                        else
                        {
                            if (!await WaitForGameState(GameState.match_found, 185))
                            {
                                Log.WriteToLog($"{Username}: Banned from ranked? Sleeping for 10 minutes!", Log.LogType.Warning);
                                SleepUntil = DateTime.Now.AddMinutes(10);
                                return SleepUntil;
                            }

                            matchDetails = GameStates[GameState.match_found];
                        }
                    }

                    JToken team = await GetTeamAsync(matchDetails);
                    if (team == null)
                    {
                        Log.WriteToLog($"{Username}: API didn't find any team - Skipping Account", Log.LogType.CriticalError);
                        SleepUntil = DateTime.Now.AddMinutes(5);
                        return SleepUntil;
                    }

                    await Task.Delay(Settings._Random.Next(4500, 8000));
                    var submittedTeam = await SubmitTeamAsync(tx, matchDetails, team);
                    if (!await WaitForTransactionSuccess(submittedTeam.tx, 10))
                    {
                        SleepUntil = DateTime.Now.AddMinutes(5);
                        return SleepUntil;
                    }

                    bool surrender = false;
                    while (stopwatch.Elapsed.Seconds < 145)
                    {
                        if (Settings.LegacyWindowsMode)
                        {
                            surrender = await WaitForEnemyPick(tx, stopwatch);
                            break;
                        }
                        else
                        {
                            if (await WaitForGameState(GameState.opponent_submit_team, 4))
                            {
                                break;
                            }
                            // if there already is a battle result now it's because the enemy surrendered or the game vanished
                            if (await WaitForGameState(GameState.battle_result) || await WaitForGameState(GameState.battle_cancelled))
                            {
                                surrender = true;
                                break;
                            }
                        }
                    }

                    stopwatch.Stop();
                    if (surrender)
                    {
                        Log.WriteToLog($"{Username}: Looks like enemy surrendered - don't reveal the team", Log.LogType.Warning);
                    }
                    else
                    {
                        RevealTeam(tx, matchDetails, team, submittedTeam.secret);
                    }
                }

                Log.WriteToLog($"{Username}: Battle finished!");

                if (Settings.ShowBattleResults)
                {
                    await ShowBattleResultAsync(tx);
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
                if (!Settings.LegacyWindowsMode) wsClient.Dispose();
                Settings.LogSummaryList.Add((LogSummary.Index, LogSummary.Account, LogSummary.BattleResult, LogSummary.Rating, LogSummary.ECR, LogSummary.QuestStatus));
                lock (_activeLock)
                {
                    CurrentlyActive = false;
                }
            }
            return SleepUntil;
        }

        private async Task<JToken> GetTeamAsync(JToken matchDetails, bool ignorePrivateAPI = false)
        {
            try
            {
                int mana = (int)matchDetails["mana_cap"];
                string rulesets = (string)matchDetails["ruleset"];
                string[] inactive = ((string)matchDetails["inactive"]).Split(',');
                
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

                JToken team = await BattleAPI.GetTeamFromAPIAsync(mana, rulesets, allowedSplinters.ToArray(), CardsCached, QuestCached.Quest, QuestCached.QuestLessDetails, Username, true, ignorePrivateAPI);
                if (team == null || (string)team["summoner_id"] == "")
                {
                    return null;
                }

                if (Settings.ShowAPIResponse)
                {
                    Log.WriteToLog($"{Username}: API Response:");
                    Log.LogTeamToTable(team, mana, rulesets);
                }
                return team;
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at requesting team: {ex}", Log.LogType.Error);
                return null;
            }
        }

        private async Task ShowBattleResultLegacyAsync(string tx)
        {
            (int newRating, int ratingChange, decimal decReward, int result) battleResult = new();
            for (int i = 0; i < 14; i++)
            {
                await Task.Delay(6000);
                battleResult = await SplinterlandsAPI.GetBattleResultAsync(Username, tx);
                if (battleResult.result >= 0)
                {
                    break;
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
        private async Task ShowBattleResultAsync(string tx)
        {
            if (Settings.LegacyWindowsMode)
            {
                await ShowBattleResultLegacyAsync(tx);
                return;
            }
            if(!await WaitForGameState(GameState.battle_result, 210))
            {
                Log.WriteToLog($"{Username}: Could not get battle result", Log.LogType.Error);
            }
            else
            {
                decimal decReward = await WaitForGameState(GameState.balance_update, 10) ?
                    (decimal)GameStates[GameState.balance_update]["amount"] : 0;

                int newRating = await WaitForGameState(GameState.rating_update) ?
                    (int)GameStates[GameState.rating_update]["new_rating"] : RatingCached;

                LeagueCached = await WaitForGameState(GameState.rating_update) ?
                    (int)GameStates[GameState.rating_update]["new_league"] : LeagueCached;

                int ratingChange = newRating - RatingCached;

                if (await WaitForGameState(GameState.ecr_update))
                {
                    ECRCached = ((double)GameStates[GameState.ecr_update]["capture_rate"]) / 100;
                }
                if (await WaitForGameState(GameState.quest_progress))
                {
                    // this is a lazy way until quest is implemented as a class and we can update the quest object here
                    QuestCached = await SplinterlandsAPI.GetPlayerQuestAsync(Username);
                }
                RatingCached = newRating;

                int battleResult = 0;
                if ((string)GameStates[GameState.battle_result]["winner"] == Username)
                {
                    battleResult = 1;
                }
                else if ((string)GameStates[GameState.battle_result]["winner"] == "DRAW")
                {
                    battleResult = 2;
                }
                
                string logTextBattleResult = "";

                switch (battleResult)
                {
                    case 2:
                        logTextBattleResult = "DRAW";
                        Log.WriteToLog($"{Username}: { logTextBattleResult}");
                        Log.WriteToLog($"{Username}: Rating has not changed ({ newRating })");
                        break;
                    case 1:
                        logTextBattleResult = $"You won! Reward: { decReward } DEC";
                        Log.WriteToLog($"{Username}: { logTextBattleResult.Pastel(Color.Green) }");
                        Log.WriteToLog($"{Username}: New rating is { newRating } ({ ("+" + ratingChange.ToString()).Pastel(Color.Green) })");
                        break;
                    case 0:
                        logTextBattleResult = $"You lost :(";
                        Log.WriteToLog($"{Username}: { logTextBattleResult.Pastel(Color.Red) }");
                        Log.WriteToLog($"{Username}: New rating is { newRating } ({ ratingChange.ToString().Pastel(Color.Red) })");
                        //API.ReportLoss(winner, Username); disabled for now
                        break;
                    default:
                        break;
                }

                LogSummary.Rating = $"{ newRating } ({ ratingChange })";
                LogSummary.BattleResult = logTextBattleResult;
            }
        }

        private async Task ClaimQuestReward()
        {
            try
            {
                string logText;
                if ((int)QuestCached.Quest["completed_items"] >= (int)QuestCached.Quest["total_items"]
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
                            bool waitForHigherLeague = (rating is >= 300 and < 400) && (PowerCached is >= 1000 || (Settings.IgnoreMissingCPAtQuestClaim && PowerCached >= (0.1 * 1000))) || // bronze 2
                                (rating is >= 600 and < 700) && (PowerCached is >= 5000 || (Settings.IgnoreMissingCPAtQuestClaim && PowerCached >= (0.2 * 5000))) || // bronze 1 
                                (rating is >= 840 and < 1000) && (PowerCached is >= 15000 || (Settings.IgnoreMissingCPAtQuestClaim && PowerCached >= (0.5 * 15000))) || // silver 3
                                (rating is >= 1200 and < 1300) && (PowerCached is >= 40000 || (Settings.IgnoreMissingCPAtQuestClaim && PowerCached >= (0.8 * 40000))) || // silver 2
                                (rating is >= 1500 and < 1600) && (PowerCached is >= 70000 || (Settings.IgnoreMissingCPAtQuestClaim && PowerCached >= (0.85 * 70000))) || // silver 1
                                (rating is >= 1800 and < 1900) && (PowerCached is >= 100000 || (Settings.IgnoreMissingCPAtQuestClaim && PowerCached >= (0.9 * 100000))); // gold 

                            if (waitForHigherLeague)
                            {
                                Log.WriteToLog($"{Username}: Don't claim quest - wait for higher league");
                                return;
                            }
                        }

                        string n = Helper.GenerateRandomString(10);
                        string json = "{\"type\":\"quest\",\"quest_id\":\"" + (string)QuestCached.Quest["id"] +"\",\"app\":\"" + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";

                        COperations.custom_json custom_Json = CreateCustomJson(false, true, "sm_claim_reward", json);

                        CtransactionData oTransaction = Settings.oHived.CreateTransaction(new object[] { custom_Json }, new string[] { PostingKey });
                        string tx = Settings.oHived.broadcast_transaction(new object[] { custom_Json }, new string[] { PostingKey });
                        //var postData = GetStringForSplinterlandsAPI(oTransaction);
                        //string response = HttpWebRequest.WebRequestPost(Settings.CookieContainer, postData, Settings.SPLINTERLANDS_BROADCAST_URL, "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:93.0) Gecko/20100101 Firefox/93.0", "https://splinterlands.com/", Encoding.UTF8);

                        //string tx = Helper.DoQuickRegex("id\":\"(.*?)\"", response);
                        if (await WaitForTransactionSuccess(tx, 45))
                        {
                            Log.WriteToLog($"{Username}: { "Claimed quest reward:".Pastel(Color.Green) } {tx}");
                            APICounter = 100; // set api counter to 100 to reload quest
                        }
                        //else
                        //{
                        //    if (response.Contains("There was an issue broadcasting"))
                        //    {
                        //        var v = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
                        //        response = HttpWebRequest.WebRequestGet(Settings.CookieContainer, $"https://api2.splinterlands.com/players/delegation?v={v}&token={AccessToken}&username={Username}", "", "https://splinterlands.com/");
                        //        await Task.Delay(15000);
                        //        custom_Json = CreateCustomJson(false, true, "sm_claim_reward", json);
                        //        tx = Settings.oHived.broadcast_transaction(new object[] { custom_Json }, new string[] { PostingKey });
                        //        Log.WriteToLog($"{Username}: { "Advanced league: ".Pastel(Color.Green) } {tx}");
                        //    }
                        //}
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
            var balanceInfo = ((JArray)await SplinterlandsAPI.GetPlayerBalancesAsync(Username)).Where(x => (string)x["token"] == "ECR").First();
            var captureRate = (int)balanceInfo["balance"];
            DateTime lastRewardTime = (DateTime)balanceInfo["last_reward_time"];
            double ecrRegen = 0.0868;
            double ecr = captureRate + (new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds() - new DateTimeOffset(lastRewardTime).ToUnixTimeMilliseconds()) / 3000 * ecrRegen;
            return Math.Min(ecr, 10000) / 100;
        }

        private async Task AdvanceLeague()
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

                    string n = Helper.GenerateRandomString(10);
                    string json = "{\"notify\":\"false\",\"app\":\"" + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";

                    COperations.custom_json custom_Json = CreateCustomJson(false, true, "sm_advance_league", json);
                    CtransactionData oTransaction = Settings.oHived.CreateTransaction(new object[] { custom_Json }, new string[] { PostingKey });
                    string tx = Settings.oHived.broadcast_transaction(new object[] { custom_Json }, new string[] { PostingKey });
                    //var postData = GetStringForSplinterlandsAPI(oTransaction);
                    //string response = HttpWebRequest.WebRequestPost(Settings.CookieContainer, postData, Settings.SPLINTERLANDS_BROADCAST_URL, "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:93.0) Gecko/20100101 Firefox/93.0", "https://splinterlands.com/", Encoding.UTF8);

                    //string tx = Helper.DoQuickRegex("id\":\"(.*?)\"", response);
                    if (await WaitForTransactionSuccess(tx, 45))
                    {
                        Log.WriteToLog($"{Username}: { "Advanced league: ".Pastel(Color.Green) } {tx}");
                        APICounter = 100; // set api counter to 100 to reload details
                    }
                    else
                    {
                        APICounter = 100;
                    }
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
