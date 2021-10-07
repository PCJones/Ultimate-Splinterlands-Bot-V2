﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using Pastel;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ultimate_Splinterlands_Bot_V2.Classes
{
    public record LogSummary
    {
        public int Index { get; init; }
        public string Account { get; set; }
        public string BattleResult { get; set; }
        public string Rating { get; set; }
        public string ECR { get; set; }
        public string QuestStatus { get; set; }

        public LogSummary(int index, string account)
        {
            Index = index;
            Account = account;
            Reset();
        }

        public void Reset()
        {
            BattleResult = "N/A";
            Rating = "N/A";
            ECR = "N/A";
            QuestStatus = "N/A";
        }
    }
    public class BotInstance
    {
        // todo: check which parameters need to be public / can be private
        public string Username { get; set; }
        public string Email { get; init; }
        public string Password { get; init; }
        public bool CurrentlyActive { get; private set; }

        private object _activeLock;
        private DateTime SleepUntil;
        private bool UnknownUsername;
        private LogSummary LogSummary;

        public BotInstance(string username, string password, int index)
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

            Password = password;
            SleepUntil = DateTime.Now.AddMinutes((Settings.SleepBetweenBattles + 1) * - 1);
            LogSummary = new LogSummary(index, username);
            _activeLock = new object();
        }

        public async Task<DateTime> DoBattleAsync(int browserInstance, bool logoutNeeded, int botInstance)
        {
            Log.WriteToLog($"Browser #{ browserInstance}", debugOnly: true);
            IWebDriver driver =  Settings.SeleniumInstances[browserInstance].driver;
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
                    Log.WriteToLog($"{Username}: is sleeping until {SleepUntil}");
                    Linenotify.lineNotify($"USB V2:{Username}: is sleeping until {SleepUntil}");
                    return SleepUntil;
                }
                if (!Login(driver, logoutNeeded))
                {
                    SleepUntil = DateTime.Now.AddMinutes(5);
                    return SleepUntil;
                }
                ClosePopups(driver);
                if (UnknownUsername)
                {
                    Username = driver.FindElement(By.ClassName("bio__name__display")).Text.Trim().ToLower();
                    if (Username.Trim().Length == 0)
                    {
                        Username = "";
                        Log.WriteToLog($"{Email}: { "Error reading username, will try again in 3 minutes".Pastel(Color.Red) }");
                        Linenotify.lineNotify($"USB V2:{Email}: { "Error reading username, will try again in 3 minutes" }");
                        SleepUntil = DateTime.Now.AddMinutes(3);
                        return SleepUntil;
                    }
                    LogSummary.Account = Username;
                    Log.WriteToLog($"{Email}: Username is {Username}");
                    Linenotify.lineNotify($"USB V2:{Email}: Username is {Username}");
                    UnknownUsername = false;
                }

                JToken quest = await API.GetPlayerQuestAsync(Username);
                Card[] cards = await API.GetPlayerCardsAsync(Username);
                Log.WriteToLog($"{Username}: Deck size: {cards.Length - 1} (duplicates filtered)"); // Minus 1 because phantom card array has an empty string in it
                ClosePopups(driver);
                NavigateToBattlePage(driver);
                double ecr = GetECR(driver);
                LogSummary.ECR = $"{ecr} %";
                // todo: add log with different colors in same line
                Log.WriteToLog($"{Username}: Current Energy Capture Rate is { (ecr >= 50 ? ecr.ToString().Pastel(Color.Green) : ecr.ToString().Pastel(Color.Red)) }%");
                Linenotify.lineNotify($"USB V2:{Username}: Current Energy Capture Rate is {(ecr >= 50 ? ecr.ToString() : ecr.ToString())}%");
                if (ecr < Settings.ECRThreshold)
                {
                    Log.WriteToLog($"{Username}: ERC is below threshold of {Settings.ECRThreshold}% - skipping this account.", Log.LogType.Warning);
                    Linenotify.lineNotify($"USB V2:{Username}: ERC is below threshold of {Settings.ECRThreshold}% - skipping this account.");
                    SleepUntil = DateTime.Now.AddMinutes(Settings.SleepBetweenBattles / 2);
                    return SleepUntil;
                }

                if (Settings.ClaimSeasonReward)
                {
                    ClaimSeasonRewards(driver);
                }
                string currentRating = GetCurrentRating(driver);
                Log.WriteToLog($"{Username}: Current Rating is: {currentRating.Pastel(Color.Yellow)}");
                Linenotify.lineNotify($"USB V2:{Username}: Current Rating is: {currentRating}");
                Log.WriteToLog($"{Username}: Quest details: {JsonConvert.SerializeObject(quest).Pastel(Color.Yellow)}");
                Linenotify.lineNotify($"USB V2:{Username}: Quest details: {JsonConvert.SerializeObject(quest)}");
                if (Settings.BadQuests.Contains((string)quest["splinter"]))
                {
                    RequestNewQuest(driver, quest);
                }
                ClaimQuestReward(driver, quest);

                ClosePopups(driver);

                // todo: implement selectCorrectBattleType
                StartBattle(driver); // todo: try catch, return true/false
                WaitForLoadingBanner(driver);
                int counter = 0;
                while (!driver.WaitForWebsiteLoadedAndElementShown(By.CssSelector("button[class='btn btn--create-team']")))
                {
                    if (counter++ > 7)
                    {
                        Log.WriteToLog($"{Username}: Can't seem to find an enemy{Environment.NewLine}Skipping Account", Log.LogType.CriticalError);
                        SleepUntil = DateTime.Now.AddMinutes(Settings.SleepBetweenBattles / 2);
                        return SleepUntil;
                    }
                    // check if this is correct modal;
                }

                int mana = GetMana(driver);
                string rulesets = GetRulesets(driver);
                string[] allowedSplinters = GetAllowedSplinters(driver);
                driver.ClickElementOnPage(By.CssSelector("button[class='btn btn--create-team']"));
                SleepUntil = DateTime.Now.AddMinutes(Settings.SleepBetweenBattles);
                WaitForLoadingBanner(driver);
                var team = await API.GetTeamFromAPIAsync(mana, rulesets, allowedSplinters, cards, quest, Username);
                if (team == null || (string)team["summoner_id"] == "")
                {
                    Log.WriteToLog($"{Username}: API didn't find any team - Skipping Account", Log.LogType.CriticalError);
                    Linenotify.lineNotify($"USB V2:{Username}: API didn't find any team - Skipping Account");
                    SleepUntil = DateTime.Now.AddMinutes(Settings.SleepBetweenBattles / 2);
                    return SleepUntil;
                }
                SelectTeam(driver, team);

                counter = 0;
                while (!driver.WaitForWebsiteLoadedAndElementShown(By.Id("btnRumble")))
                {
                    if (counter++ > 12)
                    {
                        if (driver.WaitForWebsiteLoadedAndElementShown(By.XPath("//h1[contains(., 'BATTLE RESULT')]")))
                        {
                            // enemy probably didn't pick anything
                            break;
                        }
                        Log.WriteToLog($"{Username}: Can't seem to find btnRumble{Environment.NewLine}Skipping Account", Log.LogType.CriticalError);
                        Linenotify.lineNotify($"USB V2:{Username}: Can't seem to find btnRumble{Environment.NewLine}Skipping Account");
                        SleepUntil = DateTime.Now.AddMinutes(Settings.SleepBetweenBattles / 2);
                        return SleepUntil;
                    }
                }
                
                Thread.Sleep(1000);
                driver.ClickElementOnPage(By.Id("btnRumble"));
                Log.WriteToLog($"{Username}: Rumble button clicked");
                driver.WaitForWebsiteLoadedAndElementShown(By.Id("btnSkip"));
                Thread.Sleep(2000);
                driver.ClickElementOnPage(By.Id("btnSkip"));
                Log.WriteToLog($"{Username}: Skip button clicked");

                GetBattleResult(driver, currentRating);

                // todo: determine winner, show summary etc
                Log.WriteToLog($"{Username}: Finished battle!");
                Linenotify.lineNotify($"USB V2:{Username}: Finished battle!");
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: {ex}{Environment.NewLine}Skipping Account", Log.LogType.CriticalError);
                Linenotify.lineNotify($"USB V2:{Username}There was an error, please check the bot. Skipping Account");
            }
            finally
            {
                if (logoutNeeded)
                {
                    driver.ExecuteJavaScript("SM.Logout();", true);
                }
                Settings.LogSummaryList.Add((LogSummary.Index, LogSummary.Account, LogSummary.BattleResult, LogSummary.Rating, LogSummary.ECR, LogSummary.QuestStatus));
                lock (_activeLock)
                {
                    CurrentlyActive = false;
                }
            }
            return SleepUntil;
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
                    Linenotify.lineNotify($"USB V2:{Username}: { logTextBattleResult }");
                    Log.WriteToLog($"{Username}: New rating is {rating} ({ ratingChange.Pastel(Color.Green) })");
                    Linenotify.lineNotify($"USB V2:{Username}: New rating is {rating} ({ ratingChange })");
                }
                else
                {
                    rating = driver.FindElement(By.CssSelector("section.player.loser .rating-total")).Text.ToLower();
                    ratingChange = driver.FindElement(By.CssSelector("section.player.loser .rating-delta")).Text;
                    logTextBattleResult = $"You lost :(";
                    Log.WriteToLog($"{Username}: { logTextBattleResult.Pastel(Color.Red) }");
                    Linenotify.lineNotify($"USB V2:{Username}: { logTextBattleResult }");
                    Log.WriteToLog($"{Username}: New rating is {rating} ({ ratingChange.Pastel(Color.Red) })");
                    Linenotify.lineNotify($"USB V2:{Username}: New rating is {rating} ({ ratingChange })");
                    API.ReportLoss(winner, Username);
                }
            }
            else
            {
                rating = oldRating;
                ratingChange = "+- 0";
                logTextBattleResult = "DRAW";
                Log.WriteToLog($"{Username}: { logTextBattleResult}");
                Linenotify.lineNotify($"USB V2:{Username}: { logTextBattleResult}");
                Log.WriteToLog($"{Username}: Rating has not changed ({ rating })");
                Linenotify.lineNotify($"USB V2:{Username}: Rating has not changed ({ rating })");
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
                    string colorToPlay = "";
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
                    Linenotify.lineNotify($"USB V2:{Username}: Claiming season rewards");
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
                    Linenotify.lineNotify($"USB V2:{Username}: Claimed season rewards");
                }
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at claiming season rewards: {ex}", Log.LogType.Error);
                Linenotify.lineNotify($"USB V2:{Username}: Error at claiming season rewards: {ex}");
            }
        }

        private void ClaimQuestReward(IWebDriver driver, JToken quest)
        {
            try
            {
                string logText;
                if (driver.WaitForWebsiteLoadedAndElementShown(By.Id("quest_claim_btn"), 1))
                {
                    logText = "Quest reward can be claimed";
                    Log.WriteToLog($"{Username}: {logText.Pastel(Color.Green)}");
                    Linenotify.lineNotify($"USB V2:{Username}: {logText}");
                    // short logText:
                    logText = "Quest reward available!";
                    if (!Settings.ClaimQuestReward)
                    {
                        return;
                    }
                    
                    Log.WriteToLog($"{Username}: Claiming quest reward...");
                    Linenotify.lineNotify($"USB V2:{Username}: Claiming quest reward...");
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
                    Linenotify.lineNotify($"USB V2:{Username}: Claimed quest reward");
                }
                else
                {
                    Log.WriteToLog($"{Username}: No quest reward to be claimed");
                    Linenotify.lineNotify($"USB V2:{Username}: No quest reward to be claimed");
                    // short logText:
                    logText = "No quest reward...";
                }

                LogSummary.QuestStatus = $" { (string)quest["completed"] }/{ (string)quest["total"] } {logText}";
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at claiming quest rewards: {ex}", Log.LogType.Error);
                Linenotify.lineNotify($"USB V2:{Username}: Error at claiming quest rewards: {ex}");
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
                    Linenotify.lineNotify($"USB V2:{ Username }: Quest type is { (string)quest["splinter"] } - requesting new one.");

                    driver.ClickElementOnPage(By.Id("quest_new_btn"));
                    Thread.Sleep(1500);
                    driver.SwitchTo().Alert().Accept();
                    WaitForLoadingBanner(driver);
                    Thread.Sleep(3000);
                    WaitForLoadingBanner(driver);
                    ClosePopups(driver);
                    Thread.Sleep(1000);
                    Log.WriteToLog($"{Username}: {"Renewed quest!".Pastel(Color.Green)}");
                    Linenotify.lineNotify($"USB V2:{Username}: {"Renewed quest!".Pastel(Color.Green)}");
                }
                else
                {
                    Log.WriteToLog($"{Username}: { "Can't change quest".Pastel(Color.Red) }");
                    Linenotify.lineNotify($"USB V2:{Username}: { "Can't change quest".Pastel(Color.Red) }");
                }
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at changing quest: {ex}", Log.LogType.Error);
                Linenotify.lineNotify($"USB V2:{Username}: Error at changing quest: {ex}");
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
                Linenotify.lineNotify($"USB V2:{Username}: Couldn't get Energy Capture Rate: {ex}");
            }
            return 0;
        }

        private string GetCurrentRating(IWebDriver driver)
        {
            try
            {
                var rating = driver.FindElement(By.XPath("//div[@class='progress__info']/span[@class='number_text']")).GetAttribute("innerHTML");
                return rating;
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Couldn't get current rating: {ex}", Log.LogType.Warning);
                Linenotify.lineNotify($"USB V2:{Username}: Couldn't get current rating: {ex}");
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
                            Log.WriteToLog($"{Username}: Already logged in!");
                            Linenotify.lineNotify($"USB V2:{Username}: Already logged in!");
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
            driver.SetData(By.Id("password"), Password);
            // endless circle workaround
            if (driver.FindElements(By.ClassName("loading")).Count > 0)
            {
                Log.WriteToLog($"{ (UnknownUsername ? Email : Username) }: Splinterlands not loading, trying to reload...");
                Linenotify.lineNotify($"USB V2:{ (UnknownUsername ? Email : Username) }: Splinterlands not loading, trying to reload...");
                Login(driver, logoutNeeded);
            }
            driver.ClickElementOnPage(By.Name("loginBtn"), 1);

            if (!driver.WaitForWebsiteLoadedAndElementShown(By.Id("log_in_text"), 60))
            {
                Log.WriteToLog($"{ (UnknownUsername ? Email : Username) }: Could not log in - skipping account.", Log.LogType.Error);
                Linenotify.lineNotify($"USB V2:{ (UnknownUsername ? Email : Username) }:  Could not log in - skipping account."); //dee
                return false;
            }

            Log.WriteToLog($"{ (UnknownUsername ? Email : Username) }: Login successful");
            Linenotify.lineNotify($"USB V2:{ (UnknownUsername ? Email : Username) }: Login successful"); //dee
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
