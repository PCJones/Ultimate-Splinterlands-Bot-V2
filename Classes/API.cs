using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Pastel;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Ultimate_Splinterlands_Bot_V2.Classes
{
    public static class API
    {
        private const string SplinterlandsAPI = "https://game-api.splinterlands.io";
        private const string SplinterlandsAPIFallback = "https://api2.splinterlands.com";

        public static async Task<JToken> GetTeamFromAPIAsync(int mana, string rules, string[] splinters, Card[] cards, JToken quest, JToken questLessDetails, string username, bool secondTry = false, bool ignorePrivateAPI = false)
        {
            Log.WriteToLog($"{username}: Requesting team from API...");
            try
            {
                JObject matchDetails;
                if (Settings.UsePrivateAPI && !ignorePrivateAPI)
                {
                    matchDetails = new JObject(
                        new JProperty("mana", mana),
                        new JProperty("rules", rules),
                        new JProperty("splinters", splinters),
                        new JProperty("quest", Settings.PrioritizeQuest && quest != null
                        && ((int)questLessDetails["total"] != (int)questLessDetails["completed"]) ?
                        questLessDetails : "")
                    );
                }
                else
                {
                    matchDetails = new JObject(
                        new JProperty("mana", mana),
                        new JProperty("rules", rules),
                        new JProperty("splinters", splinters),
                        new JProperty("myCardsV2", JsonConvert.SerializeObject(cards)),
                        new JProperty("quest", Settings.PrioritizeQuest && quest != null
                        && ((int)questLessDetails["total"] != (int)questLessDetails["completed"]) ?
                        questLessDetails : "")
                    );
                }

                string url = (Settings.UsePrivateAPI && !ignorePrivateAPI) ? $"{Settings.PrivateAPIUrl}get_team_private/{username}/" : $"{Settings.APIUrl}get_team/";
                string APIResponse = await PostJSONToApi(matchDetails, url, username);
                Log.WriteToLog($"{username}: API Response: {APIResponse.Pastel(Color.Yellow) }");

                if (APIResponse.Contains("api limit reached"))
                {
                    if (APIResponse.Contains("overload"))
                    {
                        Log.WriteToLog($"{username}: API Overloaded! Waiting 25 seconds and trying again after...", Log.LogType.Warning);
                        System.Threading.Thread.Sleep(25000);
                        return await GetTeamFromAPIAsync(mana, rules, splinters, cards, quest, questLessDetails, username, true);
                    }
                    else
                    {
                        Log.WriteToLog($"{username}: API Rate Limit reached! Waiting until no longer blocked...", Log.LogType.Warning);
                        await CheckRateLimitLoopAsync(username);
                    }
                }
                else if (Settings.UsePrivateAPI && APIResponse.Contains("API Error") && !secondTry)
                {
                    Log.WriteToLog($"{username}: Private API doesn't seem to have card data yet - using free API", Log.LogType.Warning);
                    System.Threading.Thread.Sleep(25000);
                    return await GetTeamFromAPIAsync(mana, rules, splinters, cards, quest, questLessDetails, username, true, true);

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
                    return await GetTeamFromAPIAsync(mana, rules, splinters, cards, quest, questLessDetails, username, true, true);
                }
                else if (secondTry)
                {
                    Log.WriteToLog($"{username}: API overloaded or down?: Waiting 2.5 minutes...", Log.LogType.Warning);
                    System.Threading.Thread.Sleep(150000);
                }
            }
            return null;
        }

        public static void ReportLoss(string enemy, string username)
        {
            _ = DownloadPageAsync($"{ Settings.APIUrl }report_loss/{enemy}/{username}");
        }

        public static async Task<(int power, int rating, int league)> GetPlayerDetailsAsync(string username)
        {
            try
            {
                string data = await DownloadPageAsync($"{SplinterlandsAPI}/players/details?name={ username }");
                if (data == null || data.Trim().Length < 10)
                {
                    // Fallback API
                    Log.WriteToLog($"{username}: Error with splinterlands API for collection power, trying fallback api...", Log.LogType.Warning);
                    data = await DownloadPageAsync($"{SplinterlandsAPIFallback}/players/details?username={ username }");
                }
                return ((int)JToken.Parse(data)["collection_power"], (int)JToken.Parse(data)["rating"], (int)JToken.Parse(data)["league"]);
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{username}: Could not get collection power from splinterlands api: {ex}", Log.LogType.Error);
            }
            return (-1, -1, -1);
        }
        public static async Task<JToken> GetPlayerBalancesAsync(string username)
        {
            try
            {
                string data = await DownloadPageAsync($"{SplinterlandsAPI}/players/balances?username={ username }");
                if (data == null || data.Trim().Length < 10)
                {
                    // Fallback API
                    Log.WriteToLog($"{username}: Error with splinterlands API for balances, trying fallback api...", Log.LogType.Warning);
                    data = await DownloadPageAsync($"{SplinterlandsAPIFallback}/players/quests?username={ username }");
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
                if (data == null || data.Trim().Length < 10)
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
                if (data == null || data.Trim().Length < 10)
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
                    APIResponse = await DownloadPageAsync($"{Settings.APIUrl}rate_limited/");
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
