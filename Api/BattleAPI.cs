using Newtonsoft.Json;
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
using Ultimate_Splinterlands_Bot_V2.Config;
using Ultimate_Splinterlands_Bot_V2.Http;
using Ultimate_Splinterlands_Bot_V2.Model;
using Ultimate_Splinterlands_Bot_V2.Utils;

namespace Ultimate_Splinterlands_Bot_V2.Api
{
    public static class BattleAPI
    {
        public static async Task<JToken> GetTeamFromAPIAsync(int rating, int mana, string rules, string[] splinters, Card[] cards, Quest quest, int chestTier, string username, string gameIdHash, bool secondTry = false, bool ignorePrivateAPI = false)
        {
            if (Settings.UsePrivateAPI && !ignorePrivateAPI)
            {
                if (Settings.PrivateAPIUrl.Contains("/v2/"))
                {
                    return await GetTeamFromPrivateAPIV2Async(rating, mana, rules, splinters, cards, quest, chestTier, username, gameIdHash, secondTry);
                }
                return await GetTeamFromPrivateAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, gameIdHash, secondTry);
            }
            else
            {
                if (Settings.PublicAPIUrl.Contains("/v2/"))
                {
                    return await GetTeamFromPublicAPIV2Async(rating, mana, rules, splinters, cards, quest, chestTier, username, secondTry);
                }
                return await GetTeamFromPublicAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, secondTry);
            }
        }

        private static async Task<JToken> GetTeamFromPublicAPIAsync(int rating, int mana, string rules, string[] splinters, Card[] cards, Quest quest, int chestTier, string username, bool secondTry = false)
        {
            string APIResponse = "";
            Log.WriteToLog($"{username}: Requesting team from public API...");
            try
            {
                JObject matchDetails = new(
                        new JProperty("mana", mana),
                        new JProperty("rules", rules),
                        new JProperty("splinters", splinters),
                        new JProperty("myCardsV2", JsonConvert.SerializeObject(cards)),
                        new JProperty("quest", ""), // disabled for old api
                        new JProperty("card_settings", Settings.CardSettings.USE_CARD_SETTINGS ? JsonConvert.SerializeObject(Settings.CardSettings) : "")
                    );

                string urlGetTeam = $"{Settings.PublicAPIUrl}get_team/";
                string urlGetTeamByHash = $"{Settings.PublicAPIUrl}get_team_by_hash/";
                APIResponse = await PostJSONToApi(matchDetails, urlGetTeam, username);
                int counter = 0;
                do
                {
                    Log.WriteToLog($"{username}: API Response: {APIResponse.Pastel(Color.Yellow) }", debugOnly: true);
                    if (APIResponse.Contains("hash"))
                    {
                        Log.WriteToLog($"{username}: Waiting 10 seconds for API to calculate team...");
                        await Task.Delay(10 * 1000);
                        JObject hashData = new(new JProperty("hash", APIResponse.Split(":")[1]));
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
                        return await GetTeamFromPublicAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, true);
                    }
                    else
                    {
                        var sw = new Stopwatch();
                        sw.Start();
                        Log.WriteToLog($"{username}: API Rate Limit reached! Waiting until no longer blocked...", Log.LogType.Warning);
                        await CheckRateLimitLoopAsync(username, Settings.PublicAPIUrl);
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
                Log.WriteToLog($"{username}: API Error: {ex} - API response: {APIResponse}", Log.LogType.CriticalError);
                if (!secondTry)
                {
                    Log.WriteToLog($"{username}: Trying again...", Log.LogType.CriticalError);
                    await Task.Delay(2000);
                    return await GetTeamFromPublicAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, true);
                }
                else if (secondTry)
                {
                    Log.WriteToLog($"{username}: API overloaded or down?: Waiting 10 minutes...", Log.LogType.Warning);
                    await Task.Delay(1000 * 60 * 10);
                    return await GetTeamFromPublicAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, true);
                }
            }
            return null;
        }

        private static async Task<JToken> GetTeamFromPrivateAPIAsync(int rating, int mana, string rules, string[] splinters, Card[] cards, Quest quest, int chestTier, string username, string gameIdHash, bool secondTry = false)
        {
            Log.WriteToLog($"{username}: Requesting team from private API...");
            try
            {
                JObject matchDetails = new(
                        new JProperty("mana", mana),
                        new JProperty("rules", rules),
                        new JProperty("splinters", splinters),
                        new JProperty("quest", ""), // disabled for old api
                        new JProperty("card_settings", Settings.CardSettings.USE_CARD_SETTINGS ? JsonConvert.SerializeObject(Settings.CardSettings) : "")
                    ) ;

                string urlGetTeam = $"{Settings.PrivateAPIUrl}get_team_private/{username}/{gameIdHash}";
                string APIResponse = await PostJSONToApi(matchDetails, urlGetTeam, username);
                Log.WriteToLog($"{username}: API Response: {APIResponse.Pastel(Color.Yellow) }", debugOnly: true);

                if (APIResponse.Contains("api limit reached"))
                {
                    // this should not occur with the private API but best to check for it just in case
                    if (APIResponse.Contains("overload"))
                    {
                        Log.WriteToLog($"{username}: API Overloaded! Waiting 25 seconds and trying again after...", Log.LogType.Warning);
                        System.Threading.Thread.Sleep(25000);
                        return await GetTeamFromPrivateAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, gameIdHash, true);
                    }
                    else
                    {
                        Log.WriteToLog($"{username}: Private API Rate Limit reached! This should not happen unless there is an error or you are abusing it!", Log.LogType.CriticalError);
                        await CheckRateLimitLoopAsync(username, Settings.PrivateAPIUrl);
                    }
                }
                else if (APIResponse.Contains("API Error") && !secondTry)
                {
                    Log.WriteToLog($"{username}: Private API doesn't seem to have card data yet - using free API", Log.LogType.Warning);
                    System.Threading.Thread.Sleep(25000);
                    return await GetTeamFromAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, gameIdHash, false, true);

                }
                else if (APIResponse.Contains("Account not allowed"))
                {
                    Log.WriteToLog($"{username}: Private API Error: Account not allowed", Log.LogType.CriticalError);
                    return await GetTeamFromAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, gameIdHash, false, true);
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
                    return await GetTeamFromPrivateAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, gameIdHash, true);
                }
                else if (secondTry)
                {
                    Log.WriteToLog($"{username}: Private API down? Trying public API...", Log.LogType.Warning);
                    return await GetTeamFromAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, gameIdHash, false, true);
                }
            }
            return null;
        }
        private static async Task<JToken> GetTeamFromPublicAPIV2Async(int rating, int mana, string rules, string[] splinters, Card[] cards, Quest quest, int chestTier, string username, bool secondTry = false)
        {
            string APIResponse = "";
            Log.WriteToLog($"{username}: Requesting team from public API...");
            try
            {
                bool chestTierReached = quest != null && quest.ChestTier != null && chestTier >= quest.ChestTier;
                JObject matchDetails = new(
                        new JProperty("mana", mana),
                        new JProperty("rules", rules),
                        new JProperty("splinters", splinters),
                        new JProperty("myCardsV2", JsonConvert.SerializeObject(cards)),
                        new JProperty("focus", 
                            Settings.PrioritizeQuest 
                            && (!Settings.CardSettings.DISABLE_FOCUS_PRIORITY_BEFORE_CHEST_LEAGUE_RATING || chestTierReached) 
                            && quest != null && !quest.IsExpired && Settings.QuestTypes.ContainsKey(quest.Name)
                                ? Settings.QuestTypes[quest.Name] : ""),
                        new JProperty("chest_tier_reached", chestTierReached),
                        new JProperty("card_settings", Settings.CardSettings.USE_CARD_SETTINGS ? JsonConvert.SerializeObject(Settings.CardSettings) : "")
                    );

                string urlGetTeam = $"{Settings.PublicAPIUrl}get_team/{rating}";
                APIResponse = await PostJSONToApi(matchDetails, urlGetTeam, username);

                if (APIResponse.Contains("api limit reached"))
                {
                    if (APIResponse.Contains("overload"))
                    {
                        Log.WriteToLog($"{username}: API Overloaded! Waiting 25 seconds and trying again after...", Log.LogType.Warning);
                        System.Threading.Thread.Sleep(25000);
                        return await GetTeamFromPublicAPIV2Async(rating, mana, rules, splinters, cards, quest, chestTier, username, true);
                    }
                    else
                    {
                        var sw = new Stopwatch();
                        sw.Start();
                        Log.WriteToLog($"{username}: API Rate Limit reached! Waiting until no longer blocked...", Log.LogType.Warning);
                        await CheckRateLimitLoopAsync(username, Settings.PublicAPIUrl);
                        sw.Stop();
                        // return null so team doesn't get submitted
                        if (sw.Elapsed.TotalSeconds > 200)
                        {
                            return null;
                        }
                    }
                }

                Log.WriteToLog("API Response: " + JsonConvert.SerializeObject(matchDetails), debugOnly: true);
                if (APIResponse == null || APIResponse.Length < 5 || APIResponse.Contains("hash"))
                {
                    Log.WriteToLog($"{username}: API Error: Response was empty", Log.LogType.CriticalError);
                    return null;
                }

                return JToken.Parse(APIResponse);
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{username}: API Error: {ex} - API response: {APIResponse}", Log.LogType.CriticalError);
                if (!secondTry)
                {
                    Log.WriteToLog($"{username}: Trying again...", Log.LogType.CriticalError);
                    await Task.Delay(2000);
                    return await GetTeamFromPublicAPIV2Async(rating, mana, rules, splinters, cards, quest, chestTier, username, true);
                }
                else if (secondTry)
                {
                    Log.WriteToLog($"{username}: API overloaded or down?: Waiting 10 minutes...", Log.LogType.Warning);
                    await Task.Delay(1000 * 60 * 10);
                    return await GetTeamFromPublicAPIV2Async(rating, mana, rules, splinters, cards, quest, chestTier, username, true);
                }
            }
            return null;
        }
        private static async Task<JToken> GetTeamFromPrivateAPIV2Async(int rating, int mana, string rules, string[] splinters, Card[] cards, Quest quest, int chestTier, string username, string gameIdHash, bool secondTry = false)
        {
            Log.WriteToLog($"{username}: Requesting team from private API...");
            try
            {
                bool chestTierReached = quest != null && quest.ChestTier != null && chestTier >= quest.ChestTier;
                JObject matchDetails = new(
                        new JProperty("mana", mana),
                        new JProperty("rules", rules),
                        new JProperty("splinters", splinters),
                        new JProperty("focus",
                            Settings.PrioritizeQuest
                            && (!Settings.CardSettings.DISABLE_FOCUS_PRIORITY_BEFORE_CHEST_LEAGUE_RATING || chestTierReached)
                            && quest != null && !quest.IsExpired && Settings.QuestTypes.ContainsKey(quest.Name)
                                ? Settings.QuestTypes[quest.Name] : ""),
                        new JProperty("chest_tier_reached", chestTierReached),
                        new JProperty("card_settings", Settings.CardSettings.USE_CARD_SETTINGS ? JsonConvert.SerializeObject(Settings.CardSettings) : "")
                    );

                string urlGetTeam = $"{Settings.PrivateAPIUrl}get_team_private/{username}/{rating}/{gameIdHash}";
                string APIResponse = await PostJSONToApi(matchDetails, urlGetTeam, username);
                Log.WriteToLog($"{username}: API Response: {APIResponse.Pastel(Color.Yellow) }", debugOnly: true);

                if (APIResponse.Contains("api limit reached"))
                {
                    // this should not occur with the private API but best to check for it just in case
                    if (APIResponse.Contains("overload"))
                    {
                        Log.WriteToLog($"{username}: API Overloaded! Waiting 25 seconds and trying again after...", Log.LogType.Warning);
                        System.Threading.Thread.Sleep(25000);
                        return await GetTeamFromPrivateAPIV2Async(rating, mana, rules, splinters, cards, quest, chestTier, username, gameIdHash, true);
                    }
                    else
                    {
                        Log.WriteToLog($"{username}: Private API Rate Limit reached! This should not happen unless there is an error or you are abusing it!", Log.LogType.CriticalError);
                        await CheckRateLimitLoopAsync(username, Settings.PrivateAPIUrl);
                    }
                }
                else if (APIResponse.Contains("API Error") && !secondTry)
                {
                    Log.WriteToLog($"{username}: Private API doesn't seem to have card data yet - using free API", Log.LogType.Warning);
                    System.Threading.Thread.Sleep(25000);
                    return await GetTeamFromAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, gameIdHash, false, true);

                }
                else if (APIResponse.Contains("Account not allowed"))
                {
                    Log.WriteToLog($"{username}: Private API Error: Account not allowed", Log.LogType.CriticalError);
                    return await GetTeamFromAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, gameIdHash, false, true);
                }

                Log.WriteToLog("API Response: " + JsonConvert.SerializeObject(matchDetails), debugOnly: true);
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
                    return await GetTeamFromPrivateAPIV2Async(rating, mana, rules, splinters, cards, quest, chestTier, username, gameIdHash, true);
                }
                else if (secondTry)
                {
                    Log.WriteToLog($"{username}: Private API down? Trying public API...", Log.LogType.Warning);
                    return await GetTeamFromAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, gameIdHash, false, true);
                }
            }
            return null;
        }

        public static void ReportLoss(string enemy, string username)
        {
            _ = Helper.DownloadPageAsync($"{ Settings.PublicAPIUrl }report_loss/{enemy}/{username}");
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

        public static void UpdateAccountInfoForPrivateAPI(string username, double ecr, JArray playerBalances, int wildRating, int wildRank, int modernRating, int modernRank, int power)
        {

            double dec = GetTokenBalance(playerBalances, "DEC");
            double sps = GetTokenBalance(playerBalances, "SPS");
            double stakedSps = GetTokenBalance(playerBalances, "SPSP");
            double credits = GetTokenBalance(playerBalances, "CREDITS");
            int chaosLegionPacks = (int)GetTokenBalance(playerBalances, "CHAOS");
            int goldPotion = (int)GetTokenBalance(playerBalances, "GOLD");
            int legendaryPotion = (int)GetTokenBalance(playerBalances, "LEGENDARY");
            int merits = (int)GetTokenBalance(playerBalances, "MERITS");
            double vouchers = GetTokenBalance(playerBalances, "VOUCHER");
            string postData = ($"account={username}&ecr={ecr}&wildRank={wildRank}&wildRating={wildRating}&modernRating={modernRating}"
                + $"&modernRank={modernRank}&dec={dec}&sps={sps}&stakedSps={stakedSps}&power={power}&credits={credits}&chaosLegionPacks={chaosLegionPacks}"
                + $"&goldPotion={goldPotion}&legendaryPotion={legendaryPotion}&merits={merits}&vouchers={vouchers}").Replace(",", ".");
            string response = HttpWebRequest.WebRequestPost(Settings.CookieContainer, postData, Settings.PrivateAPIShop + "index.php?site=updateaccountinfo", "", "", Encoding.Default);

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

            response = HttpWebRequest.WebRequestPost(Settings.CookieContainer, postData, Settings.PrivateAPIShop + "index.php?site=updateaccountinfo", "", "", Encoding.Default);

            if (!response.Contains("success"))
            {
                Log.WriteToLog($"{username}: Failed to update account information for private API tracker: +  " + response, Log.LogType.Error);
            }
        }

        private static double GetTokenBalance(JArray playerBalances, string token)
        {
            JToken balanceInfo = playerBalances.Where(x => (string)x["token"] == token).FirstOrDefault();
            return balanceInfo != null ? (double)balanceInfo["balance"] : 0;
        }

        public async static Task CheckRateLimitLoopAsync(string username, string apiUrl)
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
                    APIResponse = await Helper.DownloadPageAsync($"{apiUrl}rate_limited/");
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
