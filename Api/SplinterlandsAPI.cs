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
using Ultimate_Splinterlands_Bot_V2.Model;
using Ultimate_Splinterlands_Bot_V2.Utils;

namespace Ultimate_Splinterlands_Bot_V2.Api
{
    public static class SplinterlandsAPI
    {
        public static async Task<string> GetSettings()
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
                return data;
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"Could not get settings from splinterlands API: {ex}", Log.LogType.Error);
            }
            return "";
        }

        public static async Task<bool> CheckForMaintenance()
        {
            try
            {
                var data = await GetSettings();
                return (bool)JToken.Parse(data)["maintenance_mode"];
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"Could not get settings from splinterlands API: {ex}", Log.LogType.Error);
            }
            return true;
        }
        public static async Task<(int power, int wildRating, int wildLeague, int modernRating, int modernLeague)> GetPlayerDetailsAsync(string username)
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
                return ((int)JToken.Parse(data)["collection_power"], (int)JToken.Parse(data)["rating"], (int)JToken.Parse(data)["league"],
                    (int)JToken.Parse(data)["modern_rating"], (int)JToken.Parse(data)["modern_league"]);
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{username}: Could not get player details from splinterlands API: {ex}", Log.LogType.Error);
            }
            return (-1, -1, -1, -1, -1);
        }

        public static async Task<(bool enemyHasPicked, bool surrender)> CheckEnemyHasPickedAsync(string username, string tx)
        {
            string data = null;
            try
            {
                data = await Helper.DownloadPageAsync($"{Settings.SPLINTERLANDS_API_URL}/players/outstanding_match?username={ username }");
                if (data == null || data.Contains("502 Bad Gateway") || data.Contains("Cannot GET"))
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
                Log.WriteToLog($"{username}: Could not get ongoing game from splinterlands API: {ex}" + Environment.NewLine + $"response: {data}", Log.LogType.Error);
            }
            return (true, true);
        }

        // this method is only being used by legacy mode
        public static async Task<(int newRating, int ratingChange, decimal spsReward, int result)> GetBattleResultAsync(string username, string tx)
        {
            try
            {
                string data = await Helper.DownloadPageAsync($"{Settings.SPLINTERLANDS_API_URL}/battle/history2?player={ username }&format={ Settings.RankedFormat.ToLower() }");
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
                decimal spsReward = (decimal)matchHistory["battles"][0]["reward_sps"];

                return (newRating, ratingChange, spsReward, gameResult);
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
        public static async Task<Quest> GetPlayerQuestAsync(string username)
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

                return JsonConvert.DeserializeObject<Quest[]>(data)[0];

            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{username}: Could not get quest from splinterlands API: {ex}", Log.LogType.Error);
            }
            return null;
        }

        public static async Task<UserCard[]> GetPlayerCardsAsync(string username, string accessToken)
        {
            try
            {
                string data = await Helper.DownloadPageAsync($"{Settings.SPLINTERLANDS_API_URL}/cards/collection/{ username }?token={ accessToken }&username={ username }");
                if (data == null || data.Trim().Length < 10 || data.Contains("502 Bad Gateway") || data.Contains("Cannot GET"))
                {
                    // Fallback API
                    await Task.Delay(5000);
                    Log.WriteToLog($"{username}: Error with splinterlands API for cards, trying fallback api...", Log.LogType.Warning);
                    data = await Helper.DownloadPageAsync($"{Settings.SPLINTERLANDS_API_URL_FALLBACK}/cards/collection/{ username }?token={ accessToken }&username={ username }");
                }

                DateTime oneDayAgo = DateTime.Now.AddDays(-1);

                List<UserCard> cards = new(JToken.Parse(data)["cards"].Where(card =>
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
                    bool listedOnMarket = (string)card["market_listing_type"] == "RENT" && (string)card["player"] != username ? false : card["market_listing_type"].Type
                        != JTokenType.Null ? true : false;
                    
                    return currentUser == username && !cardOnCooldown && !listedOnMarket;
                })
                .Select(x => new UserCard((string)x["card_detail_id"], (string)x["uid"], (string)x["level"], (bool)x["gold"], false))
                .Distinct().ToArray());

                // add starter cards
                cards.AddRange(Settings.StarterCards);

                cards.Sort();
                cards.Reverse();

                // only use highest level/gold cards
                UserCard[] cardsFiltered = Settings.CardSettings.FilterByCardSettings(cards)
                    .Select(x => cards.Where(y => x.card_detail_id == y.card_detail_id)
                    .First()).Distinct().ToArray();

                return cardsFiltered;
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{username}: Could not get cards from splinterlands API: {ex}{Environment.NewLine}Bot will play with phantom cards only.", Log.LogType.Error);
            }

            return Settings.StarterCards;
        }
    }
}
