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

namespace Ultimate_Splinterlands_Bot_V2.Classes
{
    public static class SplinterlandsAPI
    {
        public static async Task<bool> CheckForMaintenance()
        {
            try
            {
                string data = await Helper.DownloadPageAsync($"{Settings.SPLINTERLANDS_API_URL}/settings");
                if (data == null || data.Trim().Length < 10 || data.Contains("502 Bad Gateway") || data.Contains("Cannot GET"))
                {
                    // Fallback API
                    await Task.Delay(5000);
                    Log.WriteToLog($"Error with splinterlands API for settings, trying fallback api...", Log.LogType.Warning);
                    data = await Helper.DownloadPageAsync($"{Settings.SPLINTERLANDS_API_URL_FALLBACK}/settings");
                }
                return (bool)JToken.Parse(data)["maintenance_mode"];
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"Could not get settings from splinterlands API: {ex}", Log.LogType.Error);
            }
            return true;
        }
        public static async Task<(int power, int rating, int league)> GetPlayerDetailsAsync(string username)
        {
            try
            {
                string data = await Helper.DownloadPageAsync($"{Settings.SPLINTERLANDS_API_URL}/players/details?name={ username }");
                if (data == null || data.Trim().Length < 10 || data.Contains("502 Bad Gateway") || data.Contains("Cannot GET"))
                {
                    // Fallback API
                    Log.WriteToLog($"{username}: Error with splinterlands API for player details, trying fallback api...", Log.LogType.Warning);
                    await Task.Delay(5000);
                    data = await Helper.DownloadPageAsync($"{Settings.SPLINTERLANDS_API_URL_FALLBACK}/players/details?name={ username }");
                }
                return ((int)JToken.Parse(data)["collection_power"], (int)JToken.Parse(data)["rating"], (int)JToken.Parse(data)["league"]);
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{username}: Could not get player details from splinterlands API: {ex}", Log.LogType.Error);
            }
            return (-1, -1, -1);
        }

