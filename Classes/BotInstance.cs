using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
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
        public async Task DoBattleAsync(IWebDriver driver, bool logout)
        {
            try
            {
                if (!Login(driver))
                {
                    return;
                }
                if (UnknownUsername)
                {
                    driver.WaitForWebsiteLoaded(By.ClassName("bio__name__display"));
                    Thread.Sleep(5000);
                    driver.WaitForWebsiteLoadedAndElementShown(By.ClassName("bio__name__display"));
                    Username = driver.FindElement(By.ClassName("bio__name__display")).Text.Trim().ToLower();
                    Log.WriteToLog($"{Email}: Username is {Username}");
                    UnknownUsername = false;
                }

                JToken quest = await API.GetPlayerQuestAsync(Username);
                string[] cards = await API.GetPlayerCards(Username);
                Log.WriteToLog($"{Username}: Deck size: {cards.Length - 1}"); // Minus 1 because phantom card array has an empty string in it
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: {ex}{Environment.NewLine}Skipping Account", Log.LogType.CriticalError);
            }
            finally
            {
                if (logout)
                {
                    driver.ExecuteJavaScript("SM.Logout();");
                }
            }
        }

        private bool Login(IWebDriver driver)
        {
            Log.WriteToLog($"{ (UnknownUsername ? Email : Username) }: Trying to login...");
            driver.Navigate().GoToUrl("https://splinterlands.com/?p=battle_history");
            do
            {
                Thread.Sleep(500);
            } while (driver.FindElements(By.ClassName("loading")).Count > 0);
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
    }
}
