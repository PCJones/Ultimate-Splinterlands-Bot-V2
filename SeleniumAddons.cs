using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support;
using OpenQA.Selenium.Support.UI;
using System.IO;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace Paid4_Remote_Bot_Client.Classes
{
    public static class SeleniumAddons
    {
        public static IWebDriver CreateSeleniumInstance(bool fireFox = false, bool iMacros = false, bool langEN = false, bool canvasBlock = true, int timeSpan = 50, bool urlBlocker = true, bool RecaptchaSolver = false, bool fittingUseragent = false, bool originalUseragent = false, string SetUserAgent = "", bool RecaptchaBuster = false)
        {
            // debugging
            if (Settings.DebugDriver != null)
            {
                return Settings.DebugDriver;
            }
            IWebDriver driver = null;
            while (driver == null)
            {
                try
                {
                    if (fireFox)
                    {
                        FirefoxProfile ffProfile = new FirefoxProfile();
                        if (SetUserAgent != "")
                        {
                            ffProfile.SetPreference("general.useragent.override", SetUserAgent);
                        }
                        else if (!fittingUseragent)
                        {
                            ffProfile.SetPreference("general.useragent.override", Properties.Settings.Default.userAgent);
                        }
                        else if (!originalUseragent)
                        {
                            string userAgent = Properties.Settings.Default.userAgent;
                            while (!userAgent.Contains("Firefox/"))
                            {
                                userAgent = Misc.GetRandomUserAgent();
                            }
                            ffProfile.SetPreference("general.useragent.override", userAgent);
                        }
                        FirefoxOptions options = new FirefoxOptions
                        {
                            Profile = ffProfile
                        };

                        FirefoxDriverService service = FirefoxDriverService.CreateDefaultService();
                        service.FirefoxBinaryPath = @"C:\Program Files (x86)\Mozilla Firefox\firefox.exe";
                        TimeSpan timeout = new TimeSpan(0, 0, 60);
                        driver = new FirefoxDriver(service, options, timeout);
                    }
                    else
                    {
                        ChromeOptions chromeOptions = new ChromeOptions();
                        chromeOptions.AddArgument("--ignore-certificate-errors");
                        // deactivated wegen GUI thread updates
                        //Log.WriteToLog("CERTIFICATE TEMP FIX ACTIVATED!", 3);
                        chromeOptions.AddExcludedArguments(new List<string>() { "enable-automation" });
                        if (SetUserAgent != "")
                        {
                            chromeOptions.AddArgument("user-agent=" + SetUserAgent);
                        }
                        else if (!fittingUseragent)
                        {
                            chromeOptions.AddArgument("user-agent=" + Properties.Settings.Default.userAgent);
                        }
                        else if (!originalUseragent)
                        {
                            string userAgent = Properties.Settings.Default.userAgent;
                            while (!userAgent.Contains("KHTML, like Gecko"))
                            {
                                userAgent = Misc.GetRandomUserAgent();
                            }
                            chromeOptions.AddArgument("user-agent=" + userAgent);
                        }
                        chromeOptions.AddArgument("--mute-audio");
                        chromeOptions.AddArgument("--disable-notifications");

                        // RecaptchaBuster
                        if (RecaptchaBuster)
                        {
                            chromeOptions.AddExtension(Settings.startupPath + "\\Config\\BrowserAddons\\busterRecaptcha.crx");
                        }
                        // Canvas 
                        if (canvasBlock)
                        {
                            chromeOptions.AddExtension(Settings.startupPath + "\\Config\\BrowserAddons\\canvasdef.crx");
                        }
                        if (iMacros)
                        {
                            chromeOptions.AddExtension(Settings.startupPath + "\\Config\\BrowserAddons\\imacros.crx");
                        }
                        if (langEN)
                        {
                            chromeOptions.AddArguments("--lang=en");
                        }
                        if (urlBlocker)
                        {
                            chromeOptions.AddExtension(Settings.startupPath + "\\Config\\BrowserAddons\\URLBlocker.crx");
                        }
                        if (RecaptchaSolver)
                        {
                            chromeOptions.AddExtension(Settings.startupPath + "\\Config\\BrowserAddons\\CaptchaSolver.crx");
                        }
                        chromeOptions.AddExtension(Settings.startupPath + "\\Config\\BrowserAddons\\AvoidDetection.crx");
                        driver = new ChromeDriver(Settings.startupPath, chromeOptions, TimeSpan.FromSeconds(timeSpan));
                    }
                    driver.Manage().Window.Position = new System.Drawing.Point(0, 0);
                    driver.Manage().Window.Maximize();
                    if (RecaptchaSolver)
                    {
                        System.Threading.Thread.Sleep(3500);
                        driver.WaitForWebsiteLoaded(By.Id("apiKey"));
                        driver.JavaScriptClick("isEnabled");
                        System.Threading.Thread.Sleep(50);
                        driver.ExecuteJavaScript("window.open('');");
                        System.Threading.Thread.Sleep(50);
                        driver.SwitchTo().Window(driver.WindowHandles[1]);
                    }

                    if (iMacros)
                    {
                        driver.SwitchTo().Window(driver.WindowHandles[1]);
                    }
                }
                catch (WebDriverException)
                {
                }
            }

            return driver;
        }

        /// <summary>
        /// Clicks an element on the current page
        /// </summary>
        /// <param name="by">By</param>
        /// <param name="elementIndex">Index of the element if there are multiple</param>
        public static void ClickElementOnPage(this IWebDriver WebDriver, By by, int elementIndex = 0, [CallerMemberName] string callerName = "")
        {
            try
            {
                OpenQA.Selenium.Remote.RemoteWebElement remoteElement = (OpenQA.Selenium.Remote.RemoteWebElement)WebDriver.FindElements(by)[elementIndex];

                System.Drawing.Point position = remoteElement.LocationOnScreenOnceScrolledIntoView;

                try
                {
                    remoteElement.Click();
                }
                catch (InvalidElementStateException)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString());
            }
        }

        public static IWebElement GetElementByCoordinates(this IWebDriver WebDriver, IWebElement element, [CallerMemberName] string callerName = "")
        {
            IWebElement returnElement = null;
            try
            {
                IJavaScriptExecutor executor = (IJavaScriptExecutor)WebDriver;
                executor.ExecuteScript("arguments[0].scrollIntoView();", element);
                var xx = executor.ExecuteScript("return window.pageXOffset;");
                int newX = element.Location.X - Convert.ToInt32(executor.ExecuteScript("return window.pageXOffset;"));
                int newY = element.Location.Y - Convert.ToInt32(executor.ExecuteScript("return window.pageYOffset;"));
                returnElement = (IWebElement)executor.ExecuteScript("return document.elementFromPoint(" + newX + ", " + newY + ")");
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "Element: " + element.ToString());
            }

            return returnElement;
        }

        /// <summary>
        /// Clicks an element on the current page
        /// </summary>
        /// <param name="by">By</param>
        /// <param name="elementIndex">Index of the element if there are multiple</param>
        public static void ClickElementOnPage(this IWebDriver WebDriver, IWebElement element, [CallerMemberName] string callerName = "")
        {
            try
            {
                OpenQA.Selenium.Remote.RemoteWebElement remoteElement = (OpenQA.Selenium.Remote.RemoteWebElement)element;

                System.Drawing.Point position = remoteElement.LocationOnScreenOnceScrolledIntoView;

                try
                {
                    remoteElement.Click();
                }
                catch (InvalidElementStateException)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "Element: " + element.ToString());
            }
        }

        /// <summary>
        /// Submits a form
        /// </summary>
        /// <param name="by">By</param>
        /// <param name="elementIndex">Index of the element if there are multiple</param>
        public static void SubmitForm(this IWebDriver WebDriver, By by, int elementIndex = 0, [CallerMemberName] string callerName = "")
        {
            try
            {
                WebDriver.FindElements(by)[elementIndex].Submit();
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString());
            }
        }

        /// <summary>
        /// Sets data into textfields.
        /// </summary>
        /// <param name="by">By</param>
        /// <param name="Data">Data / Key (ex.: Keys.Tab)</param>
        /// <param name="elementIndex">Index of the element if there are multiple</param>
        public static void SetData(this IWebDriver WebDriver, By by, string Data, int elementIndex = 0, [CallerMemberName] string callerName = "")
        {
            try
            {
                WebDriver.FindElements(by)[elementIndex].Clear();
                WebDriver.FindElements(by)[elementIndex].SendKeys(Data);
            }
            catch (Exception ex)
            {
                StackFrame frame = new StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString());
            }
        }

        /// <summary>
        /// Waits for a given success message (max. 30 seconds with 2 seconds check interval)
        /// </summary>
        /// <param name="Message">Message to look at</param>
        public static bool WaitForSuccessMessage(this IWebDriver WebDriver, string Message, int waitingTime = 15, [CallerMemberName] string callerName = "")
        {
            bool returnBool = false;
            try
            {
                // Waite till website contains a give peace of text (max. 30 seconds)
                for (int i = 0; i < waitingTime; i++)
                {
                    try
                    {
                        if (WebDriver.PageSource.Contains(Message))
                        {
                            returnBool = true;
                            break;
                        }
                        System.Threading.Thread.Sleep(2000);
                    }
                    catch (Exception)
                    {
                        System.Threading.Thread.Sleep(1000);
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex);
            }
            return returnBool;
        }

        /// <summary>
        /// Waits for an element to load and to be shown
        /// </summary>
        /// <param name="by">By</param>
        /// <param name="seconds">Maximum seconds to wait</param>
        /// <param name="elementIndex">Index of the element if there are multiple</param>
        public static void WaitForWebsiteLoadedAndElementShown(this IWebDriver WebDriver, By by, int seconds = 15, int elementIndex = 0, [CallerMemberName] string callerName = "")
        {
            WaitForWebsiteLoaded(WebDriver, by, seconds, elementIndex, callerName);
            WaitForElementShown(WebDriver, by, (seconds / 2), elementIndex, callerName);
        }

        /// <summary>
        /// Waits till the website is fully loaded by a given time (default = 30 seconds)
        /// </summary>
        /// <param name="by">By</param>
        /// <param name="Time">Time to wait (default 30 seconds)</param>
        /// <param name="elementIndex">Index of the element if there are multiple</param>
        //[DebuggerNonUserCode]
        public static void WaitForWebsiteLoaded(this IWebDriver WebDriver, By by, int Time = 16, int elementIndex = 0, [CallerMemberName] string callerName = "")
        {
            try
            {
                Time = Time / 2;

                for (int i = 0; i < Time; i++)
                {
                    try
                    {
                        IWebElement element = WebDriver.FindElements(by)[elementIndex];

                        if (element != null)
                        {
                            break;
                        }
                        else
                        {
                            System.Threading.Thread.Sleep(2000);
                        }
                    }
                    catch (WebDriverException ex)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        System.Threading.Thread.Sleep(2000);
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString());
            }
        }

        /// <summary>
        /// Switches to an frame / iframe
        /// </summary>
        /// <param name="Name">Frame name</param>
        public static void SwitchToFrame(this IWebDriver WebDriver, string Name, [CallerMemberName] string callerName = "")
        {
            try
            {
                // Switch to iframe
                WebDriver.SwitchTo().Frame(Name);
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "Frame-Name: " + Name);
            }
        }

        /// <summary>
        /// Switches to an frame / iframe
        /// </summary>
        /// <param name="Index">Frame index</param>
        public static void SwitchToFrame(this IWebDriver WebDriver, int Index, [CallerMemberName] string callerName = "")
        {
            try
            {
                // Switch to iframe
                WebDriver.SwitchTo().Frame(Index);
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "Frame-Index: " + Index.ToString());
            }
        }

        /// <summary>
        /// Switches to an frame / iframe
        /// </summary>
        /// <param name="Element">Frame element</param>
        public static void SwitchToFrame(this IWebDriver WebDriver, IWebElement Element, [CallerMemberName] string callerName = "")
        {
            try
            {
                // Switch to iframe
                WebDriver.SwitchTo().Frame(Element);
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "Element: " + Element.ToString());
            }
        }

        /// <summary>
        /// Switches to an frame / iframe
        /// </summary>
        /// <param name="by">Frame By</param>
        public static void SwitchToFrame(this IWebDriver WebDriver, By by, [CallerMemberName] string callerName = "")
        {
            try
            {
                // Switch to iframe
                WebDriver.SwitchTo().Frame(WebDriver.FindElement(by));
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString());
            }
        }

        /// <summary>
        /// Switches to the default frame (main page)
        /// </summary>
        public static void SwitchToDefaultFrame(this IWebDriver WebDriver, [CallerMemberName] string callerName = "")
        {
            try
            {
                WebDriver.SwitchTo().DefaultContent();
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex);
            }
        }

        /// <summary>
        /// Gets a list with web elements
        /// </summary>
        /// <param name="by">By</param>
        /// <returns>Returns a list with web elements</returns>
        public static System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> GetWebElements(this IWebDriver WebDriver, By by, [CallerMemberName] string callerName = "")
        {
            try
            {
                System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> webElements = WebDriver.FindElements(by);
                return webElements;
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString());
                throw;
            }
        }

        /// <summary>
        /// Searches / finds an element and clicks on it
        /// </summary>
        /// <param name="WebDriver">SeleniumWebDriver</param>
        /// <param name="by">By</param>
        /// <param name="Attribute">Html attribute</param>
        /// <param name="Context">Context</param>
        public static void FindAndClickElement(this IWebDriver WebDriver, By by, string Attribute, string Context, [CallerMemberName] string callerName = "")
        {
            try
            {
                System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> aElements = WebDriver.FindElements(by);
                foreach (IWebElement element in aElements)
                {
                    string elementAttribute = element.GetAttribute(Attribute);

                    if (elementAttribute != null)
                    {
                        if (elementAttribute.Contains(Context))
                        {
                            element.Click();
                            break;
                        }
                    }

                    System.Threading.Thread.Sleep(100);
                }

            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString());
            }
        }

        /// <summary>
        /// Clicks all elements on the page
        /// </summary>
        /// <param name="by">By</param>
        public static void ClickAllElements(this IWebDriver WebDriver, By by, [CallerMemberName] string callerName = "")
        {
            try
            {
                foreach (IWebElement element in WebDriver.FindElements(by))
                {
                    if (element.Displayed)
                    {
                        element.Click();
                        System.Threading.Thread.Sleep(650);
                    }

                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString());
            }
        }

        ///// <summary>
        ///// Selects a dropdown value by value
        ///// </summary>
        ///// <param name="WebDriver">SeleniumWebDriver</param>
        ///// <param name="by">By</param>
        ///// <param name="Data">Data / value</param>
        ///// <param name="elementIndex">Index of the element if there are multiple</param>
        //public static void SelectDropDownByValue(IWebDriver WebDriver, By by, string Data, int elementIndex = 0, [CallerMemberName] string callerName = "")
        //{
        //    try
        //    {
        //        new SelectElement(WebDriver.FindElements(by)[elementIndex]).SelectByValue(Data);
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Windows.Forms.MessageBox.Show("Error: " + ex.Message);
        //    }
        //}

        /// <summary>
        /// Selects a dropdown value by index
        /// </summary>
        /// <param name="WebDriver">SeleniumWebDriver</param>
        /// <param name="by">By</param>
        /// <param name="Index">Index</param>
        /// <param name="elementIndex">Index of the element if there are multiple</param>
        public static void SelectDropDownByIndex(this IWebDriver WebDriver, By by, int Index, int elementIndex = 0, [CallerMemberName] string callerName = "")
        {
            try
            {
                IJavaScriptExecutor executor = (IJavaScriptExecutor)WebDriver;
                executor.ExecuteScript("arguments[0].selectedIndex = " + Index.ToString() + ";", WebDriver.FindElements(by)[elementIndex]);
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString() + Environment.NewLine + "Index: " + Index.ToString() + "elementIndex: " + elementIndex.ToString());
            }
        }

        /// <summary>
        /// Selects a dropdown value by index
        /// </summary>
        /// <param name="WebDriver">SeleniumWebDriver</param>
        /// <param name="by">By</param>
        /// <param name="Value">Value</param>
        /// <param name="elementIndex">Index of the element if there are multiple</param>
        public static void SelectDropDownByValue(this IWebDriver WebDriver, By by, string Value, int elementIndex = 0, [CallerMemberName] string callerName = "")
        {
            try
            {
                IJavaScriptExecutor executor = (IJavaScriptExecutor)WebDriver;
                executor.ExecuteScript("for(var i=0;i<arguments[0].options.length;i++)arguments[0].options[i].value==" + Value + "&&(arguments[0].options[i].selected=!0);", WebDriver.FindElements(by)[elementIndex]);
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString() + Environment.NewLine + "Value: " + Value + "elementIndex: " + elementIndex.ToString());
            }
        }

        ///// <summary>
        ///// Selects a dropdown value by text
        ///// </summary>
        ///// <param name="WebDriver">SeleniumWebDriver</param>
        ///// <param name="by">By</param>
        ///// <param name="Text">Text to select</param>
        ///// <param name="elementIndex">Index of the element if there are multiple</param>
        //public static void SelectDropDownByText(IWebDriver WebDriver, By by, string Text, int elementIndex = 0, [CallerMemberName] string callerName = "")
        //{
        //    try
        //    {
        //        new Sele(WebDriver.FindElements(by)[elementIndex]).SelectByText(Text);
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Windows.Forms.MessageBox.Show("Error: " + ex.Message);
        //    }
        //}

        /// <summary>
        /// Checks if the website contains a given string
        /// </summary>
        /// <param name="Message">Message / string to check</param>
        /// <returns>Returns true or false</returns>
        public static bool PageContainsString(this IWebDriver WebDriver, string Message, [CallerMemberName] string callerName = "")
        {
            try
            {
                if (WebDriver.PageSource.Contains(Message))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "Message: " + Message);
            }
            return false;
        }

        /// <summary>
        /// Executes a JavaScript at the form
        /// </summary>
        /// <param name="jsScript">JavaScript to execute</param>
        public static void ExecuteJavaScript(this IWebDriver WebDriver, string jsScript, [CallerMemberName] string callerName = "")
        {
            try
            {
                IJavaScriptExecutor executor = (IJavaScriptExecutor)WebDriver;
                executor.ExecuteScript(jsScript);
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "JS: " + jsScript);
            }
        }


        /// <summary>
        /// Executes a JavaScript at the form
        /// </summary>
        /// <param name="jsScript">JavaScript to execute</param>
        /// <param name="argument">IWebElement for arguments[0]</param>
        public static void ExecuteJavaScript(this IWebDriver WebDriver, string jsScript, IWebElement argument, [CallerMemberName] string callerName = "")
        {
            try
            {
                IJavaScriptExecutor executor = (IJavaScriptExecutor)WebDriver;
                executor.ExecuteScript(jsScript, argument);
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "JS: " + jsScript + Environment.NewLine + "argument: " + argument.ToString());
            }
        }

        /// <summary>
        /// Clicks on an element via JavaScript
        /// </summary>
        /// <param name="id">ID of the lement</param>
        public static void JavaScriptClick(this IWebDriver WebDriver, string id, [CallerMemberName] string callerName = "")
        {
            try
            {
                IJavaScriptExecutor executor = (IJavaScriptExecutor)WebDriver;
                //executor.ExecuteScript("document.getElementById('" + id + "').click();");
                executor.ExecuteScript(Properties.Settings.Default.simulateClickFunctions + Environment.NewLine + "simulate(document.getElementById('" + id + "'), 'click');");
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By.ID: " + id);
            }
        }

        /// <summary>
        /// Clicks on an element via JavaScript
        /// </summary>
        /// <param name="element">Element to click</param>
        public static void JavaScriptClick(this IWebDriver WebDriver, IWebElement element, [CallerMemberName] string callerName = "")
        {
            try
            {
                IJavaScriptExecutor executor = (IJavaScriptExecutor)WebDriver;
                //executor.ExecuteScript("arguments[0].click();", element);
                ///executor.ExecuteScript(Properties.Settings.Default.simulateClickFunctions);
                executor.ExecuteScript(Properties.Settings.Default.simulateClickFunctions + Environment.NewLine + "simulate(arguments[0], 'click');", element);
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "Element: " + element.ToString());
            }
        }

        /// <summary>
        /// Clicks on an element via JavaScript
        /// </summary>
        /// <param name="by">By</param>
        public static void JavaScriptClick(this IWebDriver WebDriver, By by, int elementIndex = 0, [CallerMemberName] string callerName = "")
        {
            try
            {
                IJavaScriptExecutor executor = (IJavaScriptExecutor)WebDriver;
                //executor.ExecuteScript("arguments[0].click();", WebDriver.FindElements(by)[elementIndex]);
                executor.ExecuteScript(Properties.Settings.Default.simulateClickFunctions + Environment.NewLine + "simulate(arguments[0], 'click');", WebDriver.FindElements(by)[elementIndex]);
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString());
            }
        }

        /// <summary>
        /// Clicks on an element via 
        /// API
        /// </summary>
        /// <param name="Element">Element to click</param>
        public static void ActionClick(this IWebDriver WebDriver, IWebElement Element, [CallerMemberName] string callerName = "")
        {
            try
            {
                ExecuteJavaScript(WebDriver, "arguments[0].scrollIntoView();", Element, callerName);
                OpenQA.Selenium.Interactions.Actions actions = new OpenQA.Selenium.Interactions.Actions(WebDriver);
                actions.MoveToElement(Element).ClickAndHold(Element).Release().Build().Perform();
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "Element: " + Element.ToString());
            }
        }

        /// <summary>
        /// Clicks on an element via Actions API
        /// </summary>
        /// <param name="by">By</param>
        public static void ActionClick(this IWebDriver WebDriver, By by, int index = 0, [CallerMemberName] string callerName = "")
        {
            try
            {
                IWebElement element = WebDriver.FindElements(by)[index];
                OpenQA.Selenium.Interactions.Actions actions = new OpenQA.Selenium.Interactions.Actions(WebDriver);
                actions.MoveToElement(element).ClickAndHold(element).Release().Build().Perform();
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString());
            }
        }

        /// <summary>
        /// Clicks on an element via Actions API
        /// </summary>
        /// <param name="by">By</param>
        public static void HoverOverElement(this IWebDriver WebDriver, By by, int index = 0, [CallerMemberName] string callerName = "")
        {
            try
            {
                IWebElement element = WebDriver.FindElements(by)[index];
                OpenQA.Selenium.Interactions.Actions actions = new OpenQA.Selenium.Interactions.Actions(WebDriver);
                actions.MoveToElement(element).Build().Perform();
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString());
            }
        }

        public static void OpenNewTab(this IWebDriver WebDriver, [CallerMemberName] string callerName = "")
        {
            try
            {
                WebDriver.ExecuteJavaScript("window.open('');", callerName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "");
            }
        }

        /// <summary>
        /// Showes a hidden element
        /// </summary>
        /// <param name="elementID">HTML-ID of the element</param>
        public static void ShowElementById(this IWebDriver WebDriver, string elementID, [CallerMemberName] string callerName = "")
        {
            try
            {
                IJavaScriptExecutor executor = (IJavaScriptExecutor)WebDriver;
                executor.ExecuteScript("document.getElementById('" + elementID + "').style.display='block';");
                if (WebDriver.FindElement(By.Id(elementID)).GetAttribute("type") == "hidden")
                {
                    executor.ExecuteScript("document.getElementById('" + elementID + "').setAttribute('type', '');");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By.ID: " + elementID);
            }
        }

        /// <summary>
        /// Showes a hidden element
        /// </summary>
        /// <param name="elementID">HTML-ClassName of the element</param>
        /// <param name="elementIndex">Index of the element if there are multiple</param>
        public static void ShowElementByClassName(this IWebDriver WebDriver, string elementID, int elementIndex = 0, [CallerMemberName] string callerName = "")
        {
            try
            {
                IJavaScriptExecutor executor = (IJavaScriptExecutor)WebDriver;
                executor.ExecuteScript("document.getElementsByClassName('" + elementID + "')[" + elementIndex + "].style.display='block';");
                if (WebDriver.FindElement(By.CssSelector("[class='" + elementID + "']")).GetAttribute("type") == "hidden")
                {
                    executor.ExecuteScript("documentgetElementsByClassName('" + elementID + "')[" + elementIndex + "].setAttribute('type', '');");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By.ID: " + elementID);
            }
        }

        /// <summary>
        /// Waits for an element to show
        /// </summary>
        /// <param name="by">By</param>
        /// <param name="seconds">Maximum seconds to wait</param>
        /// <param name="elementIndex">Index of the element if there are multiple</param>
        public static void WaitForElementShown(this IWebDriver WebDriver, By by, int seconds = 15, int elementIndex = 0, [CallerMemberName] string callerName = "")
        {
            try
            {
                seconds = seconds / 2;
                for (int i = 0; i < seconds; i++)
                {
                    //if (WebDriver.FindElements(by)[elementIndex].GetAttribute("type") == "hidden")
                    if (WebDriver.FindElements(by)[elementIndex].Displayed == false)
                    {
                        System.Threading.Thread.Sleep(2000);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString() + Environment.NewLine + "Element-Index: " + elementIndex.ToString());
            }
        }

        /// <summary>
        /// Sets data into textfields while simulating typing
        /// </summary>
        /// <param name="by">By</param>
        /// <param name="Data">Data / Key (ex.: Keys.Tab)</param>
        /// <param name="elementIndex">Index of the element if there are multiple</param>
        public static void SimulateTyping(this IWebDriver WebDriver, By by, string Data, int elementIndex = 0, bool sendTabAtTheEnd = true, bool clearElement = true, int minWait = 95, int maxWait = 200, [CallerMemberName] string callerName = "")
        {
            try
            {
                if (clearElement)
                {
                    WebDriver.FindElements(by)[elementIndex].Clear();
                }
                Random rnd = new Random();
                if (maxWait == 0)
                {
                    WebDriver.FindElements(by)[elementIndex].SendKeys(Data);
                }
                else
                {
                    foreach (char buchstabe in Data)
                    {
                        WebDriver.FindElements(by)[elementIndex].SendKeys(buchstabe.ToString());
                        System.Threading.Thread.Sleep(rnd.Next(minWait, maxWait));
                    }
                }

                if (sendTabAtTheEnd)
                {
                    WebDriver.FindElements(by)[elementIndex].SendKeys(Keys.Tab);
                    System.Threading.Thread.Sleep(rnd.Next(minWait, maxWait));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString() + Environment.NewLine + "Element-Index: " + elementIndex.ToString());
            }
        }

        /// <summary>
        /// Wait until the email is created (aborts if no email is created after 1500 Seconds)
        /// </summary>
        public static void WaitForEmailCreated(this IWebDriver WebDriver, [CallerMemberName] string callerName = "")
        {
            try
            {
                int maxSeconds = 1500;
                for (int i = 0; i < maxSeconds; i++)
                {
                    if (Settings.currentEmail.Contains("@"))
                    {
                        break;
                    }
                    System.Threading.Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex);
            }
        }

        /// <summary>
        /// Sets the Email into an element after the email is created (aborts if no email is created after 1500 Seconds)
        /// </summary>
        /// <param name="by">By</param>
        /// <param name="elementIndex">Index of the element if there are multiple</param>
        public static void EnterEmailWhenCreated(this IWebDriver WebDriver, By by, int elementIndex = 0, bool clearElement = false, [CallerMemberName] string callerName = "")
        {
            try
            {
                int maxSeconds = 1500;
                for (int i = 0; i < maxSeconds; i++)
                {
                    if (Settings.currentEmail.Contains("@"))
                    {
                        WebDriver.SimulateTyping(by, Settings.currentEmail, elementIndex, clearElement: clearElement, callerName: callerName);
                        break;
                    }
                    System.Threading.Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString() + Environment.NewLine + "Element-Index: " + elementIndex.ToString());
            }
        }

        private static void CheckBonicloudPreEnterData(IWebDriver driver, Link link, [CallerMemberName] string callerName = "")
        {
            try
            {
                if (link.LinkURL.Contains("bonicloud") && driver.Url.Contains("bonicloud.net/qualitycheck/"))
                {
                    if (Settings.currentIdentity[0] == "Male")
                    {
                        driver.ClickElementOnPage(By.Id("gender_0"));
                    }
                    else
                    {
                        driver.ClickElementOnPage(By.Id("gender_1"));
                    }

                    driver.SetData(By.Name("firstname"), Settings.currentIdentity[1]);
                    driver.SetData(By.Name("lastname"), Settings.currentIdentity[2]);
                    driver.EnterEmailWhenCreated(By.Name("email"));

                    string birthday = "";
                    if (Settings.currentIdentity[6].Length == 1)
                    {
                        birthday = "0" + Settings.currentIdentity[6];
                    }
                    else
                    {
                        birthday = Settings.currentIdentity[6];
                    }
                    if (Settings.currentIdentity[7].Length == 1)
                    {
                        birthday += ".0" + Settings.currentIdentity[7];
                    }
                    else
                    {
                        birthday += "." + Settings.currentIdentity[7];
                    }
                    birthday += "." + Settings.currentIdentity[8];

                    driver.SetData(By.Name("birthDate"), birthday);

                    //driver.SetData(By.Name("password"), Settings.currentIdentity[5]);
                    //driver.SetData(By.Name("strasse"), Settings.currentIdentity[11]);
                    //driver.SetData(By.Name("hausnummer"), Settings.currentIdentity[12]);
                    //driver.SetData(By.Name("plz"), Settings.currentIdentity[9]);
                    //driver.SetData(By.Name("ort"), Settings.currentIdentity[10]);
                    driver.ActionClick(By.TagName("button"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "Webdriver URL: " + driver.Url + Environment.NewLine + "Page-Source: " + driver.PageSource);
            }
        }

        private static void CheckVeuroPreEnterData(IWebDriver driver, Link link, [CallerMemberName] string callerName = "")
        {
            try
            {
                if (link.LinkURL.Contains("veuro") && driver.Url.Contains("veuro.de/webapp/extern"))
                {
                    while (driver.PageContainsString("<p>Prüfung läuft"))
                    {
                        System.Threading.Thread.Sleep(500);
                    }
                    if (driver.FindElements(By.Name("email")).Count == 0)
                    {
                        return;
                    }

                    driver.EnterEmailWhenCreated(By.Name("email"));
                    driver.SetData(By.Name("vorname"), Settings.currentIdentity[1]);
                    driver.SetData(By.Name("nachname"), Settings.currentIdentity[2]);
                    driver.SetData(By.Name("password"), Settings.currentIdentity[5]);
                    driver.SetData(By.Name("strasse"), Settings.currentIdentity[11]);
                    driver.SetData(By.Name("hausnummer"), Settings.currentIdentity[12]);
                    driver.SetData(By.Name("plz"), Settings.currentIdentity[9]);
                    driver.SetData(By.Name("ort"), Settings.currentIdentity[10]);
                    driver.JavaScriptClick("submit_id");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "Webdriver URL: " + driver.Url + Environment.NewLine + "Page-Source: " + driver.PageSource);
            }
        }

        /// <summary>
        /// Navigates to every URL given until one matches one of the validation strings / urls
        /// </summary>
        /// <param name="WebDriver"></param>
        /// <param name="banTime">Ban time for the link after a successfull lead</param>
        public static bool NavigateToFirstValidURL(this IWebDriver WebDriver, Campaign campaign, string campaignName, int banTimeOnSuccess = 0, bool emailCampaign = true, [CallerMemberName] string callerName = "")
        {
            bool validUrlFound = false;
            //Link[] links = campaign.LinkList.ToArray();
            Link[] links = campaign.GetCombinedLinkList().ToArray();
            try
            {
                string[] validationStrings = Settings.validationStrings[campaignName];
                string usedURL = "";
                foreach (Link link in links)
                {
                    if (!link.IsActive || link.IsBlocked)
                    {
                        continue;
                    }

                    // IP Range Ban Check
                    bool rangeBanned = false;
                    foreach (string ipBan in Settings.ipRangeBans)
                    {
                        if (ipBan.Split(';')[0] == link.LinkID)
                        {
                            if (Settings.currentIP.Contains(ipBan.Split(';')[1]))
                            {
                                rangeBanned = true;
                                break;
                            }
                        }
                    }

                    if (rangeBanned)
                    {
                        continue;
                    }

                    // Bei Veuro Links Mail prüfen
                    if (link.LinkURL.Contains("veuro.de") && !Misc.CheckVeuroConditions() && emailCampaign)
                    {
                        continue;
                    }

                    // Arabona check
                    if (link.LinkURL.ToLower().Contains("arabona") || link.LinkURL.ToLower().Contains("melunia") || link.LinkURL.ToLower().Contains("lawida"))
                    {
                        // Wegen ban
                        if (!Settings.debugMode && (Settings.IsAdminRound || Settings.userName.ToLower().Contains("jones")))
                        {
                            continue;
                        }

                        // Regulär:
                        if (Settings.IsAdminRound && Settings.userName.ToLower().Contains("marklos"))
                        {
                            continue;
                        }
                    }

                    // 1. Versuch
                    try
                    {
                        if (link.Provider.Contains("WALL_"))
                        {
                            OfferWallHelper.PrepareOfferWallLink(link, WebDriver);
                        }
                        else
                        {
                            WebDriver.Navigate().GoToUrl(link.LinkURL);
                        }
                    }
                    catch (Exception ex)
                    {
                        continue;
                    }

                    foreach (string validationString in validationStrings)
                    {
                        if (validationString.Contains("http"))
                        {
                            if (WebDriver.Url.Contains(validationString))
                            {
                                validUrlFound = true;
                                break;
                            }
                        }
                        else if (WebDriver.PageSource.Contains(validationString))
                        {
                            validUrlFound = true;
                            break;
                        }
                    }
                    if (validUrlFound) // Link funktioniert
                    {
                        link.BanLink(banTimeOnSuccess);
                        link.LinkUsed(); // linkUsed NACH BanLink falls Limit überschritten wird und Link bis zum nächsten Tag gebannt werden soll
                        usedURL = link.LinkURL;
                        // LastUsedLink eintragen
                        campaign.LastUsedLink = link;
                        break;
                    }
                    else // Link funktioniert nicht
                    {
                        System.Threading.Thread.Sleep(1000);
                        // Gucken ob gewartet werden muss
                        foreach (string waitString in Settings.providerMessageWait)
                        {
                            if (WebDriver.Url.Contains(waitString.Split(';')[0]) && WebDriver.PageSource.Contains(waitString.Split(';')[1]))
                            {
                                System.Threading.Thread.Sleep(waitString.Split(';')[2].ToInt());
                                break;
                            }
                        }

                        // Gucken ob Daten ausgefüllt werden müssen
                        // Datenchecks
                        if (emailCampaign)
                        {
                            CheckVeuroPreEnterData(WebDriver, link);
                        }
                        CheckBonicloudPreEnterData(WebDriver, link);

                        // Gucken ob man auf weiter klicken muss
                        foreach (string clickString in Settings.providerMessageClicks)
                        {
                            if (WebDriver.Url.Contains(clickString.Split(';')[0]))
                            {
                                try
                                {
                                    IJavaScriptExecutor executor = (IJavaScriptExecutor)WebDriver;
                                    executor.ExecuteScript("arguments[0].click();", WebDriver.FindElement(By.XPath(clickString.Split(';')[1])));
                                    System.Threading.Thread.Sleep(2000);
                                }
                                catch (Exception)
                                {
                                }
                                break;
                            }
                        }

                        // Gucken ob Frame gewechselt werden muss
                        foreach (string frameString in Settings.providerMessageFrames)
                        {
                            try
                            {
                                if (WebDriver.Url.Contains(frameString.Split(';')[0]))
                                {
                                    if (WebDriver.PageSource.Contains(frameString.Split(';')[1]))
                                    {
                                        WebDriver.SwitchToFrame(By.XPath(frameString.Split(';')[2]));
                                        break;
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                WebDriver.SwitchToDefaultFrame();
                            }
                        }
                        System.Threading.Thread.Sleep(4000);

                        // Gucken ob gewartet werden muss
                        foreach (string waitString in Settings.providerMessageWait)
                        {
                            if (WebDriver.Url.Contains(waitString.Split(';')[0]) && WebDriver.PageSource.Contains(waitString.Split(';')[1]))
                            {
                                System.Threading.Thread.Sleep(waitString.Split(';')[2].ToInt());
                                break;
                            }
                        }

                        foreach (string validationString in validationStrings)
                        {
                            if (validationString.Contains("http"))
                            {
                                if (WebDriver.Url.Contains(validationString))
                                {
                                    validUrlFound = true;
                                    break;
                                }
                            }
                            else if (WebDriver.PageSource.Contains(validationString))
                            {
                                validUrlFound = true;
                                break;
                            }
                        }
                        if (validUrlFound) // Link funktioniert
                        {
                            link.BanLink(banTimeOnSuccess);
                            link.LinkUsed(); // linkUsed NACH BanLink falls Limit überschritten wird und Link bis zum nächsten Tag gebannt werden soll
                            usedURL = link.LinkURL;
                            // LastUsedLink eintragen
                            campaign.LastUsedLink = link;
                            break;
                        }

                        bool providerMessageFound = false;
                        foreach (string providerMessage in Settings.providerMessages)
                        {
                            if (providerMessage.Contains("##") || providerMessage.Length < 5)
                            {
                                continue;
                            }

                            if (providerMessage.Contains("http") && WebDriver.Url.Contains(providerMessage.Split(';')[0]) ||
                                !providerMessage.Contains("http") && WebDriver.PageSource.Contains(providerMessage.Split(';')[0]))
                            {
                                string banTime = providerMessage.Split(';')[1];
                                providerMessageFound = true;
                                if (banTime == "0")
                                {
                                    break;
                                }
                                if (banTime.Contains("x"))
                                {
                                    link.BanLinkUntilNextDay(banTime.Replace("x", "").ToInt());
                                }
                                else
                                {
                                    link.BanLink(banTime.ToInt());
                                }
                                Log.WriteToLog("Link " + link.LinkURL + " banned for " + providerMessage.Split(';')[1] + " minutes due to provider message " + providerMessage.Split(';')[0], 2, true);
                                break;
                            }
                        }

                        if (!providerMessageFound)
                        {
                            link.BanLink(20);
                            File.AppendAllText(System.Windows.Forms.Application.StartupPath + "\\Logs\\unknownErrorSites.txt", link.LinkURL + ";" + WebDriver.Url + Environment.NewLine, Encoding.Default);

                            Log.WriteToLog("Unknown Provider Message Found. Logged in unknownErrorSites.txt", 5);
                        }
                    }
                }

                // URL-Logging
                if (validUrlFound)
                {
                    try
                    {
                        File.AppendAllText(Settings.startupPath + "\\Logs\\links.txt", campaignName + " - " + usedURL + Environment.NewLine);
                    }
                    catch (Exception)
                    {
                    }
                }

            }
            catch (Exception ex)
            {

            }
            return validUrlFound;
        }

        private static void ReportError(string originalCaller, Exception exception, string additionalInfo = "", [CallerMemberName] string callerName = "")
        {
            string extraText = "";

            if (additionalInfo.Length > 0)
            {
                extraText = Environment.NewLine + "Info: " + additionalInfo;
            }
            Log.LogPaid4("Error at " + callerName + ": " + Environment.NewLine + exception.Message + Environment.NewLine + "Called by " + originalCaller + extraText, 3);
        }
    }
}
