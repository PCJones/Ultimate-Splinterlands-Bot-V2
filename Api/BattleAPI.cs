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
using Ultimate_Splinterlands_Bot_V2.Config;
using Ultimate_Splinterlands_Bot_V2.Http;
using Ultimate_Splinterlands_Bot_V2.Model;
using Ultimate_Splinterlands_Bot_V2.Utils;

namespace Ultimate_Splinterlands_Bot_V2.Api
{
    public static class BattleAPI
    {
        public static async Task<JToken> GetTeamFromAPIAsync(int rating, int mana, string rules, string[] splinters, Card[] cards, Quest quest, int chestTier, string username, string gameIdHash, string apikey, bool secondTry = false, bool ignorePrivateAPI = false)
        {
            if (Settings.UsePrivateAPI && !ignorePrivateAPI)
            {
                if (Settings.PrivateAPIUrl.Contains("/v2/"))
                {
                    return await GetTeamFromPrivateAPIV2Async(rating, mana, rules, splinters, cards, quest, chestTier, username, gameIdHash, apikey, secondTry);
                }
                return await GetTeamFromPrivateAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, gameIdHash, apikey, secondTry);
            }
            else
            {
                if (Settings.PublicAPIUrl.Contains("/v2/") | Settings.PublicAPIUrl.Contains("beta") | !Settings.PublicAPIUrl.Contains("xyz/v2/"))
                {
                    return await GetTeamFromPublicAPIV2Async(rating, mana, rules, splinters, cards, quest, chestTier, username, apikey, secondTry);
                }
                return await GetTeamFromPublicAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, apikey, secondTry);
            }
        }

        private static async Task<JToken> GetTeamFromPublicAPIAsync(int rating, int mana, string rules, string[] splinters, Card[] cards, Quest quest, int chestTier, string username, string apikey, bool secondTry = false)
        {
            string APIResponse = "";
            Log.WriteToLog($"{username}: Requesting team from API...");
            try
            {
                JObject matchDetails = new JObject(
                        new JProperty("api_key", apikey),
                        new JProperty("player", username),
                        new JProperty("mana", mana),
                        new JProperty("rules", rules),
                        new JProperty("splinters", splinters),
                        new JProperty("myCardsV2", JsonConvert.SerializeObject(cards)),
                        new JProperty("quest", ""), // disabled for old api
                        new JProperty("team_settings", Settings.TeamSettings.USE_TEAM_SETTINGS ? JsonConvert.SerializeObject(Settings.TeamSettings) : ""),
                        new JProperty("card_settings", Settings.CardSettings.USE_CARD_SETTINGS ? JsonConvert.SerializeObject(Settings.CardSettings) : "")
                    );

                string urlGetTeam = $"{Settings.PublicAPIUrl}get_team/";
                string urlGetTeamFallBack = $"{Settings.FallBackPublicAPIUrl}get_team/";
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

                if (APIResponse.Contains("No Available Team"))
                {
                    Log.WriteToLog($"{username}: API Error: No Available Team", Log.LogType.Warning);
                    return null;
                }

                if (APIResponse.Contains("api limit reached") || APIResponse.Contains("Rate limit exceeded") || APIResponse.Contains("Not Authorized") || APIResponse.Contains("InternalServerError") || APIResponse == "")
                {
                    if (APIResponse.Contains("overload"))
                    {
                        Log.WriteToLog($"{username}: API Overloaded! Waiting 25 seconds and trying again after...", Log.LogType.Warning);
                        System.Threading.Thread.Sleep(25000);
                        return await GetTeamFromPublicAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, apikey, true);
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
                    return await GetTeamFromPublicAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, apikey, true);
                }
                else if (secondTry)
                {
                    Log.WriteToLog($"{username}: API overloaded or down?: Waiting 10 minutes...", Log.LogType.Warning);
                    await Task.Delay(1000 * 60 * 10);
                    return await GetTeamFromPublicAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, apikey, true);
                }
            }
            return null;
        }

        private static async Task<JToken> GetTeamFromPrivateAPIAsync(int rating, int mana, string rules, string[] splinters, Card[] cards, Quest quest, int chestTier, string username, string gameIdHash, string apikey, bool secondTry = false)
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
                    );

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
                        return await GetTeamFromPrivateAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, gameIdHash, apikey, true);
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
                    return await GetTeamFromAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, gameIdHash, apikey, false, true);

                }
                else if (APIResponse.Contains("Account not allowed"))
                {
                    Log.WriteToLog($"{username}: Private API Error: Account not allowed", Log.LogType.CriticalError);
                    return await GetTeamFromAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, gameIdHash, apikey, false, true);
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
                    return await GetTeamFromPrivateAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, gameIdHash, apikey, true);
                }
                else if (secondTry)
                {
                    Log.WriteToLog($"{username}: Private API down? Trying public API...", Log.LogType.Warning);
                    return await GetTeamFromAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, gameIdHash, apikey, false, true);
                }
            }
            return null;
        }
        private static async Task<JToken> GetTeamFromPublicAPIV2Async(int rating, int mana, string rules, string[] splinters, Card[] cards, Quest quest, int chestTier, string username, string apikey, bool secondTry = false)
        {
            string APIResponse = "";
            Log.WriteToLog($"{username}: Requesting team from public API...");
            try
            {
                bool chestTierReached = quest != null && quest.ChestTier != null && chestTier >= quest.ChestTier;
                JObject matchDetails = new(
                        new JProperty("api_key", apikey),
                        new JProperty("player", username),
                        new JProperty("mana", mana),
                        new JProperty("rules", rules),
                        new JProperty("splinters", splinters),
                        new JProperty("myCardsV2", JsonConvert.SerializeObject(cards)),
                        new JProperty("focus", Settings.PrioritizeQuest && chestTierReached && quest != null && !quest.IsExpired ? Settings.QuestTypes[quest.Name] : ""),
                        new JProperty("chest_tier_reached", chestTierReached),
                        new JProperty("team_settings", Settings.TeamSettings.USE_TEAM_SETTINGS ? JsonConvert.SerializeObject(Settings.TeamSettings) : ""),
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
                        return await GetTeamFromPublicAPIV2Async(rating, mana, rules, splinters, cards, quest, chestTier, username, apikey, true);
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
                    return await GetTeamFromPublicAPIV2Async(rating, mana, rules, splinters, cards, quest, chestTier, username, apikey, true);
                }
                else if (secondTry)
                {
                    Log.WriteToLog($"{username}: API overloaded or down?: Waiting 10 minutes...", Log.LogType.Warning);
                    await Task.Delay(1000 * 60 * 10);
                    return await GetTeamFromPublicAPIV2Async(rating, mana, rules, splinters, cards, quest, chestTier, username, apikey, true);
                }
            }
            return null;
        }
        private static async Task<JToken> GetTeamFromPrivateAPIV2Async(int rating, int mana, string rules, string[] splinters, Card[] cards, Quest quest, int chestTier, string username, string gameIdHash, string apikey, bool secondTry = false)
        {
            Log.WriteToLog($"{username}: Requesting team from private API...");
            try
            {
                bool chestTierReached = quest != null && chestTier >= quest.ChestTier;
                JObject matchDetails = new(
                        new JProperty("mana", mana),
                        new JProperty("rules", rules),
                        new JProperty("splinters", splinters),
                        new JProperty("focus", Settings.PrioritizeQuest && chestTierReached && quest != null && !quest.IsExpired ? Settings.QuestTypes[quest.Name] : ""),
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
                        return await GetTeamFromPrivateAPIV2Async(rating, mana, rules, splinters, cards, quest, chestTier, username, gameIdHash, apikey, true);
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
                    return await GetTeamFromAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, gameIdHash, apikey, false, true);

                }
                else if (APIResponse.Contains("Account not allowed"))
                {
                    Log.WriteToLog($"{username}: Private API Error: Account not allowed", Log.LogType.CriticalError);
                    return await GetTeamFromAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, gameIdHash, apikey, false, true);
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
                    return await GetTeamFromPrivateAPIV2Async(rating, mana, rules, splinters, cards, quest, chestTier, username, gameIdHash, apikey, true);
                }
                else if (secondTry)
                {
                    Log.WriteToLog($"{username}: Private API down? Trying public API...", Log.LogType.Warning);
                    return await GetTeamFromAPIAsync(rating, mana, rules, splinters, cards, quest, chestTier, username, gameIdHash, apikey, false, true);
                }
            }
            return null;
        }

        public static void ReportLoss(string enemy, string username)
        {
            _ = Helper.DownloadPageAsync($"{ Settings.PublicAPIUrl }report_loss/{enemy}/{username}");
        }

        public static async void ReportGameResult(string apikey, string username, string result)
        {
            JObject GameResult = new JObject(
                        new JProperty("api_key", apikey),
                        new JProperty("player", username),
                        new JProperty("result", result)
                    );
            using var content = new StringContent(JsonConvert.SerializeObject(GameResult), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await Settings._httpClient.PostAsync(Settings.PublicAPIUrl + "rgs/", content);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                Log.WriteToLog($"{username}: Reported Game Result: ({result})");
            }
        }

        private static async Task<string> PostJSONToApi(object json, string url, string username)
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

        public static async Task CheckRateLimitLoopAsync(string username, string apiUrl)
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