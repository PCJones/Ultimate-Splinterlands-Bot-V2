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
using System.Runtime.InteropServices;

namespace Ultimate_Splinterlands_Bot_V2.Classes
{
    // SeleniumAddons Class by PC Jones
    public static class SeleniumAddons
    {
        public static object WebDriver { get; private set; }

        public static IWebDriver CreateSeleniumInstance(bool fireFox = false, int timeOut = 120, bool disableImages = false, string userAgent = "")
        {
            IWebDriver driver = null;
            while (driver == null)
            {
                try
                {
                    if (fireFox)
                    {
                        FirefoxProfile ffProfile = new();
                        if (userAgent != "")
                        {
                            ffProfile.SetPreference("general.useragent.override", userAgent);
                        }
                        FirefoxOptions options = new()
                        {
                            Profile = ffProfile
                        };

                        FirefoxDriverService service = FirefoxDriverService.CreateDefaultService();
                        service.FirefoxBinaryPath = @"C:\Program Files (x86)\Mozilla Firefox\firefox.exe";
                        TimeSpan timeout = new(0, 0, timeOut);
                        driver = new FirefoxDriver(service, options, timeout);
                    }
                    else
                    {
                        ChromeOptions chromeOptions = new();
                        //chromeOptions.AddArgument("--ignore-certificate-errors");
                        if (Settings.ChromeBinaryPath != "")
                        {
                            chromeOptions.BinaryLocation = Settings.ChromeBinaryPath;
                        }
                        if (userAgent != "")
                        {
                            chromeOptions.AddArgument("user-agent=" + userAgent);
                        }
                        if (Settings.ChromeNoSandbox)
                        {
                            chromeOptions.AddArgument("--no-sandbox");
                        }
                        if (Settings.Headless)
                        {
                            chromeOptions.AddArgument("--headless");
                        }
                        chromeOptions.AddArgument("--disable-web-security");
                        chromeOptions.AddArgument("--disable-features=IsolateOrigins");
                        chromeOptions.AddArgument("--disable-site-isolation-trials");
                        chromeOptions.AddArgument("--mute-audio");
                        chromeOptions.AddArgument("--disable-notifications");
                        if (disableImages)
                        {
                            chromeOptions.AddArgument("--blink-settings=imagesEnabled=false");
                        }
                        //chromeOptions.AddArgument("--window-size=1920,1080");
                        chromeOptions.AddArgument("--window-size=1800,1500");
                        var chromeDriverService = ChromeDriverService.CreateDefaultService(Settings.StartupPath);
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !Settings.DebugMode)
                        {
                            chromeDriverService.HideCommandPromptWindow = true;
                        }
                        driver = new ChromeDriver(chromeDriverService, chromeOptions, TimeSpan.FromSeconds(timeOut));
                    }
                    driver.Manage().Window.Position = new System.Drawing.Point(0, 0);
                    //driver.Manage().Window.Maximize();
                }
                catch (WebDriverException ex)
                {
                    Log.WriteToLog(ex.Message, Log.LogType.CriticalError);
                    System.Threading.Thread.Sleep(5000);
                }
                catch (Exception ex)
                {
                    Log.WriteToLog(ex.Message, Log.LogType.CriticalError);
                    System.Threading.Thread.Sleep(5000);
                }
            }

            return driver;
        }

        /// <summary>
        /// Clicks an element on the current page
        /// </summary>
        /// <param name="by">By</param>
        /// <param name="elementIndex">Index of the element if there are multiple</param>
        public static void ClickElementOnPage(this IWebDriver webDriver, By by, int elementIndex = 0, bool suppressErrors = false, [CallerMemberName] string callerName = "")
        {
            try
            {
                OpenQA.Selenium.Remote.RemoteWebElement remoteElement = (OpenQA.Selenium.Remote.RemoteWebElement)webDriver.FindElements(by)[elementIndex];

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
                if (!suppressErrors)
                {
                    StackFrame frame = new(1);
                    string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                    ReportError(calledMethod, ex, "By: " + by.ToString());
                }
            }
        }

