using Cryptography.ECDSA;
using HiveAPI.CS;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
using Ultimate_Splinterlands_Bot_V2.Model;
using Ultimate_Splinterlands_Bot_V2.Utils;
using Ultimate_Splinterlands_Bot_V2.Api;
using Ultimate_Splinterlands_Bot_V2.Http;
using Ultimate_Splinterlands_Bot_V2.Config;

namespace Ultimate_Splinterlands_Bot_V2.Bot
{
    [JsonObject(MemberSerialization.OptIn)]
    public class BotInstance
    {
        [JsonProperty]
        public string Username { get; private set; }
        private string PostingKey { get; set; }
        private string ActiveKey { get; set; } // only needed for plugins, not used by normal bot
        [JsonProperty]
        public string AccessToken { get; private set; } // used for websocket authentication
        private int APICounter { get; set; }
        private int PowerCached { get; set; }
        private int LeagueCached { get; set; }
        private int RatingCached { get; set; }
        private double ECRCached { get; set; }
        private int LossesTotal { get; set; }
        private double DrawsTotal { get; set; }
        private double WinsTotal { get; set; }
        [JsonProperty]
        private Reward Reward { get; set; }
        private Card[] CardsCached { get; set; }
        private Dictionary<GameEvent, JToken> GameEvents { get; set; }
        public bool CurrentlyActive { get; private set; }
        private bool WebsocketAuthenticated { get; set; }

        private readonly object _activeLock;
        [JsonProperty]
        private DateTime SleepUntil;
        private DateTime LastCacheUpdate;
        private LogSummary LogSummary;
        [JsonProperty]
        private readonly bool IsDeserialized;