        public static async Task<(bool enemyHasPicked, bool surrender)> CheckEnemyHasPickedAsync(string username, string tx)
        {
            try
            {
                string data = await Helper.DownloadPageAsync($"{Settings.SPLINTERLANDS_API_URL}/players/outstanding_match?username={ username }");
                if (data == null || data.Trim().Length < 10 || data.Contains("502 Bad Gateway") || data.Contains("Cannot GET"))
                {
                    // Fallback API
                    // wait 10 seconds just in case for this method
                    await Task.Delay(10000);
                    Log.WriteToLog($"{username}: Error with splinterlands API for ongoing game, trying fallback api...", Log.LogType.Warning);
                    data = await Helper.DownloadPageAsync($"{Settings.SPLINTERLANDS_API_URL_FALLBACK}/players/outstanding_match?username={ username }");
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
                Log.WriteToLog($"{username}: Could not get ongoing game from splinterlands API: {ex}", Log.LogType.Error);
            }
            return (true, true);
        }

        // this method is not being used currently
        public static async Task<(int newRating, int ratingChange, decimal decReward, int result)> GetBattleResultAsync(string username, string tx)
        {
            try
            {
                string data = await Helper.DownloadPageAsync($"{Settings.SPLINTERLANDS_API_URL}/battle/history2?player={ username }");
                if (data == null || data.Trim().Length < 10 || data.Contains("502 Bad Gateway") || data.Contains("Cannot GET"))
                {
                    // Fallback API
                    await Task.Delay(5000);
                    Log.WriteToLog($"{username}: Error with splinterlands API for battle result, trying fallback api...", Log.LogType.Warning);
                    data = await Helper.DownloadPageAsync($"{Settings.SPLINTERLANDS_API_URL_FALLBACK}/battle/history2?player={ username }");
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
                Log.WriteToLog($"{username}: Could not get battle results from splinterlands API: {ex}", Log.LogType.Error);
            }
            return (-1, -1, -1, -1);
        }

        public static async Task<JToken> GetPlayerBalancesAsync(string username)
        {
            try
            {
                string data = await Helper.DownloadPageAsync($"{Settings.SPLINTERLANDS_API_URL}/players/balances?username={ username }");
                if (data == null || data.Trim().Length < 10 || data.Contains("502 Bad Gateway") || data.Contains("Cannot GET"))
                {
                    // Fallback API
                    await Task.Delay(5000);
                    Log.WriteToLog($"{username}: Error with splinterlands API for balances, trying fallback api...", Log.LogType.Warning);
                    data = await Helper.DownloadPageAsync($"{Settings.SPLINTERLANDS_API_URL_FALLBACK}/players/balances?username={ username }");
                }
                JToken balances = JToken.Parse(data);
                return balances;

            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{username}: Could not get balances from splinterlands API: {ex}", Log.LogType.Error);
            }
            return null;
        }
        public static async Task<(JToken quest, JToken questLessDetails)> GetPlayerQuestAsync(string username)
        {
            try
            {
                string data = await Helper.DownloadPageAsync($"{Settings.SPLINTERLANDS_API_URL}/players/quests?username={ username }");
                if (data == null || data.Trim().Length < 10 || data.Contains("502 Bad Gateway") || data.Contains("Cannot GET"))
                {
                    // Fallback API
                    await Task.Delay(5000);
                    Log.WriteToLog($"{username}: Error with splinterlands API for quest, trying fallback api...", Log.LogType.Warning);
                    data = await Helper.DownloadPageAsync($"{Settings.SPLINTERLANDS_API_URL_FALLBACK}/players/quests?username={ username }");
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
                Log.WriteToLog($"{username}: Could not get quest from splinterlands API: {ex}", Log.LogType.Error);
            }
            return (null, null);
        }

        public static async Task<Card[]> GetPlayerCardsAsync(string username)
        {
            try
            {
                string data = await Helper.DownloadPageAsync($"{Settings.SPLINTERLANDS_API_URL}/cards/collection/{ username }");
                if (data == null || data.Trim().Length < 10 || data.Contains("502 Bad Gateway") || data.Contains("Cannot GET"))
                {
                    // Fallback API
                    await Task.Delay(5000);
                    Log.WriteToLog($"{username}: Error with splinterlands API for cards, trying fallback api...", Log.LogType.Warning);
                    data = await Helper.DownloadPageAsync($"{Settings.SPLINTERLANDS_API_URL_FALLBACK}/cards/collection/{ username }");
                }

                DateTime oneDayAgo = DateTime.Now.AddDays(-1);

                List<Card> cards = new List<Card>(JToken.Parse(data)["cards"].Where(card =>
                {
                    string currentUser = card["delegated_to"].Type == JTokenType.Null ? (string)card["player"] : (string)card["delegated_to"];
                    bool cardOnCooldown;
                    if (card["last_transferred_date"].Type == JTokenType.Null || card["last_used_date"].Type == JTokenType.Null)
                    {
                        cardOnCooldown = false;
                    }
                    else
                    {
                        cardOnCooldown = DateTime.Parse(JsonConvert.SerializeObject(card["last_transferred_date"]).Replace("\"", "").Trim()) > oneDayAgo 
                            && DateTime.Parse(JsonConvert.SerializeObject(card["last_used_date"]).Replace("\"", "").Trim()) > oneDayAgo && (
                             currentUser != (string)card["last_used_player"]);
                    }
                    bool forSale = (string)card["market_listing_type"] == "RENT" ? false : card["market_listing_type"].Type != JTokenType.Null ? true : false;

                    return currentUser == username && !cardOnCooldown && !forSale;
                })
                .Select(x => new Card((string)x["card_detail_id"], (string)x["uid"], (string)x["level"], (bool)x["gold"]))
                .Distinct().OrderByDescending(x => x.SortValue()).ToArray());

                //// lets hope nobody ever needs to touch this mess again
                //List<Card> cards = new List<Card>(JToken.Parse(data)["cards"].Where(x =>
                //((x["delegated_to"].Type == JTokenType.Null && x["market_listing_type"].Type == JTokenType.Null)
                //|| (string)x["delegated_to"] == username)
                //&&
                //    (string)x["market_listing_type"] == "RENT" &&
                //    (int)x["market_listing_status"] == 3
                //    ||
                //    (
                //        !((string)x["last_used_player"] != username &&
                //            (
                //                x["last_used_date"].Type != JTokenType.Null &&
                //                DateTime.Parse(JsonConvert.SerializeObject(x["last_used_date"]).Replace("\"", "").Trim()) > oneDayAgo
                //            )
                //        )
                //    )
                //)
                //.Select(x => new Card((string)x["card_detail_id"], (string)x["uid"], (string)x["level"], (bool)x["gold"]))
                //.Distinct().OrderByDescending(x => x.SortValue()).ToArray());

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
                Log.WriteToLog($"{username}: Could not get cards from splinterlands API: {ex}{Environment.NewLine}Bot will play with phantom cards only.", Log.LogType.Error);
            }
            return Settings.PhantomCards.Select(x => new Card(x, "starter-" + x + "-" + Helper.GenerateRandomString(5), "1", false)).ToArray();
        }
    }
}
