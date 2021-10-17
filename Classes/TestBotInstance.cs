﻿using HiveAPI.CS;
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

namespace Ultimate_Splinterlands_Bot_V2.Classes
{
    public class TestBotInstance
    {
        // todo: check which parameters need to be public / can be private
        public string Username { get; set; }
        public string Email { get; init; }
        public string PostingKey { get; init; }
        public string ActiveKey { get; init; } // only needed for plugins, not used by normal bot
        public bool CurrentlyActive { get; private set; }

        private object _activeLock;
        private DateTime SleepUntil;
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
        private string StartNewMatch()
        {
            string n = Helper.GenerateRandomString(10);
            string json = "{\"match_type\":\"Ranked\",\"app\":\"" + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";
            
            COperations.custom_json custom_Json = CreateCustomJson(false, true, "sm_find_match", json);

            try
            {
                Log.WriteToLog($"{Username} Finding match...");
                return Settings.oHived.broadcast_transaction(new object[] { custom_Json }, new string[] { PostingKey });
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username} Error at finding match: " + ex.ToString(), Log.LogType.Error);
            }
            return null;
        }

        private async Task RevealTeam(string trxId, JToken matchDetails, Card[] cards, JToken quest)
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
                if (monster == null)
                {
                    break;
                }

                monsters += "\"" + monster.card_long_id + "\",";
            }
            monsters = monsters[..^1];

            string secret = Helper.GenerateRandomString(10);
            string n = Helper.GenerateRandomString(10);


            string json = "{\"trx_id\":\"" + trxId + "\",\"summoner\":\"" + summoner + "\",\"monsters\":[" + monsters + "],\"secret\":\"" + secret +"\",\"app\":\"" + Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";

            COperations.custom_json custom_Json = CreateCustomJson(false, true, "sm_team_reveal", json);

            try
            {
                Log.WriteToLog($"{Username} Submitting team...");
                string txid = Settings.oHived.broadcast_transaction(new object[] { custom_Json }, new string[] { PostingKey });
                await Task.Delay(15000);
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username} Error at submitting team: " + ex.ToString(), Log.LogType.Error);
            }
        }

        private async Task<JToken> WaitForMatchDetails(string trxId)
        {
            for (int i = 0; i < 10; i++) // 10 * 20 = 200seconds, so 3mins + 20sec 
            {
                try
                {
                    await Task.Delay(15000);
                    JToken matchDetails = JToken.Parse(await Helper.DownloadPageAsync("https://api.splinterlands.io/battle/status?id=" + trxId));
                    if (matchDetails["mana_cap"].Type != JTokenType.Null)
                    {
                        return matchDetails;
                    }
                    await Task.Delay(5000);
                }
                catch (Exception ex)
                {
                    Log.WriteToLog($"{Username} Error at waiting for match details: " + ex.ToString(), Log.LogType.Error);
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
            LogSummary = new LogSummary(index, username);
            _activeLock = new object();
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
                double ecr = await GetECRFromAPI();
                LogSummary.ECR = $"{ecr} %";
                // todo: add log with different colors in same line
                Log.WriteToLog($"{Username}: Current Energy Capture Rate is { (ecr >= 50 ? ecr.ToString("N3").Pastel(Color.Green) : ecr.ToString("N3").Pastel(Color.Red)) }%");
                if (ecr < Settings.ECRThreshold)
                {
                    Log.WriteToLog($"{Username}: ERC is below threshold of {Settings.ECRThreshold}% - skipping this account.", Log.LogType.Warning);
                    SleepUntil = DateTime.Now.AddMinutes(Settings.SleepBetweenBattles / 2);
                    return SleepUntil;
                }
                string trxId = StartNewMatch();
                SleepUntil = DateTime.Now.AddMinutes(Settings.SleepBetweenBattles >= 3? Settings.SleepBetweenBattles : 3);
                JToken quest = await API.GetPlayerQuestAsync(Username);
                if (Settings.BadQuests.Contains((string)quest["splinter"]))
                {
                    RequestNewQuestViaAPI(quest);
                }
                Card[] cards = await API.GetPlayerCardsAsync(Username);
                Log.WriteToLog($"{Username}: Deck size: {(cards.Length - 1).ToString().Pastel(Color.Red)} (duplicates filtered)"); // Minus 1 because phantom card array has an empty string in it
                if (trxId == null)
                {
                    SleepUntil = DateTime.Now.AddMinutes(Settings.SleepBetweenBattles / 2);
                    return SleepUntil;
                }
                JToken matchDetails = await WaitForMatchDetails(trxId);
                if (matchDetails == null)
                {
                    SleepUntil = DateTime.Now.AddMinutes(Settings.SleepBetweenBattles / 2);
                    return SleepUntil;
                }
                await RevealTeam(trxId, matchDetails, cards, quest);

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
        
        private async Task<double> GetECRFromAPI()
        {
            var balanceInfo = ((JArray)await API.GetPlayerBalancesAsync(Username)).Where(x => (string)x["token"] == "ECR").First();
            var captureRate = (int)balanceInfo["balance"];
            DateTime lastRewardTime = (DateTime)balanceInfo["last_reward_time"];
            double ecrRegen = 0.0868;
            double ecr = captureRate + (new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds() - new DateTimeOffset(lastRewardTime).ToUnixTimeMilliseconds()) / 3000 * ecrRegen;
            return Math.Min(ecr, 10000) / 100;
        }

        private void AdvanceLeague(IWebDriver driver, string currentRating)
        {
            try
            {
                if (!Settings.AdvanceLeague || currentRating == "unknown" || Convert.ToInt32(currentRating.Replace(",", "").Replace(".", "")) < 1000)
                {
                    return;
                }
                if (driver.FindElement(By.ClassName("bh_advance_btn")).Displayed)
                {
                    Log.WriteToLog($"{Username}: { "Advancing to higher league!".Pastel(Color.Green)}");
                    driver.ExecuteJavaScript("SM.AdvanceLeaderboard(true);");
                    Thread.Sleep(3000);
                    driver.SwitchTo().Alert().Accept();
                    Thread.Sleep(5000);
                    WaitForLoadingBanner(driver);
                    Thread.Sleep(5000);
                    ClosePopups(driver);
                }

            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at advancing league: {ex}");
            }
        }
        private void GetBattleResult(IWebDriver driver, string oldRating)
        {
            //driver.WaitForWebsiteLoadedAndElementShown(By.CssSelector("section.player.winner .bio__name__display"));
            driver.WaitForWebsiteLoadedAndElementShown(By.XPath("//h1[contains(., 'BATTLE RESULT')]"));
            string logTextBattleResult;
            string rating;
            string ratingChange;
            if (driver.FindElements(By.CssSelector("section.player.winner .bio__name__display")).Count > 0)
            {
                string winner = driver.FindElement(By.CssSelector("section.player.winner .bio__name__display")).Text.ToLower();
                if (winner == Username)
                {
                    rating = driver.FindElement(By.CssSelector("section.player.winner .rating-total")).Text;
                    ratingChange = driver.FindElement(By.CssSelector("section.player.winner .rating-delta")).Text;
                    string decWon = driver.FindElement(By.CssSelector(".player.winner span.dec-reward span")).Text;
                    logTextBattleResult = $"You won! Reward: {decWon} DEC";
                    Log.WriteToLog($"{Username}: { logTextBattleResult.Pastel(Color.Green) }");
                    Log.WriteToLog($"{Username}: New rating is {rating} ({ ratingChange.Pastel(Color.Green) })");
                }
                else
                {
                    rating = driver.FindElement(By.CssSelector("section.player.loser .rating-total")).Text.ToLower();
                    ratingChange = driver.FindElement(By.CssSelector("section.player.loser .rating-delta")).Text;
                    logTextBattleResult = $"You lost :(";
                    Log.WriteToLog($"{Username}: { logTextBattleResult.Pastel(Color.Red) }");
                    Log.WriteToLog($"{Username}: New rating is {rating} ({ ratingChange.Pastel(Color.Red) })");
                    API.ReportLoss(winner, Username);
                }
            }
            else
            {
                rating = oldRating;
                ratingChange = "+- 0";
                logTextBattleResult = "DRAW";
                Log.WriteToLog($"{Username}: { logTextBattleResult}");
                Log.WriteToLog($"{Username}: Rating has not changed ({ rating })");
            }

            LogSummary.Rating = $"{ rating } ({ ratingChange })";
            LogSummary.BattleResult = logTextBattleResult;

            Thread.Sleep(4500);
            ClosePopups(driver);
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
        private void StartBattle(IWebDriver driver)
        {
            Log.WriteToLog($"{Username}: Waiting for battle button...");
            if (!driver.WaitForWebsiteLoadedAndElementShown(By.XPath("//button[contains(., 'BATTLE')]")))
            {
                Log.WriteToLog($"{Username}: Battle button not visible, reloading page...", Log.LogType.Warning);
                driver.Navigate().GoToUrl("https://splinterlands.com/?p=battle_history");
                Thread.Sleep(5000);
                ClosePopups(driver);
            }
            driver.ClickElementOnPage(By.XPath("//button[contains(., 'BATTLE')]"));
            Log.WriteToLog($"{Username}: Battle button clicked!");
        }
        private void NavigateToBattlePage(IWebDriver driver)
        {
            if (!driver.Url.Contains("battle_history"))
            {
                try
                {
                    driver.FindElement(By.Id("menu_item_battle")).Click();
                }
                catch (Exception)
                {
                    ClosePopups(driver);
                    driver.ClickElementOnPage(By.Id("menu_item_battle"));
                }
                driver.WaitForWebsiteLoadedAndElementShown(By.Id("battle_category_btn"));
            }
        }

        private void ClaimSeasonRewards(IWebDriver driver)
        {
            try
            {
                ClosePopups(driver);
                Thread.Sleep(400);
                if (driver.WaitForElementShown(By.Id("claim-btn"), 1))
                {
                    Log.WriteToLog($"{Username}: Claiming season rewards");
                    Thread.Sleep(1000);
                    driver.ClickElementOnPage(By.Id("claim-btn"));
                    Thread.Sleep(5000);
                    WaitForLoadingBanner(driver);
                    Thread.Sleep(3000);
                    driver.Navigate().Refresh();
                    Thread.Sleep(5000);
                    WaitForLoadingBanner(driver);
                    Thread.Sleep(10000);
                    ClosePopups(driver);
                    Log.WriteToLog($"{Username}: Claimed season rewards", Log.LogType.Success);
                }
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at claiming season rewards: {ex}", Log.LogType.Error);
            }
        }

        private void ClaimQuestReward(IWebDriver driver, JToken quest, string currentRating)
        {
            try
            {
                string logText;
                if (driver.WaitForWebsiteLoadedAndElementShown(By.Id("quest_claim_btn"), 1))
                {
                    logText = "Quest reward can be claimed";
                    Log.WriteToLog($"{Username}: {logText.Pastel(Color.Green)}");
                    // short logText:
                    logText = "Quest reward available!";
                    if (!Settings.ClaimQuestReward)
                    {
                        return;
                    }
                    if (Settings.DontClaimQuestNearHigherLeague)
                    {
                        if (currentRating == "unknown")
                        {
                            return;
                        }
                        // todo: check if league can be reached
                        int rating = Convert.ToInt32(currentRating.Replace(".", "").Replace(",", ""));
                        int power = (int)Convert.ToDecimal(driver.FindElement(By.CssSelector("div#power_progress div.progress__info span.number_text")).Text, CultureInfo.InvariantCulture);
                        bool waitForHigherLeague = (rating is >= 300 and < 400) && (power is >= 1000 || (Settings.RentalBotActivated && Settings.DesiredRentalPower >= 1000)) || // bronze 2
                            (rating is >= 550 and < 700) && (power is >= 5000 || (Settings.RentalBotActivated && Settings.DesiredRentalPower >= 5000)) || // bronze 1 
                            (rating is >= 840 and < 1000) && (power is >= 15000 || (Settings.RentalBotActivated && Settings.DesiredRentalPower >= 15000)) || // silver 3
                            (rating is >= 1200 and < 1300) && (power is >= 40000 || (Settings.RentalBotActivated && Settings.DesiredRentalPower >= 40000)) || // silver 2
                            (rating is >= 1500 and < 1600) && (power is >= 70000 || (Settings.RentalBotActivated && Settings.DesiredRentalPower >= 70000)) || // silver 1
                            (rating is >= 1800 and < 1900) && (power is >= 100000 || (Settings.RentalBotActivated && Settings.DesiredRentalPower >= 100000)); // gold 

                        if (waitForHigherLeague)
                        {
                            Log.WriteToLog($"{Username}: Don't claim quest - wait for higher league");
                            return;
                        }
                    }

                    Log.WriteToLog($"{Username}: Claiming quest reward...");
                    driver.ClickElementOnPage(By.Id("quest_claim_btn"));
                    Thread.Sleep(5000);
                    WaitForLoadingBanner(driver);
                    Thread.Sleep(3000);
                    driver.Navigate().Refresh();
                    Thread.Sleep(3000);
                    WaitForLoadingBanner(driver);
                    Thread.Sleep(3000);
                    ClosePopups(driver);
                    Thread.Sleep(1000);
                    Log.WriteToLog($"{Username}: { "Claimed quest reward".Pastel(Color.Green) }");
                }
                else
                {
                    Log.WriteToLog($"{Username}: No quest reward to be claimed");
                    // short logText:
                    logText = "No quest reward...";
                }

                LogSummary.QuestStatus = $" { (string)quest["completed"] }/{ (string)quest["total"] } {logText}";
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at claiming quest rewards: {ex}", Log.LogType.Error);
            }
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

                Log.WriteToLog($"{Username} Requesting new quest!");
                Settings.oHived.broadcast_transaction(new object[] { custom_Json }, new string[] { PostingKey });
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at changing quest: {ex}", Log.LogType.Error);
            }
        }

        private void RequestNewQuest(IWebDriver driver, JToken quest)
        {
            try
            {
                if ((int)quest["completed"] > 3)
                {
                    return;
                }
                if (driver.WaitForWebsiteLoadedAndElementShown(By.Id("quest_new_btn"), 1))
                {
                    Log.WriteToLog($"{ Username }: Quest type is { (string)quest["splinter"] } - requesting new one.");

                    driver.ClickElementOnPage(By.Id("quest_new_btn"));
                    Thread.Sleep(1500);
                    driver.SwitchTo().Alert().Accept();
                    WaitForLoadingBanner(driver);
                    Thread.Sleep(3000);
                    WaitForLoadingBanner(driver);
                    ClosePopups(driver);
                    Thread.Sleep(1000);
                    Log.WriteToLog($"{Username}: {"Renewed quest!".Pastel(Color.Green)}");
                }
                else
                {
                    Log.WriteToLog($"{Username}: { "Can't change quest".Pastel(Color.Red) }");
                }
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at changing quest: {ex}", Log.LogType.Error);
            }
        }

        private void ClosePopups(IWebDriver driver, bool needNameVisible = true)
        {
            try
            {
                if (needNameVisible && !UnknownUsername)
                {
                    driver.WaitForSuccessMessage(Username);
                    driver.ExecuteJavaScript("$('.modal').modal('hide');", suppressErrors: true);
                }
                else
                {
                    Thread.Sleep(1000);
                    driver.ExecuteJavaScript("$('.modal').modal('hide');", suppressErrors: true);
                }
                //if (needNameVisible)
                //{
                //    int counter = 0;
                //    Thread.Sleep(500);
                //    while (!driver.FindElement(By.ClassName("bio__name__display")).Displayed)
                //    {
                //        Thread.Sleep(500);
                //        driver.ExecuteJavaScript("$('.modal').modal('hide');", suppressErrors: true);
                //        if (counter++ > 30)
                //        {
                //            Log.WriteToLog($"{Username}: Error at closing popups loop - name not visible", Log.LogType.CriticalError);
                //            break;
                //        }
                //    }
                //}
                //else
                //{
                //    Thread.Sleep(500);
                //    driver.ExecuteJavaScript("$('.modal').modal('hide');", suppressErrors: true);
                //}
                //driver.ExecuteJavaScript("$('#daily_update_dialog').modal('hide);");
                //driver.ExecuteJavaScript("$('.modal-dialog modal-lg').modal('hide);");
                //if (driver.WaitForWebsiteLoaded(By.ClassName("close"), 3))
                //{
                //    if (driver.WaitForElementShown(By.ClassName("close"), 1))
                //    {
                //        Log.WriteToLog($"{Username}: Closing popup #1");
                //        driver.ExecuteJavaScript("$('.modal-dialog modal-lg').modal('hide);");
                //        try
                //        {
                //            Thread.Sleep(500);
                //            if (driver.WaitForElementShown(By.ClassName("close"), 1))
                //                driver.FindElement(By.ClassName("close")).Click();
                //        }
                //        catch (Exception)
                //        {
                //            // try again if popup wasn't ready yet
                //            Thread.Sleep(2500);
                //            driver.ClickElementOnPage(By.ClassName("close"));
                //        }
                //    }
                //}
                //if (driver.WaitForWebsiteLoaded(By.ClassName("modal-close-new"), 1))
                //{
                //    if (driver.WaitForElementShown(By.ClassName("modal-close-new"), 1))
                //    {
                //        Log.WriteToLog($"{Username}: Closing popup #2");
                //        try
                //        {
                //            Thread.Sleep(300);
                //            driver.FindElement(By.ClassName("modal-close-new")).Click();
                //        }
                //        catch (Exception)
                //        {
                //            // try again if popup wasn't ready yet
                //            Thread.Sleep(2500);
                //            driver.ClickElementOnPage(By.ClassName("modal-close-new"));
                //        }
                //    }
                //}
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at closing popups: {ex}", Log.LogType.Error);
            }
        }

        private double GetECR(IWebDriver driver)
        {
            try
            {
                var ecr = driver.FindElement(By.XPath("//div[@class='dec-options'][1]/div[@class='value'][2]/div")).GetAttribute("innerHTML");
                return Convert.ToDouble(ecr[..^1], CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Couldn't get Energy Capture Rate: {ex}", Log.LogType.Error);
            }
            return 0;
        }

        private string GetCurrentRating(IWebDriver driver)
        {
            try
            {
                var rating = driver.FindElement(By.XPath("//div[@class='league_status_panel_progress_bar_pos']//span[@class='number_text']")).GetAttribute("innerHTML");
                return rating;
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Couldn't get current rating: {ex}", Log.LogType.Warning);
            }
            return "unknown";
        }

        private bool Login(IWebDriver driver, bool logoutNeeded)
        {
            Log.WriteToLog($"{ (UnknownUsername ? Email : Username) }: Trying to login...");
            driver.Navigate().GoToUrl("https://splinterlands.com/");
            WaitForLoadingBanner(driver);
            ClosePopups(driver, false);
            if (!UnknownUsername && !logoutNeeded)
            {
                // check if already logged in
                if (!driver.WaitForWebsiteLoadedAndElementShown(By.Id("log_in_button"), 3))
                {
                    if (driver.FindElements(By.ClassName("bio__name__display")).Count > 0)
                    {
                        string usernameIngame = driver.FindElement(By.ClassName("bio__name__display")).Text.Trim().ToLower();
                        if (usernameIngame == Username)
                        {
                            Log.WriteToLog($"{Username}: {"Already logged in!".Pastel(Color.Yellow)}");
                            return true;
                        }
                    }
                }
                // this shouldn't happen unless session is no longer valid, call logout to be sure
            }
            driver.ExecuteJavaScript("SM.Logout();", true);
            if (!driver.WaitForWebsiteLoadedAndElementShown(By.Id("log_in_button"), 6))
            {
                // splinterlands bug, battle result still open
                if (driver.WaitForWebsiteLoadedAndElementShown(By.XPath("//h1[contains(., 'BATTLE RESULT')]"), 1))
                {
                    driver.ClickElementOnPage(By.CssSelector("button[class='btn btn--done']"));
                }

            }
            driver.WaitForWebsiteLoadedAndElementShown(By.Id("log_in_button"), 8);
            ClosePopups(driver, false);
            Thread.Sleep(1000);
            driver.ExecuteJavaScript("SM.ShowLogin(SM.ShowAbout);");

            driver.WaitForWebsiteLoadedAndElementShown(By.Id("email"));
            driver.SetData(By.Id("email"), Email.Length > 0 ? Email : Username);
            driver.SetData(By.Id("password"), PostingKey);
            // endless circle workaround
            if (driver.FindElements(By.ClassName("loading")).Count > 0)
            {
                Log.WriteToLog($"{ (UnknownUsername ? Email : Username) }: Splinterlands not loading, trying to reload...");
                Login(driver, logoutNeeded);
            }
            driver.ClickElementOnPage(By.Name("loginBtn"), 1);

            if (!driver.WaitForWebsiteLoadedAndElementShown(By.Id("log_in_text"), 60))
            {
                Log.WriteToLog($"{ (UnknownUsername ? Email : Username) }: Could not log in - skipping account.", Log.LogType.Error);
                return false;
            }

            Log.WriteToLog($"{ (UnknownUsername ? Email : Username) }: Login successful", Log.LogType.Success);
            return true;
        }

        private void WaitForLoadingBanner(IWebDriver driver)
        {
            do
            {
                Thread.Sleep(500);
            } while (driver.FindElements(By.ClassName("loading")).Count > 0);
        }
    }
}