        private void HandleWebsocketMessage(ResponseMessage message)
        {
            if (message.MessageType != System.Net.WebSockets.WebSocketMessageType.Text
                || !message.Text.Contains("\"id\""))
            {
                // handle expired access token
                if (message.Text.StartsWith("{\"status\":\"connection refused\""))
                {
                    Log.WriteToLog($"{Username}: Access Token expired!", Log.LogType.Warning);
                    AccessToken = RequestAccessTokenAsync().Result;
                }
                else if (message.Text.StartsWith("{\"status\":\"authenticated\""))
                {
                    WebsocketAuthenticated = true;
                }
                return;
            }

            JToken json = JToken.Parse(message.Text);
            if (Enum.TryParse(json["id"].ToString(), out GameEvent gameEvent))
            {
                if (GameEvents.ContainsKey(gameEvent))
                {
                    GameEvents[gameEvent] = json["data"];
                }
                else
                {
                    GameEvents.TryAdd(gameEvent, json["data"]);
                }

                if (gameEvent == GameEvent.ecr_update)
                {
                    ECRCached = (double)GameEvents[GameEvent.ecr_update]["capture_rate"] / 100;
                }
                else if (gameEvent == GameEvent.transaction_complete
                    && (string)json["data"]["trx_info"]["type"] == "claim_reward")
                {
                    JToken result = JToken.Parse((string)json["data"]["trx_info"]["result"]);
                    Reward.SetLastChestQuantity(result["rewards"].Count());
                }
            }
            else if (json["data"]["trx_info"] != null
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

        private async Task<bool> WaitForGameEventAsync(GameEvent gameEvent, int secondsToWait = 0)
        {
            int maxI = secondsToWait > 1 ? secondsToWait : 2;
            for (int i = 0; i < maxI; i++)
            {
                if (GameEvents.ContainsKey(gameEvent))
                {
                    return true;
                }
                if (i != maxI - 1)
                {
                    await Task.Delay(1000);
                }
            }
            return false;
        }

        private async Task<bool> WaitForTransactionSuccessAsync(string tx, int secondsToWait)
        {
            if (tx.Length == 0)
            {
                return false;
            }
            else if (Settings.LegacyWindowsMode)
            {
                return true;
            }

            for (int i = 0; i < secondsToWait * 2; i++)
            {
                await Task.Delay(500);
                if (GameEvents.ContainsKey(GameEvent.transaction_complete)
                    && (string)GameEvents[GameEvent.transaction_complete]["trx_info"]["id"] == tx)
                {
                    if ((bool)GameEvents[GameEvent.transaction_complete]["trx_info"]["success"])
                    {
                        return true;
                    }
                    else
                    {
                        Log.WriteToLog($"{Username}: Transaction error: " + tx + " - " + (string)GameEvents[GameEvent.transaction_complete]["trx_info"]["error"], Log.LogType.Warning);
                        return false;
                    }
                }
            }
            return false;
        }

        private async Task WebsocketPingLoopAsync(IWebsocketClient wsClient)
        {
            try
            {
                while (CurrentlyActive)
                {
                    for (int i = 0; i < 12; i++)
                    {
                        await Task.Delay(5 * 1000);
                        if (!CurrentlyActive)
                        {
                            return;
                        }
                    }

                    Log.WriteToLog($"{Username}: ping", debugOnly: true);
                    wsClient.Send("{\"type\":\"ping\"}");
                }
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at WebSocket ping { ex }");
            }
            finally
            {
                await wsClient.Stop(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "");
                wsClient.Dispose();
            }
        }

        private async Task<bool> WebsocketAuthenticate(IWebsocketClient wsClient)
        {
            string sessionID = Helper.GenerateRandomString(10);
            string message = "{\"type\":\"auth\",\"player\":\"" + Username + "\",\"access_token\":\"" + AccessToken + "\",\"session_id\":\"" + sessionID + "\"}";
            wsClient.Send(message);

            for (int i = 0; i < 175; i++)
            {
                await Task.Delay(50);
                if (WebsocketAuthenticated)
                {
                    return true;
                }
            }

            return false;
        }

        private COperations.custom_json CreateCustomJson(bool activeKey, bool postingKey, string methodName, string json)
        {
            COperations.custom_json customJsonOperation = new()
            {
                required_auths = activeKey ? new string[] { Username } : Array.Empty<string>(),
                required_posting_auths = postingKey ? new string[] { Username } : Array.Empty<string>(),
                id = methodName,
                json = json
            };
            return customJsonOperation;
        }

        private static string GetStringForSplinterlandsAPI(CtransactionData oTransaction)
        {
            try
            {
                string json = JsonConvert.SerializeObject(oTransaction.tx);
                string postData = "signed_tx=" + json.Replace("operations\":[{", "operations\":[[\"custom_json\",{")
                    .Replace(",\"opid\":18}", "}]");
                return postData;
            }
            catch (Exception ex)
            {
                Log.WriteToLog("Error at GetStringForSplinterlandsAPI:" + ex.ToString());
            }
            return "";
        }

        private string StartNewMatch()
        {
            string n = Helper.GenerateRandomString(10);
            string matchType = Settings.RankedFormat == "WILD" ? "Ranked" : "Modern Ranked";
            string json = "{\"match_type\":\"" + matchType + "\",\"app\":\"" + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";

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

        private async Task<bool> WaitForEnemyPickAsync(string tx, Stopwatch stopwatch)
        {
            int counter = 0;
            do
            {
                var enemyHasPicked = await SplinterlandsAPI.CheckEnemyHasPickedAsync(Username, tx);
                if (enemyHasPicked.enemyHasPicked)
                {
                    // this is no longer working, but since this is only for old windows versions we don't really care!
                    //return enemyHasPicked.surrender;
                    return false;
                }
                Log.WriteToLog($"{Username}: Waiting 15 seconds for enemy to pick #{++counter}");
                await Task.Delay(stopwatch.Elapsed.TotalSeconds > 170 ? 2500 : 15000);
            } while (stopwatch.Elapsed.TotalSeconds < 179);
            return false;
        }
        
        private async Task<(string secret, string tx, JToken team)> SubmitTeamAsync(string tx, JToken matchDetails, JToken team, bool secondTry = false)
        {
            try
            {
                var cardQuery = CardsCached.Where(x => x.card_detail_id == (string)team["summoner_id"]);
                string summoner = cardQuery.Any() ? cardQuery.First().card_long_id : null;
                string monsters = "";
                for (int i = 0; i < 6; i++)
                {
                    var monster = CardsCached.Where(x => x.card_detail_id == (string)team[$"monster_{i + 1}_id"]).FirstOrDefault();
                    if (monster == null || summoner == null)
                    {
                        if (Settings.UsePrivateAPI && !secondTry)
                        {
                            Log.WriteToLog($"{Username}: Requesting team from public API - private API needs card update!", Log.LogType.Warning);
                            CardsCached = await SplinterlandsAPI.GetPlayerCardsAsync(Username, AccessToken);
                            team = await GetTeamAsync(matchDetails, ignorePrivateAPI: true);
                            BattleAPI.UpdateCardsForPrivateAPI(Username, CardsCached);
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

                /*string json = "{\"trx_id\":\"" + tx + "\",\"team_hash\":\"" + teamHash + "\",\"summoner\":\"" + summoner 
                    + "\",\"monsters\":[" + monsters + "],\"secret\":\"" + secret + "\",\"app\":\"" 
                    + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";
                */
                string json = "{\"trx_id\":\"" + tx + "\",\"team_hash\":\"" + teamHash + "\",\"app\":\"" + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";

                COperations.custom_json custom_Json = CreateCustomJson(false, true, "sm_submit_team", json);

                Log.WriteToLog($"{Username}: Submitting team...");
                CtransactionData oTransaction = Settings.oHived.CreateTransaction(new object[] { custom_Json }, new string[] { PostingKey });
                var postData = GetStringForSplinterlandsAPI(oTransaction);
                var response = HttpWebRequest.WebRequestPost(Settings.CookieContainer, postData, "https://battle.splinterlands.com/battle/battle_tx", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:93.0) Gecko/20100101 Firefox/93.0", "https://splinterlands.com/", Encoding.UTF8);
                string responseTx = Helper.DoQuickRegex("id\":\"(.*?)\"", response);
                return (secret, responseTx, team);
            }   
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at submitting team: " + ex.ToString(), Log.LogType.Error);
                // update cards for private API
                APICounter = 100;
            }
            return ("", "", null);
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

        private async Task<JToken> WaitForMatchDetailsAsync(string trxId)
        {
            for (int i = 0; i < 15; i++) // 9 * 20 = 300, so 5mins
            {
                try
                {
                    await Task.Delay(7500);
                    string matchDetailsRaw = await Helper.DownloadPageAsync(Settings.SPLINTERLANDS_API_URL + "/battle/status?id=" + trxId);
                    if (matchDetailsRaw.Contains("no battle"))
                    {
                        if (i > 10)
                        {
                            Log.WriteToLog($"{Username}: Error at waiting for match details: " + matchDetailsRaw, Log.LogType.Error);
                            return null;
                        }
                    }
                    else
                    {
                        var matchDetails = JToken.Parse(matchDetailsRaw);
                        if (matchDetails["mana_cap"] != null && matchDetails["mana_cap"].Type != JTokenType.Null)
                        {
                            return matchDetails;
                        }
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

        private async Task<string> RequestAccessTokenAsync()
        {
            if (Settings.LegacyWindowsMode)
            {
                return "none";
            }
            try
            {
                Log.WriteToLog($"{Username}: Requesting access token...");
                var filePathAccessTokens = Settings.StartupPath + @"/config/access_tokens.txt";
                var bid = "bid_" + Helper.GenerateRandomString(20);
                var sid = "sid_" + Helper.GenerateRandomString(20);
                var ts = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds().ToString();
                var hash = Sha256Manager.GetHash(Encoding.ASCII.GetBytes(Username + ts));
                var sig = Secp256K1Manager.SignCompressedCompact(hash, CBase58.DecodePrivateWif(PostingKey));
                var signature = Hex.ToString(sig);
                var response = await Helper.DownloadPageAsync(Settings.SPLINTERLANDS_API_URL + "/players/login?name=" + Username + "&ref=&browser_id=" + bid + "&session_id=" + sid + "&sig=" + signature + "&ts=" + ts);

                var token = Helper.DoQuickRegex("\"name\":\"" + Username + "\",\"token\":\"([A-Z0-9]{10})\"", response);
                if (token.Length > 0)
                {
                    Log.WriteToLog($"{Username}: Successfully requested access token!", Log.LogType.Success);
                }
                return token;
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at requesting access token: {ex}", Log.LogType.Error);
                return "";
            }
        }
        public BotInstance(string username)
        {
            Username = username;
            LastCacheUpdate = DateTime.MinValue;
            Reward = new();
            if (!IsDeserialized)
            {
                SleepUntil = DateTime.Now;
                IsDeserialized = true;
            }
            _activeLock = new object();
            APICounter = 99999;
            GameEvents = new Dictionary<GameEvent, JToken>();
            DrawsTotal = 0;
            WinsTotal = 0;
            LossesTotal = 0;
        }

        public void Initialize(int index, string postingKey, string activeKey = "")
        {
            LogSummary = new LogSummary(index, Username);
            PostingKey = postingKey;
            ActiveKey = activeKey;
            AccessToken = AccessToken?.Length > 0 ? AccessToken : RequestAccessTokenAsync().Result;
            while (AccessToken.Length == 0)
            {
                // Sleep 20 seconds to not spam retries
                Thread.Sleep(20000);
                Log.WriteToLog($"{Username}: Could not get Access Token for this account - trying again", Log.LogType.Error);
                Log.WriteToLog($"{Username}: Make sure to use your correct username and posting key", Log.LogType.Error);
                AccessToken = RequestAccessTokenAsync().Result;
            }
        }

        public async Task<DateTime> DoBattleAsync()
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

            if (Settings.ClaimSeasonReward)
            {
                await ClaimSeasonRewardAsync();
                return SleepUntil.AddMinutes(30);
            }

            GameEvents.Clear();
            WebsocketAuthenticated = false;
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
                    if (!Settings.DebugMode) Log.WriteToLog($"{Username}: is sleeping until {SleepUntil.ToString().Pastel(Color.Red)}");
                    return SleepUntil;
                }

                if (!Settings.LegacyWindowsMode)
                {
                    wsClient.MessageReceived.Subscribe(msg => HandleWebsocketMessage(msg));
                    await wsClient.Start();
                    wsClient.ReconnectionHappened.Subscribe(info =>
                        Log.WriteToLog($"{Username}: Reconnection happened, type: {info.Type}"));
                    _ = WebsocketPingLoopAsync(wsClient).ConfigureAwait(false);

                    // don't fight if access token is getting refreshed
                    if(!await WebsocketAuthenticate(wsClient))
                    {
                        await Task.Delay(15000);
                        return SleepUntil;
                    }
                }

                APICounter++;
                if (Settings.LegacyWindowsMode && APICounter >= 5 || APICounter >= 10 || (DateTime.Now - LastCacheUpdate).TotalMinutes >= 50)
                {
                    LastCacheUpdate = DateTime.Now;
                    var (power, wildRating, wildLeague, modernRating, modernLeague) = await SplinterlandsAPI.GetPlayerDetailsAsync(Username);
                    PowerCached = power;
                    RatingCached = Settings.RankedFormat == "WILD" ? wildRating : modernRating;
                    LeagueCached = Settings.RankedFormat == "WILD" ? wildLeague : modernLeague;

                    Reward.Quest = await SplinterlandsAPI.GetPlayerQuestAsync(Username);
                    CardsCached = await SplinterlandsAPI.GetPlayerCardsAsync(Username, AccessToken);
                    JArray playerBalances = (JArray)await SplinterlandsAPI.GetPlayerBalancesAsync(Username);
                    ECRCached = GetEcrFromPlayerBalances(playerBalances);

                    if (Settings.UsePrivateAPI)
                    {
                        BattleAPI.UpdateCardsForPrivateAPI(Username, CardsCached);
                        BattleAPI.UpdateAccountInfoForPrivateAPI(Username, ECRCached, playerBalances, wildRating, wildLeague, modernRating, modernLeague, power);
                    }

                    // Only at start of bot
                    if (APICounter >= 99999 && Settings.StartBattleAboveECR >= 10 && ECRCached < Settings.StartBattleAboveECR)
                    {
                        SetSleepUntilEcrReached(Settings.StartBattleAboveECR);
                        TransferPowerIfNeeded();
                        return SleepUntil;
                    }
                    APICounter = 0;
                }

                LogSummary.Rating = RatingCached.ToString();
                LogSummary.ECR = ECRCached.ToString();

                Log.WriteToLog($"{Username}: Deck size: {(CardsCached.Length - 1).ToString().Pastel(Color.Red)} (duplicates filtered)"); // Minus 1 because phantom card array has an empty string in it
                if (Reward.Quest != null)
                {
                    // new quests temp workaround
                    if (Settings.QuestTypes.ContainsKey(Reward.Quest.Name))
                    {
                        Log.WriteToLog($"{Username}: Quest element: {Settings.QuestTypes[Reward.Quest.Name].Pastel(Color.Yellow)} " +
    $"Completed items: {Reward.Quest.CompletedItems.ToString().Pastel(Color.Yellow)}");
                    }
                    else
                    {
                        Log.WriteToLog($"{Username}: Quest element: {Reward.Quest.Name.Pastel(Color.Yellow)} ");
                        //Log.WriteToLog($"{Username} has new quest type - the bot will not be updated to play for them until august!", Log.LogType.Warning);
                    }
                }
                else
                {
                    // TODO test this and make the bot request a quest on it's own
                    Log.WriteToLog($"{Username}: Account has no quest! Log in via browser to request one!", Log.LogType.Warning);
                    Log.WriteToLog($"{Username}: Account has no quest! Log in via browser to request one!", Log.LogType.Warning);
                    Log.WriteToLog($"{Username}: Account has no quest! Log in via browser to request one!", Log.LogType.Warning);
                }

                await AdvanceLeagueAsync();
                await ClaimQuestRewardAsync();
                await RequestNewQuestViaAPIAsync();

                Log.WriteToLog($"{Username}: Current Energy Capture Rate is { (ECRCached >= 50 ? ECRCached.ToString("N3").Pastel(Color.Green) : ECRCached.ToString("N3").Pastel(Color.Red)) }%");
                if (ECRCached < Settings.StopBattleBelowECR)
                {
                    Log.WriteToLog($"{Username}: ECR is below threshold of {Settings.StopBattleBelowECR}% - skipping this account.", Log.LogType.Warning);
                    if (Settings.StartBattleAboveECR >= 10)
                    {
                        SetSleepUntilEcrReached(Settings.StartBattleAboveECR);
                        TransferPowerIfNeeded();
                    }
                    else
                    {
                        SleepUntil = DateTime.Now.AddMinutes(5);
                    }
                    await Task.Delay(1500); // Short delay to not spam splinterlands api
                    return SleepUntil;
                }

                if (PowerCached < Settings.MinimumBattlePower)
                {
                    bool transferPower = false;
                    if (Settings.PowerTransferBot)
                    {
                        BotInstance account = null;
                        lock (Settings.PowerTransferBotLock)
                        {
                            if (Settings.PlannedPowerTransfers.ContainsKey(Username))
                            {
                                account = Settings.PlannedPowerTransfers[Username];
                                Settings.PlannedPowerTransfers.Remove(Username);
                                transferPower = true;
                            }
                            else if (Settings.AvailablePowerTransfers.Any())
                            {
                                account = Settings.AvailablePowerTransfers.Dequeue();
                                transferPower = true;
                            }

                            if (transferPower)
                            {
                                // Set the power to a high value temporarily so no other account will try to send cards to it
                                PowerCached = 999999;
                                APICounter = 999;
                                account.PowerCached = 0;
                                account.APICounter = 999;
                            }
                        }

                        if (transferPower)
                        {
                            var sessionID = Settings.CookieContainer.GetCookies(new Uri(Settings.PrivateAPIShop)).FirstOrDefault();
                            var args = $"{account.Username} {Username} {account.ActiveKey} {Settings.PrivateAPIUsername} " +
                                $"{Settings.PrivateAPIPassword} {sessionID.Name} {sessionID.Value} {Settings.DebugMode}";
                            var fileName = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) ?
                                 "Power Transfer Bot.exe" : "Power Transfer Bot";

                            if (Helper.RunProcessWithResult(Settings.StartupPath + "/PowerTransferBot/" + fileName, args))
                            {
                                Log.WriteToLog($"{Username}: Successfully transfered power from {account.Username} to {Username}", Log.LogType.Success);
                                SleepUntil = DateTime.Now.AddSeconds(30);
                            }
                            else
                            {
                                Log.WriteToLog($"{Username}: Error at transfering power from {account.Username} to {Username}", Log.LogType.Error);
                            }
                        }
                    }
                    if (!transferPower)
                    {
                        Log.WriteToLog($"{Username}: Power is below threshold of {Settings.MinimumBattlePower} - skipping this account.", Log.LogType.Warning);
                        SleepUntil = DateTime.Now.AddMinutes(Settings.PowerTransferBot ? 60 : 30);
                    }
                    await Task.Delay(1500); // Short delay to not spam splinterlands api
                    return SleepUntil;
                }

                Stopwatch stopwatch = new();
                stopwatch.Start();
                string jsonResponsePlain = StartNewMatch();
                string tx = Helper.DoQuickRegex("id\":\"(.*?)\"", jsonResponsePlain);
                bool submitTeam = true;
                JToken matchDetails = null;

                if (jsonResponsePlain == "" || !jsonResponsePlain.Contains("success") || !await WaitForTransactionSuccessAsync(tx, 30))
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

                // Subtract 1% from ECR
                ECRCached *= 0.99d;

                SleepUntil = DateTime.Now.AddMinutes(Settings.SleepBetweenBattles);

                if (submitTeam)
                {
                    if (matchDetails == null)
                    {
                        if (Settings.LegacyWindowsMode)
                        {
                            matchDetails = await WaitForMatchDetailsAsync(tx);
                            if (matchDetails == null)
                            {
                                Log.WriteToLog($"{Username}: Banned from ranked? Sleeping for 10 minutes!", Log.LogType.Warning);
                                SleepUntil = DateTime.Now.AddMinutes(10);
                                return SleepUntil;
                            }
                        }
                        else
                        {
                            if (!await WaitForGameEventAsync(GameEvent.match_found, 185))
                            {
                                Log.WriteToLog($"{Username}: Banned from ranked? Sleeping for 10 minutes!", Log.LogType.Warning);
                                SleepUntil = DateTime.Now.AddMinutes(10);
                                return SleepUntil;
                            }

                            matchDetails = GameEvents[GameEvent.match_found];
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
                    if (!await WaitForTransactionSuccessAsync(submittedTeam.tx, 10))
                    {
                        SleepUntil = DateTime.Now.AddMinutes(5);
                        return SleepUntil;
                    }

                    //// Reveal the team even when the enemy surrendered, just to be sure
                    RevealTeam(tx, matchDetails, submittedTeam.team, submittedTeam.secret);

                    bool surrender = false;
                    while (stopwatch.Elapsed.Seconds < 130)
                    {
                        if (Settings.LegacyWindowsMode)
                        {
                            surrender = await WaitForEnemyPickAsync(tx, stopwatch);
                            break;
                        }
                        else
                        {
                            if (await WaitForGameEventAsync(GameEvent.opponent_submit_team, 5))
                            {
                                break;
                            }
                            // if there already is a battle result now it's because the enemy surrendered or the game vanished
                            if ((await WaitForGameEventAsync(GameEvent.battle_result) && !await WaitForGameEventAsync(GameEvent.opponent_submit_team))
                                || await WaitForGameEventAsync(GameEvent.battle_cancelled))
                            {
                                surrender = true;
                                break;
                            }
                        }
                    }

                    stopwatch.Stop();
                    if (surrender)
                    {
                        Log.WriteToLog($"{Username}: Looks like the enemy surrendered!", Log.LogType.Warning);
                    }
                }

                if (Settings.ShowBattleResults)
                {
                    await ShowBattleResultAsync(tx);
                }
                else
                {
                    Log.WriteToLog($"{Username}: Battle finished!");
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: {ex}{Environment.NewLine}Skipping account", Log.LogType.CriticalError);
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

        private void TransferPowerIfNeeded()
        {
            Log.WriteToLog($"{Username}: PowerTransferDebug: PowerCached: {PowerCached}", debugOnly: true);
            if (Settings.PowerTransferBot && PowerCached >= Settings.MinimumBattlePower)
            {
                lock (Settings.PowerTransferBotLock)
                {
                    var query = Settings.BotInstances.Where(x =>
                        x.ECRCached >= Settings.StartBattleAboveECR && x.PowerCached < Settings.MinimumBattlePower)
                        .OrderByDescending(y => y.ECRCached);

                    bool availableForAnyAccount = true;
                    BotInstance[] accountsECRSorted = query.ToArray();

                    if (accountsECRSorted.Any())
                    {
                        for (int i = 0; i < accountsECRSorted.Length; i++)
                        {
                            Log.WriteToLog($"{Username}: PowerTransferDebug: {accountsECRSorted[i].Username}", debugOnly: true);
                            var receivingAccount = accountsECRSorted[i];
                            if (!Settings.PlannedPowerTransfers.ContainsKey(receivingAccount.Username))
                            {
                                Log.WriteToLog($"{Username}: PowerTransferDebug: Planned transfer: {accountsECRSorted[i].Username}", debugOnly: true);
                                availableForAnyAccount = false;
                                Settings.PlannedPowerTransfers.Add(receivingAccount.Username, this);

                                // Remove any remaining sleep
                                receivingAccount.SleepUntil = DateTime.Now;
                                break;
                            }
                        }
                    }

                    if (availableForAnyAccount)
                    {
                        // Show this as available to any account
                        if (!Settings.AvailablePowerTransfers.Contains(this))
                        {
                            Log.WriteToLog($"{Username}: No eligible account for power transfer found - will transfer cards once there is an account that needs cards!");
                            Settings.AvailablePowerTransfers.Enqueue(this);
                        }
                    }
                }
            }
        }

        private void SetSleepUntilEcrReached(int desiredEcr)
        {
            double missingECR = desiredEcr - ECRCached;
            double hoursUntilEcrReached = missingECR / 1.041666;
            SleepUntil = DateTime.Now.AddHours(hoursUntilEcrReached).AddMinutes(1);
            Log.WriteToLog($"{Username}: Sleeping until {SleepUntil.ToShortTimeString()} to reach an ECR of {desiredEcr}%.", Log.LogType.Warning);
            APICounter = 999;
        }

        private async Task ClaimSeasonRewardAsync()
        {
            try
            {
                Log.WriteToLog($"{Username}: Checking for season rewards... ");
                var bid = "bid_" + Helper.GenerateRandomString(20);
                var sid = "sid_" + Helper.GenerateRandomString(20);
                var ts = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds().ToString();
                var hash = Sha256Manager.GetHash(Encoding.ASCII.GetBytes(Username + ts));
                var sig = Secp256K1Manager.SignCompressedCompact(hash, CBase58.DecodePrivateWif(PostingKey));
                var signature = Hex.ToString(sig);
                var response = await Helper.DownloadPageAsync(Settings.SPLINTERLANDS_API_URL + "/players/login?name=" + Username + "&ref=&browser_id=" + bid + "&session_id=" + sid + "&sig=" + signature + "&ts=" + ts);

                if (response.Contains("maintenance mode") || response.Contains("Too many request"))
                {
                    Log.WriteToLog($"{Username}: Error at claiming season rewards: Maintenance mode or IP soft ban - wait 5 minutes!", Log.LogType.Warning);
                    await Task.Delay(5 * 60 * 1000);
                    ts = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds().ToString();
                    hash = Sha256Manager.GetHash(Encoding.ASCII.GetBytes(Username + ts));
                    sig = Secp256K1Manager.SignCompressedCompact(hash, CBase58.DecodePrivateWif(PostingKey));
                    signature = Hex.ToString(sig);
                    response = await Helper.DownloadPageAsync(Settings.SPLINTERLANDS_API_URL + "/players/login?name=" + Username + "&ref=&browser_id=" + bid + "&session_id=" + sid + "&sig=" + signature + "&ts=" + ts);
                }

                var seasonReward = Helper.DoQuickRegex("\"season_reward\":(.*?)},\"", response);
                if (seasonReward.StartsWith("{\"reward_packs\":0"))
                {
                    Log.WriteToLog($"{Username}: No season reward available!", Log.LogType.Error);
                }
                else
                {
                    var season = Helper.DoQuickRegex("\"season\":(.*?)\\Z", seasonReward);
                    if (season.Length <= 1)
                    {
                        Log.WriteToLog($"{Username}: Error at claiming season rewards: Could not read season!", Log.LogType.Error);
                    }
                    else
                    {
                        string n = Helper.GenerateRandomString(10);
                        string json = "{\"type\":\"league_season\",\"season\":\"" + season + "\",\"app\":\"" + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";
                        string tx = BroadcastCustomJsonToHiveNode("sm_claim_reward", json);

                        for (int i = 0; i < 10; i++)
                        {
                            await Task.Delay(15000);
                            var rewardsRaw = await Helper.DownloadPageAsync(Settings.SPLINTERLANDS_API_URL + "/transactions/lookup?trx_id=" + tx);
                            if (rewardsRaw.Contains(" not found"))
                            {
                                continue;
                            }
                            else if (rewardsRaw.Contains("as already claimed their rewards from the specified season"))
                            {
                                Log.WriteToLog($"{Username}: Rewards already claimed!", Log.LogType.Error);
                                return;
                            }
                            var rewards = JToken.Parse(rewardsRaw)["trx_info"]["result"];


                            if (!((string)rewards).Contains("success\":true"))
                            {
                                Log.WriteToLog($"{Username}: Error at claiming season rewards: " + (string)rewards, Log.LogType.Error);
                                return;
                            }
                            else if (((string)rewards).Contains("success\":true"))
                            {
                                Log.WriteToLog($"{Username}: Successfully claimed season rewards!", Log.LogType.Success);
                                return;
                            }
                            else
                            {

                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at claiming season reward: {ex}", Log.LogType.Error);
            }
        }

        private string BroadcastCustomJsonToHiveNode(string command, string json, bool postingKey = true, bool activeKey = false)
        {
            try
            {
                COperations.custom_json custom_Json = CreateCustomJson(activeKey, postingKey, command, json);

                string tx = Settings.oHived.broadcast_transaction(new object[] { custom_Json }, new string[] { postingKey ? PostingKey : ActiveKey });
                return tx;
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at broadcasting transaction to blockchain: {ex}", Log.LogType.Error);
            }
            return "";
        }

        private async Task<JToken> GetTeamAsync(JToken matchDetails, bool ignorePrivateAPI = false)
        {
            try
            {
                int mana = (int)matchDetails["mana_cap"];
                string rulesets = (string)matchDetails["ruleset"];
                string[] inactive = ((string)matchDetails["inactive"]).Split(',');
                string gameIdPlayer = (string)matchDetails["id"];
                //string gameIdOpponent = (string)matchDetails["opponent"]";
                string opponentLookupName = Settings.LegacyWindowsMode ? (string)matchDetails["opponent"] : (string)matchDetails["opponent"]["lookup_name"];
                string gameIdHash = Helper.GenerateMD5Hash(gameIdPlayer) + "/" + Helper.GenerateMD5Hash(opponentLookupName);

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

                int chestTier = GetTier(LeagueCached);

                JToken team = await BattleAPI.GetTeamFromAPIAsync(RatingCached, mana, rulesets, allowedSplinters.ToArray(), CardsCached, Reward.Quest, chestTier, Username, gameIdHash, false, ignorePrivateAPI);
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
                    DrawsTotal++;
                    Log.WriteToLog($"{Username}: { logTextBattleResult}");
                    Log.WriteToLog($"{Username}: Rating has not changed ({ battleResult.newRating })");
                    break;
                case 1:
                    WinsTotal++;
                    logTextBattleResult = $"You won! Reward: { battleResult.decReward } SPS";
                    Log.WriteToLog($"{Username}: { logTextBattleResult.Pastel(Color.Green) }");
                    Log.WriteToLog($"{Username}: New rating is { battleResult.newRating } ({ ("+" + battleResult.ratingChange.ToString()).Pastel(Color.Green) })");
                    break;
                case 0:
                    LossesTotal++;
                    logTextBattleResult = $"You lost :(";
                    Log.WriteToLog($"{Username}: { logTextBattleResult.Pastel(Color.Red) }");
                    Log.WriteToLog($"{Username}: New rating is { battleResult.newRating } ({ battleResult.ratingChange.ToString().Pastel(Color.Red) })");
                    //BattleAPI.ReportLoss(winner, Username); disabled for now
                    break;
                default:
                    break;
            }

            LogSummary.Rating = $"{ battleResult.newRating } ({ battleResult.ratingChange })";
            LogSummary.BattleResult = logTextBattleResult;
        }
        private async Task ShowBattleResultAsync(string tx)
        {
            if (Settings.LegacyWindowsMode || Settings.ShowSpsReward)
            {
                await ShowBattleResultLegacyAsync(tx);
                return;
            }
            if (!await WaitForGameEventAsync(GameEvent.battle_result, 210))
            {
                Log.WriteToLog($"{Username}: Could not get battle result", Log.LogType.Error);
            }
            else
            {
                var rankedFormat = Settings.RankedFormat.ToLower();
                //decimal spsReward = await WaitForGameEventAsync(GameEvent.balance_update, 10) ?
                    //(decimal)GameEvents[GameEvent.balance_update]["amount"] : 0;

                int newRating = await WaitForGameEventAsync(GameEvent.rating_update) ?
                    (int)GameEvents[GameEvent.rating_update][rankedFormat]["new_rating"] : RatingCached;

                LeagueCached = await WaitForGameEventAsync(GameEvent.rating_update) ?
                    (int)GameEvents[GameEvent.rating_update][rankedFormat]["new_league"] : LeagueCached;

                int ratingChange = newRating - RatingCached;
                RatingCached = newRating;

                int battleResult = 0;
                if ((string)GameEvents[GameEvent.battle_result]["winner"] == Username)
                {
                    battleResult = 1;
                    if (Reward.Quest != null && await WaitForGameEventAsync(GameEvent.quest_progress))
                    {
                        Reward.Quest.TotalItems++;
                    }
                }
                else if ((string)GameEvents[GameEvent.battle_result]["winner"] == "DRAW")
                {
                    battleResult = 2;
                }

                string logTextBattleResult = "";

                switch (battleResult)
                {
                    case 2:
                        DrawsTotal++;
                        logTextBattleResult = "DRAW";
                        Log.WriteToLog($"{Username}: { logTextBattleResult}");
                        Log.WriteToLog($"{Username}: Rating has not changed ({ newRating })");
                        break;
                    case 1:
                        WinsTotal++;
                        //logTextBattleResult = $"You won! Reward: { spsReward } SPS";
                        logTextBattleResult = $"You won!";
                        Log.WriteToLog($"{Username}: { logTextBattleResult.Pastel(Color.Green) }");
                        Log.WriteToLog($"{Username}: New rating is { newRating } ({ ("+" + ratingChange.ToString()).Pastel(Color.Green) })");
                        break;
                    case 0:
                        LossesTotal++;
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

        private async Task ClaimQuestRewardAsync()
        {
            try
            {
                string logText;
                // Old quest types
                if (Reward.Quest != null && Reward.Quest.Name.Length > 10 && Reward.Quest.CompletedItems >= Reward.Quest.TotalItems
                    && Reward.Quest.Rewards.Type == JTokenType.Null && Reward.Quest.TotalItems > 0)
                {
                    logText = "Quest reward can be claimed";
                    Log.WriteToLog($"{Username}: {logText.Pastel(Color.Green)}");
                    // short logText:
                    logText = "Quest reward available!";
                    if (Settings.ClaimQuestReward)
                    {
                        string n = Helper.GenerateRandomString(10);
                        string json = "{\"type\":\"quest\",\"quest_id\":\"" + Reward.Quest.Id + "\",\"app\":\"" + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";

                        string tx = BroadcastCustomJsonToHiveNode("sm_claim_reward", json);
                        if (await WaitForTransactionSuccessAsync(tx, 45))
                        {
                            Log.WriteToLog($"{Username}: { "Claimed quest reward:".Pastel(Color.Green) } {tx}");
                            APICounter = 100; // set api counter to 100 to reload quest
                        }
                    }
                }
                // Focus quest
                else if (Reward.Quest != null && Reward.Quest.Rewards.Type == JTokenType.Null && Reward.Quest.TotalItems == 0 && Reward.Quest.IsExpired)
                {
                    logText = "Focus quest reward can be claimed";
                    Log.WriteToLog($"{Username}: {logText.Pastel(Color.Green)}");
                    // short logText:
                    logText = "Quest reward available!";
                    if (Settings.ClaimQuestReward)
                    {
                        string n = Helper.GenerateRandomString(10);
                        string json = "{\"type\":\"quest\",\"quest_id\":\"" + Reward.Quest.Id + "\",\"app\":\"" + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";
                        string tx = BroadcastCustomJsonToHiveNode("sm_claim_reward", json);

                        if (await WaitForTransactionSuccessAsync(tx, 45))
                        {
                            Log.WriteToLog($"{Username}: { "Claimed focus quest reward:".Pastel(Color.Green) } {tx}");
                        }
                    }
                }
                else
                {
                    Log.WriteToLog($"{Username}: No quest reward to be claimed");
                    // short logText:
                    logText = "No quest reward...";
                }

                if (Reward.Quest != null)
                {
                    // temp workaround
                    if (Settings.QuestTypes.ContainsKey(Reward.Quest.Name))
                    {
                        logText = Settings.QuestTypes[Reward.Quest.Name] + ": " + logText;
                    }
                    else
                    {
                        logText = "unknown quest type";
                    }
                }
                LogSummary.QuestStatus = logText;
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at claiming quest rewards: {ex}", Log.LogType.Error);
            }
        }

        private static double GetEcrFromPlayerBalances(JArray playerBalances)
        {
            JToken balanceInfo = playerBalances.Where(x => (string)x["token"] == "ECR").First();
            if (balanceInfo["last_reward_time"].Type == JTokenType.Null) return 100;
            var captureRate = (int)balanceInfo["balance"];
            DateTime lastRewardTime = (DateTime)balanceInfo["last_reward_time"];
            double ecrRegen = 0.0868;
            double ecr = captureRate + (new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds() - new DateTimeOffset(lastRewardTime).ToUnixTimeMilliseconds()) / 3000 * ecrRegen;
            return Math.Min(ecr, 10000) / 100;
        }

        private async Task AdvanceLeagueAsync()
        {
            try
            {
                if (!Settings.AdvanceLeague || RatingCached == -1 || RatingCached < 1000)
                {
                    return;
                }


                int highestPossibleLeague = GetMaxLeagueByRankAndPower();
                int highestPossibleLeagueTier = GetTier(highestPossibleLeague);
                if (highestPossibleLeague > LeagueCached && highestPossibleLeagueTier <= Settings.MaxLeagueTier)
                {
                    Log.WriteToLog($"{Username}: { "Advancing to higher league!".Pastel(Color.Green)}");

                    string n = Helper.GenerateRandomString(10);
                    string json;
                    if (Settings.RankedFormat == "MODERN")
                    {
                        json = "{\"notify\":\"false\",\"format\":\"modern\",\"app\":\"" + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";
                    }
                    else
                    {
                        json = "{\"notify\":\"false\",\"app\":\"" + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";
                    }

                    //CtransactionData oTransaction = Settings.oHived.CreateTransaction(new object[] { custom_Json }, new string[] { PostingKey });
                    string tx = BroadcastCustomJsonToHiveNode("sm_advance_league", json);
                    //var postData = GetStringForSplinterlandsAPI(oTransaction);
                    //string response = HttpWebRequest.WebRequestPost(Settings.CookieContainer, postData, Settings.SPLINTERLANDS_BROADCAST_URL, "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:93.0) Gecko/20100101 Firefox/93.0", "https://splinterlands.com/", Encoding.UTF8);

                    //string tx = Helper.DoQuickRegex("id\":\"(.*?)\"", response);
                    if (await WaitForTransactionSuccessAsync(tx, 45))
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

        private async Task RequestNewQuestViaAPIAsync()
        {
            try
            {
                if (!Settings.ClaimQuestReward || PowerCached < Settings.MinimumBattlePower)
                {
                    return;
                }
                if (Reward.Quest != null && Reward.Quest.IsExpired && Reward.Quest.Name.Length < 11) // name length for old quest
                {
                    string n = Helper.GenerateRandomString(10);
                    string json = "{\"type\":\"daily\",\"app\":\"" + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";

                    string tx = BroadcastCustomJsonToHiveNode("sm_start_quest", json);
                    Log.WriteToLog($"{Username}: Requesting new quest because 24 hours passed: {tx}");
                    await Task.Delay(12500); // wait for splinterlands to refresh the quest
                    Reward.Quest = await SplinterlandsAPI.GetPlayerQuestAsync(Username);
                }

                // Check for bad quest
                if (Reward.Quest != null && Reward.Quest.RefreshTrxID == null 
                    && Settings.QuestTypes.ContainsKey(Reward.Quest.Name)
                    && Settings.BadQuests.Contains(Settings.QuestTypes[Reward.Quest.Name]))
                {
                    string n = Helper.GenerateRandomString(10);
                    string json = "{\"type\":\"daily\",\"app\":\"" + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";

                    string tx = BroadcastCustomJsonToHiveNode("sm_refresh_quest", json);
                    Log.WriteToLog($"{Username}: Requesting new quest because of bad element: {tx}");
                    await Task.Delay(12500); // wait for splinterlands to refresh the quest
                    Reward.Quest = await SplinterlandsAPI.GetPlayerQuestAsync(Username);
                }
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at changing quest: {ex}", Log.LogType.Error);
            }
        }

        private int GetTier(int league)
        {
            // novice
            if (league == 0)
            {
                return -1;
            }
            // bronze
            if (league is >= 1 and <= 3)
            {
                return 0;
            }
            // silver
            if (league is >= 4 and <= 6)
            {
                return 1;
            }
            // gold
            if (league is >= 5 and <= 8)
            {
                return 2;
            }
            // diamond
            if (league is >= 5 and <= 8)
            {
                return 3;
            }
            return 4;
        }

        private int GetMaxLeagueByRankAndPower()
        {
            // bronze
            int league = 0;
            if (RatingCached is >= 100 and <= 399 && PowerCached is >= 0)
            {
                league = 1;
            }
            if (RatingCached is >= 400 && PowerCached is >= 1000)
            {
                league = 2;
            }
            if (RatingCached is >= 700 && PowerCached is >= 5000)
            {
                league = 3;
            }
            // silver
            if (RatingCached is >= 1000 && PowerCached is >= 15000)
            {
                league = 4;
            }
            if (RatingCached is >= 1300 && PowerCached is >= 40000)
            {
                league = 5;
            }
            if (RatingCached is >= 1600 && PowerCached is >= 70000)
            {
                league = 6;
            }
            // gold
            if (RatingCached is >= 1900 && PowerCached is >= 100000)
            {
                league = 7;
            }
            if (RatingCached is >= 2200 && PowerCached is >= 150000)
            {
                league = 8;
            }
            if (RatingCached is >= 2500 && PowerCached is >= 200000)
            {
                league = 9;
            }
            // diamond
            if (RatingCached is >= 2800 && PowerCached is >= 250000)
            {
                league = 10;
            }
            if (RatingCached is >= 3100 && PowerCached is >= 325000)
            {
                league = 11;
            }
            if (RatingCached is >= 3400 && PowerCached is >= 400000)
            {
                league = 12;
            }
            return league;
        }
    }
}
