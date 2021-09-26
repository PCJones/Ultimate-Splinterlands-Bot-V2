using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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

        public static async Task<JToken> GetTeamFromAPI(int mana, string rules, string[] splinters, string[] cards, JToken quest)
        {
            var matchDetails = new JObject(
                new JProperty("mana", mana),
                new JProperty("rules", rules),
                new JProperty("splinters", splinters),
                new JProperty("myCards", cards),
                new JProperty("quest", Settings.PrioritizeQuest && quest != null 
                &&((int)quest["total"] != (int)quest["completed"]) ? quest : "")
            );

            var test = JsonConvert.SerializeObject(matchDetails);
            return matchDetails;
        }

        public static async Task<JToken> GetPlayerQuestAsync(string username)
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
                JToken userHistory = JToken.Parse(data);
                return userHistory;

            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{username}: Could not get quest from splinterlands api: {ex}", Log.LogType.Error);
            }
            return null;
        }

        public static async Task<string[]> GetPlayerCards(string username)
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
                string[] cards = JToken.Parse(data)["cards"].Where(x =>
                (x["delegated_to"].Type == JTokenType.Null || (string)x["delegated_to"] == username) &&
                x["market_listing_type"].Type == JTokenType.Null && 
                    !((string)x["last_used_player"] != username && 
                        (
                            x["last_used_date"].Type != JTokenType.Null && 
                            DateTime.Parse(JsonConvert.SerializeObject(x["last_used_date"]).Replace("\"", "").Trim()) > oneDayAgo
                        )
                    )
                )
                .Select(x => (string)x["card_detail_id"]).ToArray();
                var combinedCards = new string[cards.Length + Settings.PhantomCards.Length];
                cards.CopyTo(combinedCards, 0);
                Settings.PhantomCards.CopyTo(combinedCards, cards.Length);
                return combinedCards;

            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{username}: Could not get cards from splinterlands api: {ex}{Environment.NewLine}Bot will play with phantom cards only.", Log.LogType.Error);
            }
            return Settings.PhantomCards;
        }

        static HttpClient _client = new HttpClient();
        static async Task<string> DownloadPageAsync(string url)
        {
            // Use static HttpClient to avoid exhausting system resources for network connections.
            var result = await _client.GetAsync(url);
            var response = await result.Content.ReadAsStringAsync();
            // Write status code.
            return response;
        }
    }
}
