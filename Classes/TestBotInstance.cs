using HiveAPI.CS;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using Pastel;
using System;
using System.Collections.Generic;
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
    public class TestBotInstance
    {
        private string Username { get; set; }
        private string Email { get; init; }
        private string PostingKey { get; init; }
        private string ActiveKey { get; init; } // only needed for plugins, not used by normal bot
        private int APICounter { get; set; }
        private int PowerCached { get; set; }
        private int LeagueCached { get; set; }
        private int RatingCached { get; set; }
        private double ECRCached { get; set; }
        private JToken QuestCached { get; set; }
        private Card[] CardsCached { get; set; }
        public bool CurrentlyActive { get; private set; }

        private object _activeLock;
        private DateTime SleepUntil;
        private DateTime LastCacheUpdate;
        private bool UnknownUsername;
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
                return HttpWebRequest.WebRequestPost(Settings.CookieContainer, postData, "https://api2.splinterlands.com/battle/battle_tx", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:93.0) Gecko/20100101 Firefox/93.0", "", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at finding match: " + ex.ToString(), Log.LogType.Error);
            }
            return null;
        }

        private async Task RevealTeam(string trxId, JToken matchDetails, Card[] cards, JToken quest)
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

                var team = await API.GetTeamFromAPIAsync(mana, rulesets, allowedSplinters.ToArray(), cards, quest, Username);

                string summoner = cards.Where(x => x.card_detail_id == (string)team["summoner_id"]).First().card_long_id;
                string monsters = "";
                for (int i = 0; i < 6; i++)
                {
                    var monster = cards.Where(x => x.card_detail_id == (string)team[$"monster_{i + 1}_id"]).FirstOrDefault();
                    if (monster.card_detail_id.Length == 0)
                    {
                        break;
                    }

                    monsters += "\"" + monster.card_long_id + "\",";
                }
                monsters = monsters[..^1];

                string secret = Helper.GenerateRandomString(10);
                string n = Helper.GenerateRandomString(10);

                string json = "{\"trx_id\":\"" + trxId + "\",\"summoner\":\"" + summoner + "\",\"monsters\":[" + monsters + "],\"secret\":\"" + secret + "\",\"app\":\"" + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";

                COperations.custom_json custom_Json = CreateCustomJson(false, true, "sm_team_reveal", json);

                Log.WriteToLog($"{Username}: Submitting team...");
                CtransactionData oTransaction = Settings.oHived.CreateTransaction(new object[] { custom_Json }, new string[] { PostingKey });
                var postData = GetStringForSplinterlandsAPI(oTransaction);
                var result = HttpWebRequest.WebRequestPost(Settings.CookieContainer, postData, "https://api2.splinterlands.com/battle/battle_tx", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:93.0) Gecko/20100101 Firefox/93.0", "", Encoding.UTF8);
                await Task.Delay(5000);
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
                    Log.WriteToLog($"{Username}: Error at waiting for match details: " + ex.ToString(), Log.LogType.Error);
                }
            }
            return null;
        }
        public TestBotInstance(string username, string password, int index, string key = "")
        {
            if (username.Contains("@"))
            {
                UnknownUsername = true;
                Email = username;
            }
            else
            {
                UnknownUsername = false;
                Username = username;
                Email = "";
            }

            PostingKey = password;
            ActiveKey = key;
            SleepUntil = DateTime.Now.AddMinutes((Settings.SleepBetweenBattles + 1) * -1);
            LastCacheUpdate = DateTime.MinValue;
            LogSummary = new LogSummary(index, username);
            _activeLock = new object();
            APICounter = 100;
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
                LogSummary.Reset();
                if (SleepUntil > DateTime.Now)
                {
                    Log.WriteToLog($"{Username}: is sleeping until {SleepUntil.ToString().Pastel(Color.Red)}");
                    return SleepUntil;
                }

                APICounter++;
                if (APICounter >= 12 || (DateTime.Now - LastCacheUpdate).TotalMinutes >= 40)
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
                    ECRCached = await GetECRFromAPI();
                } else if (APICounter % 3 == 0) {
                    if ((int)QuestCached["completed"] != 5)
                    {
                        QuestCached = await API.GetPlayerQuestAsync(Username);
                    }
                    ECRCached = await GetECRFromAPI();
                }

                Log.WriteToLog($"{Username}: Deck size: {(CardsCached.Length - 1).ToString().Pastel(Color.Red)} (duplicates filtered)"); // Minus 1 because phantom card array has an empty string in it
                Log.WriteToLog($"{Username}: Quest details: {JsonConvert.SerializeObject(QuestCached).Pastel(Color.Yellow)}");

                AdvanceLeague();

                if (Settings.BadQuests.Contains((string)QuestCached["splinter"]))
                {
                    RequestNewQuestViaAPI(QuestCached);
                }
                else
                {
                    ClaimQuestReward();
                }

                Log.WriteToLog($"{Username}: Current Energy Capture Rate is { (ECRCached >= 50 ? ECRCached.ToString("N3").Pastel(Color.Green) : ECRCached.ToString("N3").Pastel(Color.Red)) }%");
                if (ECRCached < Settings.ECRThreshold)
                {
                    Log.WriteToLog($"{Username}: ERC is below threshold of {Settings.ECRThreshold}% - skipping this account.", Log.LogType.Warning);
                    SleepUntil = DateTime.Now.AddMinutes(Settings.SleepBetweenBattles / 2);
                    return SleepUntil;
                }

                string trxId = StartNewMatch();
                if (!trxId.Contains("success"))
                {
                    Log.WriteToLog($"{Username}: Creating match was not successful: " + trxId, Log.LogType.Warning);
                    SleepUntil = DateTime.Now.AddMinutes(Settings.SleepBetweenBattles / 2);
                    return SleepUntil;
                }
                Log.WriteToLog($"{Username}: Splinterlands Response: {trxId}");
                trxId = Helper.DoQuickRegex("id\":\"(.*?)\"", trxId);

                SleepUntil = DateTime.Now.AddMinutes(Settings.SleepBetweenBattles >= 3 ? Settings.SleepBetweenBattles : 3);

                JToken matchDetails = await WaitForMatchDetails(trxId);
                if (matchDetails == null)
                {
                    SleepUntil = DateTime.Now.AddMinutes(Settings.SleepBetweenBattles / 2);
                    return SleepUntil;
                }
                await RevealTeam(trxId, matchDetails, CardsCached, QuestCached);

                // todo: determine winner, show summary etc
                Log.WriteToLog($"{Username}: Finished battle!");
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

        private void ClaimQuestReward()
        {
            try
            {
                string logText;
                if ((int)QuestCached["total_items"] == (int)QuestCached["completed_items"]
                    && QuestCached["rewards"].Type == JTokenType.Null)
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
                        string json = "{\"type\":\"quest\",\"quest_id\":\"" + (string)QuestCached["id"] +"\",\"app\":\"" + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";

                        COperations.custom_json custom_Json = CreateCustomJson(false, true, "sm_claim_reward", json);

                        Log.WriteToLog($"{Username}: { "Claimed quest reward".Pastel(Color.Green) }");
                        CtransactionData oTransaction = Settings.oHived.CreateTransaction(new object[] { custom_Json }, new string[] { PostingKey });
                        var postData = GetStringForSplinterlandsAPI(oTransaction);
                        HttpWebRequest.WebRequestPost(Settings.CookieContainer, postData, "https://bcast.splinterlands.com/send", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:93.0) Gecko/20100101 Firefox/93.0", "", Encoding.UTF8);
                        APICounter = 100; // set api counter to 100 to reload quest
                    }
                }
                else
                {
                    Log.WriteToLog($"{Username}: No quest reward to be claimed");
                    // short logText:
                    logText = "No quest reward...";
                }

                LogSummary.QuestStatus = $" { (string)QuestCached["completed_items"] }/{ (string)QuestCached["total_items"] } {logText}";
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at claiming quest rewards: {ex}", Log.LogType.Error);
            }
        }

        private async Task<double> GetECRFromAPI()
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

                    // api call
                }

            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at advancing league: {ex}");
            }
        }

        private void SelectTeam(IWebDriver driver, JToken team)
        {
            try
            {
                Log.WriteToLog($"{Username}: Selecting team...");
                string color = (string)team["color"];
                string summonerID = (string)team["summoner_id"];
                string monster1 = (string)team["monster_1_id"];
                string monster2 = (string)team["monster_2_id"];
                string monster3 = (string)team["monster_3_id"];
                string monster4 = (string)team["monster_4_id"];
                string monster5 = (string)team["monster_5_id"];
                string monster6 = (string)team["monster_6_id"];
                driver.WaitForWebsiteLoadedAndElementShown(By.XPath($"//div[@card_detail_id={summonerID}]"));
                Thread.Sleep(300);
                IWebElement element = driver.FindElement(By.XPath($"//div[@card_detail_id={summonerID}]"));
                //driver.ExecuteJavaScript("arguments[0].click();", element);
                driver.ClickElementOnPage(By.XPath($"//div[@card_detail_id={summonerID}]"));
                //driver.ActionClick(By.XPath($"//div[@card_detail_id={summonerID}]"));
                Thread.Sleep(1000);
                if (GetSummonerColor(summonerID) == "Gold")
                {
                    string colorToPlay = "fire";
                    if (monster1 != "")
                    {
                        string mobColor = GetSummonerColor(monster1);
                        if (mobColor != "Gray" && mobColor != "Gold") // not neutral or dragon
                        {
                            colorToPlay = mobColor;
                        }
                    }
                    if (colorToPlay == "" && monster2 != "")
                    {
                        string mobColor = GetSummonerColor(monster2);
                        if (mobColor != "Gray" && mobColor != "Gold") // not neutral or dragon
                        {
                            colorToPlay = mobColor;
                        }
                    }
                    if (colorToPlay == "" && monster3 != "")
                    {
                        string mobColor = GetSummonerColor(monster3);
                        if (mobColor != "Gray" && mobColor != "Gold") // not neutral or dragon
                        {
                            colorToPlay = mobColor;
                        }
                    }
                    if (colorToPlay == "" && monster4 != "")
                    {
                        string mobColor = GetSummonerColor(monster4);
                        if (mobColor != "Gray" && mobColor != "Gold") // not neutral or dragon
                        {
                            colorToPlay = mobColor;
                        }
                    }
                    if (colorToPlay == "" && monster5 != "")
                    {
                        string mobColor = GetSummonerColor(monster5);
                        if (mobColor != "Gray" && mobColor != "Gold") // not neutral or dragon
                        {
                            colorToPlay = mobColor;
                        }
                    }
                    if (colorToPlay == "" && monster6 != "")
                    {
                        string mobColor = GetSummonerColor(monster6);
                        if (mobColor != "Gray" && mobColor != "Gold") // not neutral or dragon
                        {
                            colorToPlay = mobColor;
                        }
                    }
                    colorToPlay = colorToPlay.Replace("Red", "fire").Replace("Blue", "water")
                        .Replace("White", "life").Replace("Black", "death").Replace("Green", "earth");
                    // check
                    Log.WriteToLog($"{Username}: DRAGON - Teamcolor: {colorToPlay}");
                    driver.WaitForWebsiteLoadedAndElementShown(By.CssSelector($"label[for='filter-element-{colorToPlay}-button']"));
                    driver.ActionClick(By.CssSelector($"label[for='filter-element-{colorToPlay}-button']"));
                    Thread.Sleep(500);
                }
                driver.WaitForWebsiteLoadedAndElementShown(By.XPath($"//div[@card_detail_id={monster1}]"));
                Thread.Sleep(1000);
                element = driver.FindElement(By.XPath($"//div[@card_detail_id={monster1}]"));
                driver.ExecuteJavaScript("arguments[0].scrollIntoView(false);", element);
                //element.Click();
                Thread.Sleep(1000);
                driver.ClickElementOnPage(By.XPath($"//div[@card_detail_id={monster1}]"));
                //driver.ActionClick(By.XPath($"//div[@card_detail_id={monster1}]"));

                if (monster2 != "")
                {
                    //Thread.Sleep(1000);
                    element = driver.FindElement(By.XPath($"//div[@card_detail_id={monster2}]"));
                    //element.Click();
                    driver.ExecuteJavaScript("arguments[0].scrollIntoView(false);", element);
                    //driver.ActionClick(By.XPath($"//div[@card_detail_id={monster2}]"));
                    Thread.Sleep(750);
                    driver.ClickElementOnPage(By.XPath($"//div[@card_detail_id={monster2}]"));
                    //driver.ActionClick(By.XPath($"//div[@card_detail_id={monster2}]"));
                }

                if (monster3 != "")
                {
                    //Thread.Sleep(1000);
                    element = driver.FindElement(By.XPath($"//div[@card_detail_id={monster3}]"));
                    driver.ExecuteJavaScript("arguments[0].scrollIntoView(false);", element);
                    //element.Click();
                    Thread.Sleep(750);
                    driver.ClickElementOnPage(By.XPath($"//div[@card_detail_id={monster3}]"));
                    //driver.ActionClick(By.XPath($"//div[@card_detail_id={monster3}]"));
                }

                if (monster4 != "")
                {
                    //Thread.Sleep(1000);
                    element = driver.FindElement(By.XPath($"//div[@card_detail_id={monster4}]"));
                    //element.Click();
                    driver.ExecuteJavaScript("arguments[0].scrollIntoView(false);", element);
                    //driver.ActionClick(By.XPath($"//div[@card_detail_id={monster2}]"));
                    Thread.Sleep(750);
                    driver.ClickElementOnPage(By.XPath($"//div[@card_detail_id={monster4}]"));
                    //driver.ActionClick(By.XPath($"//div[@card_detail_id={monster4}]"));
                }

                if (monster5 != "")
                {
                    //Thread.Sleep(1000);
                    element = driver.FindElement(By.XPath($"//div[@card_detail_id={monster5}]"));
                    //element.Click();
                    driver.ExecuteJavaScript("arguments[0].scrollIntoView(false);", element);
                    //driver.ActionClick(By.XPath($"//div[@card_detail_id={monster2}]"));
                    Thread.Sleep(750);
                    driver.ClickElementOnPage(By.XPath($"//div[@card_detail_id={monster5}]"));
                    //driver.ActionClick(By.XPath($"//div[@card_detail_id={monster5}]"));
                }

                if (monster6 != "")
                {
                    //Thread.Sleep(1000);
                    element = driver.FindElement(By.XPath($"//div[@card_detail_id={monster6}]"));
                    //element.Click();
                    driver.ExecuteJavaScript("arguments[0].scrollIntoView(false);", element);
                    //driver.ActionClick(By.XPath($"//div[@card_detail_id={monster2}]"));
                    Thread.Sleep(750);
                    driver.ClickElementOnPage(By.XPath($"//div[@card_detail_id={monster6}]"));
                    //driver.ActionClick(By.XPath($"//div[@card_detail_id={monster6}]"));
                }

                Thread.Sleep(1000);
                driver.ClickElementOnPage(By.CssSelector("button[class='btn-green']"));
                Thread.Sleep(1000);
                driver.ClickElementOnPage(By.CssSelector("button[class='btn-green']"), suppressErrors: true);
                //driver.ActionClick(By.CssSelector("button[class='btn-green']"));
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at team selection: {ex}");
            }
        }

        private static string GetSummonerColor(string id)
        {
            return (string)Settings.CardsDetails[Convert.ToInt32(id) - 1]["color"];
        }

        private int GetMana(IWebDriver driver)
        {
            driver.WaitForWebsiteLoadedAndElementShown(By.CssSelector("div.col-md-12 > div.mana-cap__icon"));
            Thread.Sleep(100);
            int mana = Convert.ToInt32(driver.FindElement(By.CssSelector("div.col-md-12 > div.mana-cap__icon"))
                    .GetAttribute("data-original-title").Split(':')[1].Trim());
            return mana;
        }

        private string GetRulesets(IWebDriver driver)
        {
            string rulesets = String.Join("|", driver.FindElements(By.CssSelector("div.combat__rules > div.row > div>  img"))
                    .Select(x => x.GetAttribute("data-original-title").Split(':')[0].Trim()));
            return rulesets;
        }
        private string[] GetAllowedSplinters(IWebDriver driver)
        {
            string[] splinters = driver.FindElements(By.CssSelector("div.col-sm-4 > img"))
                .Where(x => x.GetAttribute("data-original-title").Split(':')[1].Trim() == "Active")
                .Select(x => x.GetAttribute("data-original-title").Split(':')[0].Trim().ToLower()).ToArray();
            return splinters;
        }
  
        private void RequestNewQuestViaAPI(JToken quest)
        {
            try
            {
                if ((int)quest["completed"] > 0)
                {
                    return;
                }
                string n = Helper.GenerateRandomString(10);
                //string json = "{\\\"match_type\\\":\\\"Challenge\\\",\\\"opponent\\\":\\\"pcjones\\\",\\\"settings\\\":{\\\"rating_level\\\":4,\\\"allowed_cards\\\":\\\"all\\\"},\\\"app\\\":\\\"" + Settings.SPLINTERLANDS_APP + "\\\",\\\"n\\\":\\\"" + n + "\\\"}";
                string json = "{\"type\":\"daily\",\"app\":\"" + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";

                COperations.custom_json custom_Json = CreateCustomJson(false, true, "sm_refresh_quest", json);

                Log.WriteToLog($"{Username}: Requesting new quest!");
                Settings.oHived.broadcast_transaction(new object[] { custom_Json }, new string[] { PostingKey });
                APICounter = 100; // set api counter to 100 to reload quest
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
