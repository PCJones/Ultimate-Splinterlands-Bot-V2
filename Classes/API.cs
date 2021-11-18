﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Pastel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Ultimate_Splinterlands_Bot_V2.Classes
{
    public static class API
    {
        private const string SplinterlandsAPI = "https://api2.splinterlands.com";
        private const string SplinterlandsAPIFallback = "https://game-api.splinterlands.io";

        public static async Task<JToken> GetTeamFromAPIAsync(int mana, string rules, string[] splinters, Card[] cards, JToken quest, JToken questLessDetails, string username, bool secondTry = false, bool ignorePrivateAPI = false)
        {
            if (Settings.UsePrivateAPI && !ignorePrivateAPI)
            {
                return await GetTeamFromPrivateAPIAsync(mana, rules, splinters, cards, quest, questLessDetails, username, secondTry);
            }
            else
            {
                return await GetTeamFromPublicAPIAsync(mana, rules, splinters, cards, quest, questLessDetails, username, secondTry);
            }
        }

        private static async Task<JToken> GetTeamFromPublicAPIAsync(int mana, string rules, string[] splinters, Card[] cards, JToken quest, JToken questLessDetails, string username, bool secondTry = false)
        {
            Log.WriteToLog($"{username}: Requesting team from public API...");
            try
            {
                JObject matchDetails = new JObject(
                        new JProperty("mana", mana),
                        new JProperty("rules", rules),
                        new JProperty("splinters", splinters),
                        new JProperty("myCardsV2", JsonConvert.SerializeObject(cards)),
                        new JProperty("quest", Settings.PrioritizeQuest && quest != null
                        && ((int)questLessDetails["total"] != (int)questLessDetails["completed"]) ?
                        questLessDetails : "")
                    );

                string urlGetTeam = $"{Settings.PublicAPIUrl}get_team/";
                string urlGetTeamByHash = $"{Settings.PublicAPIUrl}get_team_by_hash/";
                string APIResponse = await PostJSONToApi(matchDetails, urlGetTeam, username);
                int counter = 0;
                do
                {
                    Log.WriteToLog($"{username}: API Response: {APIResponse.Pastel(Color.Yellow) }", debugOnly: true);
                    if (APIResponse.Contains("hash"))
                    {
                        Log.WriteToLog($"{username}: Waiting 10 seconds for API to calculate team...");
                        await Task.Delay(10 * 1000);
                        JObject hashData = new JObject(new JProperty("hash", APIResponse.Split(":")[1]));
                        APIResponse = await PostJSONToApi(hashData, urlGetTeamByHash, username);
                    }
                    else
                    {
                        break;
                    }
                } while (counter++ < 19);

                if (APIResponse.Contains("api limit reached"))
                {
                    if (APIResponse.Contains("overload"))
                    {
                        Log.WriteToLog($"{username}: API Overloaded! Waiting 25 seconds and trying again after...", Log.LogType.Warning);
                        System.Threading.Thread.Sleep(25000);
                        return await GetTeamFromPublicAPIAsync(mana, rules, splinters, cards, quest, questLessDetails, username, true);
                    }
                    else
                    {
                        var sw = new Stopwatch();
                        sw.Start();
                        Log.WriteToLog($"{username}: API Rate Limit reached! Waiting until no longer blocked...", Log.LogType.Warning);
                        await CheckRateLimitLoopAsync(username);
                        sw.Stop();
                        // return null so team doesn't get submitted
                        if (sw.Elapsed.TotalSeconds > 200)
                        {
                            return null;
                        }
                    }
                }

                if (APIResponse == null || APIResponse.Length < 5 || APIResponse.Contains("hash"))
                {
                    Log.WriteToLog($"{username}: API Error: Response was empty", Log.LogType.CriticalError);
                    return null;
                }

                return JToken.Parse(APIResponse);
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{username}: API Error: {ex}", Log.LogType.CriticalError);
                if (!secondTry)
                {
                    Log.WriteToLog($"{username}: Trying again...", Log.LogType.CriticalError);
                    await Task.Delay(2000);
                    return await GetTeamFromPublicAPIAsync(mana, rules, splinters, cards, quest, questLessDetails, username, true);
                }
                else if (secondTry)
                {
                    Log.WriteToLog($"{username}: API overloaded or down?: Waiting 10 minutes...", Log.LogType.Warning);
                    await Task.Delay(1000 * 60 * 10);
                    return await GetTeamFromPublicAPIAsync(mana, rules, splinters, cards, quest, questLessDetails, username, true);
                }
            }
            return null;
        }

        private static async Task<JToken> GetTeamFromPrivateAPIAsync(int mana, string rules, string[] splinters, Card[] cards, JToken quest, JToken questLessDetails, string username, bool secondTry = false)
        {
            Log.WriteToLog($"{username}: Requesting team from private API...");
            try
            {
                JObject matchDetails = new JObject(
                        new JProperty("mana", mana),
                        new JProperty("rules", rules),
                        new JProperty("splinters", splinters),
                        new JProperty("quest", Settings.PrioritizeQuest && quest != null
                        && ((int)questLessDetails["total"] != (int)questLessDetails["completed"]) ?
                        questLessDetails : "")
                    );

                string urlGetTeam = $"{Settings.PrivateAPIUrl}get_team_private/{username}/";
                string APIResponse = await PostJSONToApi(matchDetails, urlGetTeam, username);
                Log.WriteToLog($"{username}: API Response: {APIResponse.Pastel(Color.Yellow) }", debugOnly: true);

                if (APIResponse.Contains("api limit reached"))
                {
                    // this should not occur with the private API but best to check for it just in case
                    if (APIResponse.Contains("overload"))
                    {
                        Log.WriteToLog($"{username}: API Overloaded! Waiting 25 seconds and trying again after...", Log.LogType.Warning);
                        System.Threading.Thread.Sleep(25000);
                        return await GetTeamFromPrivateAPIAsync(mana, rules, splinters, cards, quest, questLessDetails, username, true);
                    }
                    else
                    {
                        Log.WriteToLog($"{username}: API Rate Limit reached! Waiting until no longer blocked...", Log.LogType.Warning);
                        await CheckRateLimitLoopAsync(username);
                    }
                }
                else if (APIResponse.Contains("API Error") && !secondTry)
                {
                    Log.WriteToLog($"{username}: Private API doesn't seem to have card data yet - using free API", Log.LogType.Warning);
                    System.Threading.Thread.Sleep(25000);
                    return await GetTeamFromAPIAsync(mana, rules, splinters, cards, quest, questLessDetails, username, false, true);

                }
                else if (APIResponse.Contains("Account not allowed"))
                {
                    Log.WriteToLog($"{username}: Private API Error: Account not allowed", Log.LogType.CriticalError);
                    return await GetTeamFromAPIAsync(mana, rules, splinters, cards, quest, questLessDetails, username, false, true);
                }
                if (APIResponse == null || APIResponse.Length < 5)
                {
                    Log.WriteToLog($"{username}: API Error: Response was empty", Log.LogType.CriticalError);
                    return null;
                }

                return JToken.Parse(APIResponse);
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{username}: API Error: {ex}", Log.LogType.CriticalError);
                if (!secondTry)
                {
                    Log.WriteToLog($"{username}: Trying again...", Log.LogType.CriticalError);
                    await Task.Delay(2000);
                    return await GetTeamFromPrivateAPIAsync(mana, rules, splinters, cards, quest, questLessDetails, username, true);
                }
                else if (secondTry)
                {
                    Log.WriteToLog($"{username}: Private API down? Trying public API...", Log.LogType.Warning);
                    return await GetTeamFromAPIAsync(mana, rules, splinters, cards, quest, questLessDetails, username, false, true);
                }
            }
            return null;
        }

        public static void ReportLoss(string enemy, string username)
        {
            _ = DownloadPageAsync($"{ Settings.PublicAPIUrl }report_loss/{enemy}/{username}");
        }
        public static async Task<bool> CheckForMaintenance()
        {
            try
            {
                string data = await DownloadPageAsync($"{SplinterlandsAPI}/settings");
                if (data == null || data.Trim().Length < 10 || data.Contains("502 Bad Gateway") || data.Contains("Cannot GET"))
                {
                    // Fallback API
                    Log.WriteToLog($"Error with splinterlands API for settings, trying fallback api...", Log.LogType.Warning);
                    data = await DownloadPageAsync($"{SplinterlandsAPIFallback}/settings");
                }
                return (bool)JToken.Parse(data)["maintenance_mode"];
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"Could not get settings from splinterlands api: {ex}", Log.LogType.Error);
            }
            return true;
        }
        public static async Task<(int power, int rating, int league)> GetPlayerDetailsAsync(string username)
        {
            try
            {
                string data = await DownloadPageAsync($"{SplinterlandsAPI}/players/details?name={ username }");
                if (data == null || data.Trim().Length < 10 || data.Contains("502 Bad Gateway") || data.Contains("Cannot GET"))
                {
                    // Fallback API
                    Log.WriteToLog($"{username}: Error with splinterlands API for collection power, trying fallback api...", Log.LogType.Warning);
                    data = await DownloadPageAsync($"{SplinterlandsAPIFallback}/players/details?username={ username }");
                }
                return ((int)JToken.Parse(data)["collection_power"], (int)JToken.Parse(data)["rating"], (int)JToken.Parse(data)["league"]);
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{username}: Could not get player details from splinterlands api: {ex}", Log.LogType.Error);
            }
            return (-1, -1, -1);
        }

        public static async Task<(bool enemyHasPicked, bool surrender)> CheckEnemyHasPickedAsync(string username, string tx)
        {
            try
            {
                string data = await DownloadPageAsync($"{SplinterlandsAPI}/players/outstanding_match?username={ username }");
                if (data == null || data.Trim().Length < 10 || data.Contains("502 Bad Gateway") || data.Contains("Cannot GET"))
                {
                    // Fallback API
                    // wait 10 seconds just in case for this method
                    
                    Log.WriteToLog($"{username}: Error with splinterlands API for ongoing game, trying fallback api...", Log.LogType.Warning);
                    data = await DownloadPageAsync($"{SplinterlandsAPIFallback}/players/outstanding_match?username={ username }");
                }

                // Check for surrender
                if (data == "null")
                {
                    return (true, true);
                }
                var matchInfo = JToken.Parse(data);

                return matchInfo["opponent_team_hash"].Type != JTokenType.Null ? (true, false) : (false, false);
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{username}: Could not get ongoing game from splinterlands api: {ex}", Log.LogType.Error);
            }
            return (true, true);
        }

        public static async Task<(int newRating, int ratingChange, decimal decReward, int result)> GetBattleResultAsync(string username, string tx)
        {
            try
            {
                string data = await DownloadPageAsync($"{SplinterlandsAPI}/battle/history2?player={ username }");
                if (data == null || data.Trim().Length < 10 || data.Contains("502 Bad Gateway") || data.Contains("Cannot GET"))
                {
                    // Fallback API
                    Log.WriteToLog($"{username}: Error with splinterlands API for collection power, trying fallback api...", Log.LogType.Warning);
                    data = await DownloadPageAsync($"{SplinterlandsAPIFallback}/battle/history2?player={ username }");
                }

                var matchHistory = JToken.Parse(data);

                // Battle not yet finished (= not yet shown in history)?
                if ((string)matchHistory["battles"][0]["battle_queue_id_1"] != tx && (string)matchHistory["battles"][0]["battle_queue_id_2"] != tx)
                {
                    return (-1, -1, -1, -1);
                }

                int gameResult = 0;
                if ((string)matchHistory["battles"][0]["winner"] == username)
                {
                    gameResult = 1;
                } else if((string)matchHistory["battles"][0]["winner"] == "DRAW")
                {
                    gameResult = 2;
                }

                int newRating = (string)matchHistory["battles"][0]["player_1"] == username ? ((int)matchHistory["battles"][0]["player_1_rating_final"]) :
                    ((int)matchHistory["battles"][0]["player_2_rating_final"]);
                int ratingChange = (string)matchHistory["battles"][0]["player_1"] == username ? newRating - ((int)matchHistory["battles"][0]["player_1_rating_initial"]) :
                    newRating - ((int)matchHistory["battles"][0]["player_2_rating_initial"]);
                decimal decReward = (decimal)matchHistory["battles"][0]["reward_dec"];

                return (newRating, ratingChange, decReward, gameResult);
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{username}: Could not get battle results from splinterlands api: {ex}", Log.LogType.Error);
            }
            return (-1, -1, -1, -1);
        }

        public static async Task<JToken> GetPlayerBalancesAsync(string username)
        {
            try
            {
                string data = await DownloadPageAsync($"{SplinterlandsAPI}/players/balances?username={ username }");
                if (data == null || data.Trim().Length < 10 || data.Contains("502 Bad Gateway") || data.Contains("Cannot GET"))
                {
                    // Fallback API
                    Log.WriteToLog($"{username}: Error with splinterlands API for balances, trying fallback api...", Log.LogType.Warning);
                    data = await DownloadPageAsync($"{SplinterlandsAPIFallback}/players/balances?username={ username }");
                }
                JToken balances = JToken.Parse(data);
                return balances;

            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{username}: Could not get balances from splinterlands api: {ex}", Log.LogType.Error);
            }
            return null;
        }
        public static async Task<(JToken quest, JToken questLessDetails)> GetPlayerQuestAsync(string username)
        {
            try
            {
                string data = await DownloadPageAsync($"{SplinterlandsAPI}/players/quests?username={ username }");
                if (data == null || data.Trim().Length < 10 || data.Contains("502 Bad Gateway") || data.Contains("Cannot GET"))
                {
                    // Fallback API
                    Log.WriteToLog($"{username}: Error with splinterlands API for quest, trying fallback api...", Log.LogType.Warning);
                    data = await DownloadPageAsync($"{SplinterlandsAPIFallback}/players/quests?username={ username }");
                }
                JToken quest = JToken.Parse(data)[0];

                var questLessDetails = new JObject(
                    new JProperty("name", quest["name"]),
                    new JProperty("splinter", Settings.QuestTypes[(string)quest["name"]]),
                    new JProperty("total", quest["total_items"]),
                    new JProperty("completed", quest["completed_items"])
                    );

                return (quest, questLessDetails);

            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{username}: Could not get quest from splinterlands api: {ex}", Log.LogType.Error);
            }
            return (null, null);
        }

        public static async Task<Card[]> GetPlayerCardsAsync(string username)
        {
            try
            {
                string data = await DownloadPageAsync($"{SplinterlandsAPI}/cards/collection/{ username }");
                if (data == null || data.Trim().Length < 10 || data.Contains("502 Bad Gateway") || data.Contains("Cannot GET"))
                {
                    // Fallback API
                    Log.WriteToLog($"{username}: Error with splinterlands API for cards, trying fallback api...", Log.LogType.Warning);
                    data = await DownloadPageAsync($"{SplinterlandsAPIFallback}/cards/collection/{ username }");
                }

                DateTime oneDayAgo = DateTime.Now.AddDays(-1);
                List<Card> cards = new List<Card>(JToken.Parse(data)["cards"].Where(x =>
                ((x["delegated_to"].Type == JTokenType.Null && x["market_listing_type"].Type == JTokenType.Null)
                || (string)x["delegated_to"] == username)
                &&
                    !((string)x["last_used_player"] != username &&
                        (
                            x["last_used_date"].Type != JTokenType.Null &&
                            DateTime.Parse(JsonConvert.SerializeObject(x["last_used_date"]).Replace("\"", "").Trim()) > oneDayAgo
                        )
                    )
                )
                .Select(x => new Card((string)x["card_detail_id"], (string)x["uid"], (string)x["level"], (bool)x["gold"]))
                .Distinct().OrderByDescending(x => x.SortValue()).ToArray());

                // add basic cards
                foreach (string cardId in Settings.PhantomCards)
                {
                    cards.Add(new Card(cardId, "starter-" + cardId + "-" + Helper.GenerateRandomString(5), "1", false));
                }

                // only use highest level/gold cards
                Card[] cardsFiltered = cards.Select(x => cards.Where(y => x.card_detail_id == y.card_detail_id).First()).Distinct().ToArray();

                return cardsFiltered;
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{username}: Could not get cards from splinterlands api: {ex}{Environment.NewLine}Bot will play with phantom cards only.", Log.LogType.Error);
            }
            return Settings.PhantomCards.Select(x => new Card(x, "starter-" + x + "-" + Helper.GenerateRandomString(5), "1", false)).ToArray();
        }
        private async static Task<string> DownloadPageAsync(string url)
        {
            // Use static HttpClient to avoid exhausting system resources for network connections.
            var result = await Settings._httpClient.GetAsync(url);
            if (result.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                Log.WriteToLog("Splinterlands API block - wait 180 seconds");
                await Task.Delay(180000);
            }
            var response = await result.Content.ReadAsStringAsync();
            // Write status code.
            return response;
        }

        private async static Task<string> PostJSONToApi(object json, string url, string username)
        {
            using (var content = new StringContent(JsonConvert.SerializeObject(json), Encoding.UTF8, "application/json"))
            {
                HttpResponseMessage result = await Settings._httpClient.PostAsync(url, content);
                if (result.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    string returnValue = await result.Content.ReadAsStringAsync();
                    return returnValue;
                }
                Log.WriteToLog($"{username}: Failed to POST data to API: ({result.StatusCode})");
            }
            return null;
        }

        public static void UpdateCardsForPrivateAPI(string username, Card[] cards)
        {
            string postData = "account=" + username + "&cards=" + JsonConvert.SerializeObject(cards);
            string response = HttpWebRequest.WebRequestPost(Settings.CookieContainer, postData, Settings.PrivateAPIShop + "index.php?site=updatecards", "", "", Encoding.Default);

            if (!response.Contains("success"))
            {
                string postDataLogin = "txtUsername=" + Uri.EscapeDataString(Settings.PrivateAPIUsername);
                postDataLogin += "&txtPassword=" + Uri.EscapeDataString(Settings.PrivateAPIPassword);
                postDataLogin += "&btnLoginSubmit=" + "Login";

                response = HttpWebRequest.WebRequestPost(Settings.CookieContainer, postDataLogin, Settings.PrivateAPIShop + "index.php", "", "", Encoding.Default);

                if (!response.Contains("Login Successfully"))
                {
                    Log.WriteToLog($"{username}: Failed to login into private API shop: " + response, Log.LogType.Error);
                    return;
                }
            }
            else
            {
                return;
            }

            response = HttpWebRequest.WebRequestPost(Settings.CookieContainer, postData, Settings.PrivateAPIShop + "index.php?site=updatecards", "", "", Encoding.Default);

            if (!response.Contains("success"))
            {
                Log.WriteToLog($"{username}: Failed to update cards for private API: +  " + response, Log.LogType.Error);
            }
        }
        public async static Task CheckRateLimitLoopAsync(string username)
        {
            bool alreadyChecking = false;
            lock (Settings.RateLimitedLock)
            {
                if (Settings.RateLimited)
                {
                    alreadyChecking = true;
                }
                else
                {
                    Settings.RateLimited = true;
                }
            }

            if (alreadyChecking)
            {
                while (Settings.RateLimited)
                {
                    await Task.Delay(20000);
                }
            }
            else
            {
                string APIResponse = "rate limit";
                do
                {
                    await Task.Delay(80000);
                    APIResponse = await DownloadPageAsync($"{Settings.PublicAPIUrl}rate_limited/");
                    Log.WriteToLog($"{username}: API Response: {APIResponse.Pastel(Color.Yellow) }");
                } while (APIResponse.Contains("rate limit"));
                lock (Settings.RateLimitedLock)
                {
                    Settings.RateLimited = false;
                    Log.WriteToLog($"{username}: { "No longer rate limited!".Pastel(Color.Green) }");
                }
            }
        }
    }
}