        public static IWebElement GetElementByCoordinates(this IWebDriver webDriver, IWebElement element, [CallerMemberName] string callerName = "")
        {
            IWebElement returnElement = null;
            try
            {
                IJavaScriptExecutor executor = (IJavaScriptExecutor)webDriver;
                executor.ExecuteScript("arguments[0].scrollIntoView();", element);
                var xx = executor.ExecuteScript("return window.pageXOffset;");
                int newX = element.Location.X - Convert.ToInt32(executor.ExecuteScript("return window.pageXOffset;"));
                int newY = element.Location.Y - Convert.ToInt32(executor.ExecuteScript("return window.pageYOffset;"));
                returnElement = (IWebElement)executor.ExecuteScript("return document.elementFromPoint(" + newX + ", " + newY + ")");
            }
            catch (Exception ex)
            {
                StackFrame frame = new(1);
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
        public static void ClickElementOnPage(this IWebDriver webDriver, IWebElement element, [CallerMemberName] string callerName = "")
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
                StackFrame frame = new(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "Element: " + element.ToString());
            }
        }

        /// <summary>
        /// Submits a form
        /// </summary>
        /// <param name="by">By</param>
        /// <param name="elementIndex">Index of the element if there are multiple</param>
        public static void SubmitForm(this IWebDriver webDriver, By by, int elementIndex = 0, [CallerMemberName] string callerName = "")
        {
            try
            {
                webDriver.FindElements(by)[elementIndex].Submit();
            }
            catch (Exception ex)
            {
                StackFrame frame = new(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString());
            }
        }

        /// <summary>
        /// Sets data into textfields.
        /// </summary>
        /// <param name="by">By</param>
        /// <param name="data">Data / Key (ex.: Keys.Tab)</param>
        /// <param name="elementIndex">Index of the element if there are multiple</param>
        public static void SetData(this IWebDriver webDriver, By by, string data, int elementIndex = 0, [CallerMemberName] string callerName = "")
        {
            try
            {
                webDriver.FindElements(by)[elementIndex].Clear();
                webDriver.FindElements(by)[elementIndex].SendKeys(data);
            }
            catch (Exception ex)
            {
                StackFrame frame = new(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString());
            }
        }

