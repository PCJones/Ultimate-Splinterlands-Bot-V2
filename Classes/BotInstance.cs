using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ultimate_Splinterlands_Bot_V2.Classes
{
    public class BotInstance
    {
        // todo: check which parameters need to be public / can be private
        public string Username { get; set; }
        public string Email { get; init; }
        public string Password { get; init; }
        private DateTime LastBattle;

        private bool UnknownUsername;
        public BotInstance(string username, string password)
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
            }

            Password = password;
            LastBattle = DateTime.Now.AddMinutes((Settings.SleepBetweenBattles + 1) * - 1);
        }
        public async Task DoBattleAsync(IWebDriver driver, bool logoutNeeded)
        {
            try
            {
                if (!Login(driver, logoutNeeded))
                {
                    return;
                }
                if (UnknownUsername)
                {
                    driver.WaitForWebsiteLoaded(By.ClassName("bio__name__display"));
                    Thread.Sleep(4000);
                    driver.WaitForWebsiteLoadedAndElementShown(By.ClassName("bio__name__display"));
                    Username = driver.FindElement(By.ClassName("bio__name__display")).Text.Trim().ToLower();
                    Log.WriteToLog($"{Email}: Username is {Username}");
                    UnknownUsername = false;
                }

                JToken quest = await API.GetPlayerQuestAsync(Username);
                string[] cards = await API.GetPlayerCardsAsync(Username);
                Log.WriteToLog($"{Username}: Deck size: {cards.Length - 1}"); // Minus 1 because phantom card array has an empty string in it
                ClosePopups(driver);
                NavigateToBattlePage(driver);
                double erc = GetERC(driver);
                // todo: add log with different colors in same line
                Log.WriteToLog($"{Username}: Current Energy Capture Rate is {erc}%");
                if (erc < Settings.ERCThreshold)
                {
                    Log.WriteToLog($"{Username}: ERC is below threshold of {Settings.ERCThreshold}% - skipping this account.", Log.LogType.Warning);
                    // todo: maybe add sleep if logoutNeeded is false
                    return;
                }

                if (Settings.ClaimSeasonReward)
                {
                    ClaimSeasonRewards(driver);
                }
                string currentRating = GetCurrentRating(driver);
                Log.WriteToLog($"{Username}: Current Rating is: {currentRating}", Log.LogType.Warning);
                Log.WriteToLog($"{Username}: Quest details: {JsonConvert.SerializeObject(quest)}", Log.LogType.Warning);
                ClaimQuestReward(driver);

                // todo: implement selectCorrectBattleType
                StartBattle(driver); // todo: try catch, return true/false
                WaitForLoadingBanner(driver);
                int counter = 0;
                while (!driver.WaitForWebsiteLoadedAndElementShown(By.CssSelector("button[class='btn btn--create-team']")))
                {
                    if (counter++ > 7)
                    {
                        Log.WriteToLog($"{Username}: Can't seem to find an enemy{Environment.NewLine}Skipping Account", Log.LogType.CriticalError);
                        return;
                    }
                    // check if this is correct modal;
                }

                int mana = GetMana(driver);
                string rulesets = GetRulesets(driver);
                string[] allowedSplinters = GetAllowedSplinters(driver);
                driver.ClickElementOnPage(By.CssSelector("button[class='btn btn--create-team']"));
                WaitForLoadingBanner(driver);
                var team = await API.GetTeamFromAPIAsync(mana, rulesets, allowedSplinters, cards, quest, Username);
                if ((string)team["summoner_id"] == "")
                {
                    Log.WriteToLog($"{Username}: API didn't find any team - Skipping Account", Log.LogType.CriticalError);
                    return;
                }
                SelectTeam(driver, team);

                // todo: determine winner, show summary etc
                Log.WriteToLog($"{Username}: Finished battle!", Log.LogType.Success);
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: {ex}{Environment.NewLine}Skipping Account", Log.LogType.CriticalError);
            }
            finally
            {
                if (logoutNeeded)
                {
                    driver.ExecuteJavaScript("SM.Logout();");
                }
            }
        }

        private void SelectTeam(IWebDriver driver, JToken team)
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
            driver.FindElement(By.XPath($"//div[@card_detail_id={summonerID}]")).Click();
            Thread.Sleep(1000);
            if (GetSummonerColor(summonerID) == "dragon")
            {
                // check
                Log.WriteToLog($"{Username}: DRAGON - Teamcolor: {color}");
                driver.WaitForWebsiteLoadedAndElementShown(By.CssSelector($"label[for='filter-element-{color}-button']"));
                driver.ClickElementOnPage(By.CssSelector($"label[for='filter-element-{color}-button']"));
            }
            driver.WaitForWebsiteLoadedAndElementShown(By.XPath($"//div[@card_detail_id={monster1}]"));
            driver.FindElement(By.XPath($"//div[@card_detail_id={monster1}]")).Click();
            if (monster2 != "")
                driver.FindElement(By.XPath($"//div[@card_detail_id={monster2}]")).Click();

            if (monster3 != "")
                driver.FindElement(By.XPath($"//div[@card_detail_id={monster3}]")).Click();

            if (monster4 != "")
                driver.FindElement(By.XPath($"//div[@card_detail_id={monster4}]")).Click();

            if (monster5 != "")
                driver.FindElement(By.XPath($"//div[@card_detail_id={monster5}]")).Click();

            if (monster6 != "")
                driver.FindElement(By.XPath($"//div[@card_detail_id={monster6}]")).Click();


        }

        private static string GetSummonerColor(string id)
        {
            return Settings.Summoners.ContainsKey(id) ? Settings.Summoners[id] : "";
        }

        private int GetMana(IWebDriver driver)
        {
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
            Log.WriteToLog($"{Username} Waiting for battle button...");
            driver.WaitForWebsiteLoadedAndElementShown(By.XPath("//button[contains(., 'BATTLE')]"));
            driver.ClickElementOnPage(By.XPath("//button[contains(., 'BATTLE')]"));
            Log.WriteToLog($"{Username} Battle button clicked!");
        }
        private void NavigateToBattlePage(IWebDriver driver)
        {
            if (!driver.Url.Contains("battle_history"))
            {
                driver.ClickElementOnPage(By.Id("menu_item_battle"));
                driver.WaitForWebsiteLoadedAndElementShown(By.Id("battle_category_btn"));
            }
        }

        private void ClaimSeasonRewards(IWebDriver driver)
        {
            try
            {
                if (driver.WaitForElementShown(By.Id("claim-btn"), 1))
                {
                    Log.WriteToLog($"{Username}: Claiming season rewards");
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

        private void ClaimQuestReward(IWebDriver driver)
        {
            try
            {
                if (driver.WaitForElementShown(By.Id("claim-btn"), 1))
                {
                    Log.WriteToLog($"{Username}: Quest reward can be claimed", Log.LogType.Success);
                    if (!Settings.ClaimQuestReward)
                    {
                        return;
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
                    Log.WriteToLog($"{Username}: Claimed quest reward", Log.LogType.Success);
                }
                else
                {
                    Log.WriteToLog($"{Username}: No quest reward to be claimed ");
                }
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at claiming quest rewards: {ex}", Log.LogType.Error);
            }
        }

        private void ClosePopups(IWebDriver driver)
        {
            try
            {
                if (driver.WaitForWebsiteLoaded(By.ClassName("close"), 4))
                {
                    if (driver.WaitForElementShown(By.ClassName("close"), 1))
                    {
                        Log.WriteToLog($"{Username}: Closing popup #1");
                        driver.ClickElementOnPage(By.ClassName("close"));
                    }
                }
                if (driver.WaitForWebsiteLoaded(By.ClassName("modal-close-new"), 1))
                {
                    if (driver.WaitForElementShown(By.ClassName("modal-close-new"), 1))
                    {
                        Log.WriteToLog($"{Username}: Closing popup #2");
                        driver.ClickElementOnPage(By.ClassName("modal-close-new"));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: Error at closing popups: {ex}", Log.LogType.Error);
            }
        }

        private double GetERC(IWebDriver driver)
        {
            try
            {
                var erc = driver.FindElement(By.XPath("//div[@class='dec-options'][1]/div[@class='value'][2]/div")).GetAttribute("innerHTML");
                return Convert.ToDouble(erc[..^1], CultureInfo.InvariantCulture);
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
                var rating = driver.FindElement(By.XPath("//div[@class='progress__info']/span[@class='number_text']")).GetAttribute("innerHTML");
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
            driver.Navigate().GoToUrl("https://splinterlands.com/?p=battle_history");
            WaitForLoadingBanner(driver);
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
                            return true;
                        }
                    }
                }
                // this shouldn't happen unless session is no longer valid, call logout to be sure
                driver.ExecuteJavaScript("SM.Logout();");
            }
            driver.WaitForWebsiteLoadedAndElementShown(By.Id("log_in_button"));
            driver.ClickElementOnPage(By.Id("log_in_button"));

            driver.WaitForWebsiteLoadedAndElementShown(By.Id("email"));
            driver.SetData(By.Id("email"), UnknownUsername ? Email : Username);
            driver.SetData(By.Id("password"), Password);
            driver.ClickElementOnPage(By.Name("loginBtn"), 1);

            if (!driver.WaitForWebsiteLoadedAndElementShown(By.ClassName("close"), 12) && !driver.PageContainsString("Welcome back,"))
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
