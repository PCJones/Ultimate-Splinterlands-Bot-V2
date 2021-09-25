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
        public string Username { get; init; }
        public string Email { get; init; }
        public string Password { get; init; }
        private DateTime LastBattle;

        private readonly bool UnknownUsername;
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
        public void DoBattle(IWebDriver driver)
        {
            try
            {
                if (!Login(driver))
                {
                    return;
                }
                if (UnknownUsername)
                {

                }
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{Username}: {ex}{Environment.NewLine}Skipping Account", Log.LogType.CriticalError);
            }
            finally
            {
                driver.ExecuteJavaScript("SM.Logout();");
            }
        }

        private bool Login(IWebDriver driver)
        {
            Log.WriteToLog($"{ (UnknownUsername ? Email : Username) }: Trying to login...");
            driver.Navigate().GoToUrl("https://splinterlands.com/");
            do
            {
                Thread.Sleep(500);
            } while (driver.FindElements(By.ClassName("loading")).Count > 0);
            driver.WaitForWebsiteLoadedAndElementShown(By.Id("log_in_button"));
            driver.ClickElementOnPage(By.Id("log_in_button"));

            driver.WaitForWebsiteLoadedAndElementShown(By.Id("email"));
            driver.SetData(By.Id("email"), Username);
            driver.SetData(By.Id("password"), Password);
            driver.ClickElementOnPage(By.Name("loginBtn"), 1);

            if (!driver.WaitForWebsiteLoadedAndElementShown(By.ClassName("close"), 12) && !driver.PageContainsString("Welcome back,"))
            {
                Log.WriteToLog($"{ (UnknownUsername ? Email : Username) } Could not log in - skipping account.", Log.LogType.Error);
                return false;
            }

            Log.WriteToLog($"{ (UnknownUsername ? Email : Username) }: Login successful", Log.LogType.Success);
            return true;
        }
    }
}