        /// <summary>
        /// Waits for a given success message
        /// </summary>
        /// <param name="message">Message to look at</param>
        public static bool WaitForSuccessMessage(this IWebDriver webDriver, string message, int waitingTime = 30, [CallerMemberName] string callerName = "")
        {
            bool returnBool = false;
            try
            {
                // Waite till website contains a give peace of text (max. 30 seconds)
                for (int i = 0; i < waitingTime; i++)
                {
                    try
                    {
                        if (webDriver.PageSource.Contains(message))
                        {
                            returnBool = true;
                            break;
                        }
                        System.Threading.Thread.Sleep(1000);
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
                StackFrame frame = new(1);
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
        public static bool WaitForWebsiteLoadedAndElementShown(this IWebDriver webDriver, By by, int seconds = 15, int elementIndex = 0, [CallerMemberName] string callerName = "")
        {
            if (!WaitForWebsiteLoaded(webDriver, by, seconds / 2, elementIndex, callerName)) return false;
            return WaitForElementShown(webDriver, by, seconds / 2, elementIndex, callerName);
        }

        /// <summary>
        /// Waits till the website is fully loaded by a given time (default = 30 seconds)
        /// </summary>
        /// <param name="by">By</param>
        /// <param name="time">Time to wait (default 30 seconds)</param>
        /// <param name="elementIndex">Index of the element if there are multiple</param>
        //[DebuggerNonUserCode]
        public static bool WaitForWebsiteLoaded(this IWebDriver webDriver, By by, int time = 30, int elementIndex = 0, [CallerMemberName] string callerName = "")
        {
            try
            {
                time = time == 0 ? 1 : time;
                for (int i = 0; i < time; i++)
                {
                    try
                    {
                        IWebElement element = webDriver.FindElements(by)[elementIndex];

                        if (element != null)
                        {
                            return true;
                        }
                        else
                        {
                            System.Threading.Thread.Sleep(1000);
                        }
                    }
                    catch (WebDriverException)
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
                StackFrame frame = new(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString());
            }
            return false;
        }

        /// <summary>
        /// Switches to an frame / iframe
        /// </summary>
        /// <param name="Name">Frame name</param>
        public static void SwitchToFrame(this IWebDriver webDriver, string Name, [CallerMemberName] string callerName = "")
        {
            try
            {
                // Switch to iframe
                webDriver.SwitchTo().Frame(Name);
            }
            catch (Exception ex)
            {
                StackFrame frame = new(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "Frame-Name: " + Name);
            }
        }

        /// <summary>
        /// Switches to an frame / iframe
        /// </summary>
        /// <param name="Index">Frame index</param>
        public static void SwitchToFrame(this IWebDriver webDriver, int Index, [CallerMemberName] string callerName = "")
        {
            try
            {
                // Switch to iframe
                webDriver.SwitchTo().Frame(Index);
            }
            catch (Exception ex)
            {
                StackFrame frame = new(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "Frame-Index: " + Index.ToString());
            }
        }

        /// <summary>
        /// Switches to an frame / iframe
        /// </summary>
        /// <param name="Element">Frame element</param>
        public static void SwitchToFrame(this IWebDriver webDriver, IWebElement Element, [CallerMemberName] string callerName = "")
        {
            try
            {
                // Switch to iframe
                webDriver.SwitchTo().Frame(Element);
            }
            catch (Exception ex)
            {
                StackFrame frame = new(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "Element: " + Element.ToString());
            }
        }

        /// <summary>
        /// Switches to an frame / iframe
        /// </summary>
        /// <param name="by">Frame By</param>
        public static void SwitchToFrame(this IWebDriver webDriver, By by, [CallerMemberName] string callerName = "")
        {
            try
            {
                // Switch to iframe
                webDriver.SwitchTo().Frame(webDriver.FindElement(by));
            }
            catch (Exception ex)
            {
                StackFrame frame = new(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString());
            }
        }

        /// <summary>
        /// Switches to the default frame (main page)
        /// </summary>
        public static void SwitchToDefaultFrame(this IWebDriver webDriver, [CallerMemberName] string callerName = "")
        {
            try
            {
                webDriver.SwitchTo().DefaultContent();
            }
            catch (Exception ex)
            {
                StackFrame frame = new(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex);
            }
        }

        /// <summary>
        /// Gets a list with web elements
        /// </summary>
        /// <param name="by">By</param>
        /// <returns>Returns a list with web elements</returns>
        public static System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> GetWebElements(this IWebDriver webDriver, By by, [CallerMemberName] string callerName = "")
        {
            try
            {
                System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> webElements = webDriver.FindElements(by);
                return webElements;
            }
            catch (Exception ex)
            {
                StackFrame frame = new(1);
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
        /// <param name="attribute">Html attribute</param>
        /// <param name="context">Context</param>
        public static void FindAndClickElement(this IWebDriver WebDriver, By by, string attribute, string context, [CallerMemberName] string callerName = "")
        {
            try
            {
                System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> aElements = WebDriver.FindElements(by);
                foreach (IWebElement element in aElements)
                {
                    string elementAttribute = element.GetAttribute(attribute);

                    if (elementAttribute != null)
                    {
                        if (elementAttribute.Contains(context))
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
                StackFrame frame = new(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString());
            }
        }

        /// <summary>
        /// Clicks all elements on the page
        /// </summary>
        /// <param name="by">By</param>
        public static void ClickAllElements(this IWebDriver webDriver, By by, [CallerMemberName] string callerName = "")
        {
            try
            {
                foreach (IWebElement element in webDriver.FindElements(by))
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
                StackFrame frame = new(1);
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
        public static void SelectDropDownByIndex(this IWebDriver webDriver, By by, int index, int elementIndex = 0, [CallerMemberName] string callerName = "")
        {
            try
            {
                IJavaScriptExecutor executor = (IJavaScriptExecutor)webDriver;
                executor.ExecuteScript("arguments[0].selectedIndex = " + index.ToString() + ";", webDriver.FindElements(by)[elementIndex]);
            }
            catch (Exception ex)
            {
                StackFrame frame = new(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString() + Environment.NewLine + "Index: " + index.ToString() + "elementIndex: " + elementIndex.ToString());
            }
        }

        /// <summary>
        /// Selects a dropdown value by index
        /// </summary>
        /// <param name="webDriver">SeleniumWebDriver</param>
        /// <param name="by">By</param>
        /// <param name="value">Value</param>
        /// <param name="elementIndex">Index of the element if there are multiple</param>
        public static void SelectDropDownByValue(this IWebDriver webDriver, By by, string value, int elementIndex = 0, [CallerMemberName] string callerName = "")
        {
            try
            {
                IJavaScriptExecutor executor = (IJavaScriptExecutor)webDriver;
                executor.ExecuteScript("for(var i=0;i<arguments[0].options.length;i++)arguments[0].options[i].value==" + value + "&&(arguments[0].options[i].selected=!0);", webDriver.FindElements(by)[elementIndex]);
            }
            catch (Exception ex)
            {
                StackFrame frame = new(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString() + Environment.NewLine + "Value: " + value + "elementIndex: " + elementIndex.ToString());
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
        public static bool PageContainsString(this IWebDriver webDriver, string Message, [CallerMemberName] string callerName = "")
        {
            try
            {
                if (webDriver.PageSource.Contains(Message))
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
                StackFrame frame = new(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "Message: " + Message);
            }
            return false;
        }

        /// <summary>
        /// Executes a JavaScript at the form
        /// </summary>
        /// <param name="jsScript">JavaScript to execute</param>
        public static void ExecuteJavaScript(this IWebDriver webDriver, string jsScript, bool suppressErrors = false, [CallerMemberName] string callerName = "")
        {
            try
            {
                IJavaScriptExecutor executor = (IJavaScriptExecutor)webDriver;
                executor.ExecuteScript(jsScript);
            }
            catch (Exception ex)
            {
                if (!suppressErrors)
                {
                    StackFrame frame = new(1);
                    string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                    ReportError(calledMethod, ex, "JS: " + jsScript);
                }
            }
        }


        /// <summary>
        /// Executes a JavaScript at the form
        /// </summary>
        /// <param name="jsScript">JavaScript to execute</param>
        /// <param name="argument">IWebElement for arguments[0]</param>
        public static void ExecuteJavaScript(this IWebDriver webDriver, string jsScript, IWebElement argument, bool suppressErrors = false, [CallerMemberName] string callerName = "")
        {
            try
            {
                IJavaScriptExecutor executor = (IJavaScriptExecutor)webDriver;
                executor.ExecuteScript(jsScript, argument);
            }
            catch (Exception ex)
            {
                if (!suppressErrors)
                {
                    StackFrame frame = new(1);
                    string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                    ReportError(calledMethod, ex, "JS: " + jsScript + Environment.NewLine + "argument: " + argument.ToString());
                }
            }
        }

        /// <summary>
        /// Clicks on an element via JavaScript
        /// </summary>
        /// <param name="id">ID of the lement</param>
        public static void JavaScriptClick(this IWebDriver webDriver, string id, [CallerMemberName] string callerName = "")
        {
            try
            {
                IJavaScriptExecutor executor = (IJavaScriptExecutor)webDriver;
                //executor.ExecuteScript("document.getElementById('" + id + "').click();");
                executor.ExecuteScript(Settings.JavaScriptClickFunction + Environment.NewLine + "simulate(document.getElementById('" + id + "'), 'click');");
            }
            catch (Exception ex)
            {
                StackFrame frame = new(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By.ID: " + id);
            }
        }

        /// <summary>
        /// Clicks on an element via JavaScript
        /// </summary>
        /// <param name="element">Element to click</param>
        public static void JavaScriptClick(this IWebDriver webDriver, IWebElement element, [CallerMemberName] string callerName = "")
        {
            try
            {
                IJavaScriptExecutor executor = (IJavaScriptExecutor)webDriver;
                //executor.ExecuteScript("arguments[0].click();", element);
                ///executor.ExecuteScript(Properties.Settings.Default.simulateClickFunctions);
                executor.ExecuteScript(Settings.JavaScriptClickFunction + Environment.NewLine + "simulate(arguments[0], 'click');", element);
            }
            catch (Exception ex)
            {
                StackFrame frame = new(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "Element: " + element.ToString());
            }
        }

        /// <summary>
        /// Clicks on an element via JavaScript
        /// </summary>
        /// <param name="by">By</param>
        public static void JavaScriptClick(this IWebDriver webDriver, By by, int elementIndex = 0, [CallerMemberName] string callerName = "")
        {
            try
            {
                IJavaScriptExecutor executor = (IJavaScriptExecutor)webDriver;
                //executor.ExecuteScript("arguments[0].click();", WebDriver.FindElements(by)[elementIndex]);
                executor.ExecuteScript(Settings.JavaScriptClickFunction + Environment.NewLine + "simulate(arguments[0], 'click');", webDriver.FindElements(by)[elementIndex]);
            }
            catch (Exception ex)
            {
                StackFrame frame = new(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString());
            }
        }

        /// <summary>
        /// Clicks on an element via 
        /// API
        /// </summary>
        /// <param name="Element">Element to click</param>
        public static void ActionClick(this IWebDriver webDriver, IWebElement Element, [CallerMemberName] string callerName = "")
        {
            try
            {
                ExecuteJavaScript(webDriver, "arguments[0].scrollIntoView();", Element);
                OpenQA.Selenium.Interactions.Actions actions = new(webDriver);
                actions.MoveToElement(Element).ClickAndHold(Element).Release().Build().Perform();
            }
            catch (Exception ex)
            {
                StackFrame frame = new(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "Element: " + Element.ToString());
            }
        }

        /// <summary>
        /// Clicks on an element via Actions API
        /// </summary>
        /// <param name="by">By</param>
        public static void ActionClick(this IWebDriver webDriver, By by, int index = 0, [CallerMemberName] string callerName = "")
        {
            try
            {
                IWebElement element = webDriver.FindElements(by)[index];
                OpenQA.Selenium.Interactions.Actions actions = new(webDriver);
                actions.MoveToElement(element).ClickAndHold(element).Release().Build().Perform();
            }
            catch (Exception ex)
            {
                StackFrame frame = new(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString());
            }
        }

        /// <summary>
        /// Clicks on an element via Actions API
        /// </summary>
        /// <param name="by">By</param>
        public static void HoverOverElement(this IWebDriver webDriver, By by, int index = 0, [CallerMemberName] string callerName = "")
        {
            try
            {
                IWebElement element = webDriver.FindElements(by)[index];
                OpenQA.Selenium.Interactions.Actions actions = new(webDriver);
                actions.MoveToElement(element).Build().Perform();
            }
            catch (Exception ex)
            {
                StackFrame frame = new(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString());
            }
        }

        public static void OpenNewTab(this IWebDriver webDriver, [CallerMemberName] string callerName = "")
        {
            try
            {
                webDriver.ExecuteJavaScript("window.open('');");
            }
            catch (Exception ex)
            {
                StackFrame frame = new(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "");
            }
        }

        /// <summary>
        /// Showes a hidden element
        /// </summary>
        /// <param name="elementID">HTML-ID of the element</param>
        public static void ShowElementById(this IWebDriver webDriver, string elementID, [CallerMemberName] string callerName = "")
        {
            try
            {
                IJavaScriptExecutor executor = (IJavaScriptExecutor)webDriver;
                executor.ExecuteScript("document.getElementById('" + elementID + "').style.display='block';");
                if (webDriver.FindElement(By.Id(elementID)).GetAttribute("type") == "hidden")
                {
                    executor.ExecuteScript("document.getElementById('" + elementID + "').setAttribute('type', '');");
                }
            }
            catch (Exception ex)
            {
                StackFrame frame = new(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By.ID: " + elementID);
            }
        }

        /// <summary>
        /// Showes a hidden element
        /// </summary>
        /// <param name="elementID">HTML-ClassName of the element</param>
        /// <param name="elementIndex">Index of the element if there are multiple</param>
        public static void ShowElementByClassName(this IWebDriver webDriver, string elementID, int elementIndex = 0, [CallerMemberName] string callerName = "")
        {
            try
            {
                IJavaScriptExecutor executor = (IJavaScriptExecutor)webDriver;
                executor.ExecuteScript("document.getElementsByClassName('" + elementID + "')[" + elementIndex + "].style.display='block';");
                if (webDriver.FindElement(By.CssSelector("[class='" + elementID + "']")).GetAttribute("type") == "hidden")
                {
                    executor.ExecuteScript("documentgetElementsByClassName('" + elementID + "')[" + elementIndex + "].setAttribute('type', '');");
                }
            }
            catch (Exception ex)
            {
                StackFrame frame = new(1);
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
        public static bool WaitForElementShown(this IWebDriver webDriver, By by, int seconds = 15, int elementIndex = 0, [CallerMemberName] string callerName = "")
        {
            try
            {
                seconds = seconds == 0 ? 1 : seconds;
                for (int i = 0; i < seconds; i++)
                {
                    //if (WebDriver.FindElements(by)[elementIndex].GetAttribute("type") == "hidden")
                    if (webDriver.FindElements(by)[elementIndex].Displayed == false)
                    {
                        System.Threading.Thread.Sleep(1000);
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                StackFrame frame = new(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString() + Environment.NewLine + "Element-Index: " + elementIndex.ToString());
            }
            return false;
        }

        /// <summary>
        /// Sets data into textfields while simulating typing
        /// </summary>
        /// <param name="by">By</param>
        /// <param name="data">Data / Key (ex.: Keys.Tab)</param>
        /// <param name="elementIndex">Index of the element if there are multiple</param>
        public static void SimulateTyping(this IWebDriver webDriver, By by, string data, int elementIndex = 0, bool sendTabAtEnd = true, bool clearElement = true, int minWait = 95, int maxWait = 200, [CallerMemberName] string callerName = "")
        {
            try
            {
                if (clearElement)
                {
                    webDriver.FindElements(by)[elementIndex].Clear();
                }
                Random rnd = new();
                if (maxWait == 0)
                {
                    webDriver.FindElements(by)[elementIndex].SendKeys(data);
                }
                else
                {
                    foreach (char buchstabe in data)
                    {
                        webDriver.FindElements(by)[elementIndex].SendKeys(buchstabe.ToString());
                        System.Threading.Thread.Sleep(rnd.Next(minWait, maxWait));
                    }
                }

                if (sendTabAtEnd)
                {
                    webDriver.FindElements(by)[elementIndex].SendKeys(Keys.Tab);
                    System.Threading.Thread.Sleep(rnd.Next(minWait, maxWait));
                }
            }
            catch (Exception ex)
            {
                StackFrame frame = new(1);
                string calledMethod = frame.GetMethod().DeclaringType.Name + "." + callerName;
                ReportError(calledMethod, ex, "By: " + by.ToString() + Environment.NewLine + "Element-Index: " + elementIndex.ToString());
            }
        }

        private static void ReportError(string originalCaller, Exception exception, string additionalInfo = "", [CallerMemberName] string callerName = "")
        {
            string extraText = "";

            if (additionalInfo.Length > 0)
            {
                extraText = Environment.NewLine + "Info: " + additionalInfo;
            }
            Log.WriteToLog("Error at " + callerName + ": " + Environment.NewLine + exception.Message + Environment.NewLine + "Called by " + originalCaller + extraText, Log.LogType.Error);
        }
    }
}
