using HiveAPI.CS;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting;
using System.Text;
using System.Threading.Tasks;
using Pastel;
using System.Threading;
using System.Drawing;
using Ultimate_Splinterlands_Bot_V2.Classes.Bot;
using Ultimate_Splinterlands_Bot_V2.Classes.Http;
using Ultimate_Splinterlands_Bot_V2.Classes.Utils;

namespace Ultimate_Splinterlands_Bot_V2.Classes.Config
{
    public static class Settings
    {
        public const string HIVE_NODE = "https://api.deathwing.me/";
        public const string SPLINTERLANDS_API_URL = "https://api2.splinterlands.com";
        public const string SPLINTERLANDS_API_URL_FALLBACK = "https://game-api.splinterlands.io";
        public const string SPLINTERLANDS_BROADCAST_URL = "https://broadcast.splinterlands.com/send";
        public const string SPLINTERLANDS_WEBSOCKET_URL = "wss://ws2.splinterlands.com/";
        public const string SPLINTERLANDS_APP = "splinterlands/0.7.139";
        public static char[] Subset = "0123456789abcdefghijklmnopqrstuvwxyz".ToCharArray();
        public static Random _Random = new Random();
        public static string StartupPath = "";
        public static bool DisableConsoleColors = false;

        public static bool LegacyWindowsMode = false;
        public static bool DebugMode = false;
        public static bool WriteLogToFile = false;
        public static bool ShowApiResponse = true;

        public static bool UseLightningMode = false;
        public static bool ShowBattleResults = true;
        public static int Threads = 1;

        public static bool UseBrowserMode = false;
        public static bool ChromeNoSandbox = false;
        public static bool Headless = false;
        public static string ChromeBinaryPath = "";
        public static string ChromeDriverPath = "";
        public static int MaxBrowserInstances = 2;

        public static bool UseApi = true;
        public static string PublicApiUrl = "";
        public static bool UsePrivateApi = false;
        public static string PrivateApiUrl = "";
        public static string PrivateApiShop= "";
        public static string PrivateAPIUsername= "";
        public static string PrivateAPIPassword= "";

        public static bool PrioritizeQuest = true;
        public static bool ClaimQuestReward = false;
        public static bool ClaimSeasonReward = false;
        public static bool DontClaimQuestNearHigherLeague = false;
        public static bool WaitForMissingCpAtQuestClaim = false;
        public static bool AdvanceLeague = false;
        public static int SleepBetweenBattles = 30;
        public static int EcrThreshold = 75;
        public static string RequestNewQuest = "";
        public static string[] BadQuests = Array.Empty<string>();

        public static string RentalBotDllPath = "";
        public static bool RentalBotActivated = false;
        public static int DaysToRent = 0;
        public static int DesiredRentalPower = 0;
        public static decimal MaxRentalPricePer500 = 0;
        public static ObjectHandle RentalBot = null;
        public static MethodInfo RentalBotMethodCheckRentals = null;
        public static MethodInfo RentalBotMethodIsAvailable = null;
        public static MethodInfo RentalBotMethodSetActive = null;

        public static bool RateLimited = false;
        public static object RateLimitedLock = new object();
        public static List<BotInstanceBrowser> BotInstancesBrowser { get; set; }
        public static List<BotInstanceBlockchain> BotInstancesBlockchain { get; set; }
        public static List<(IWebDriver driver, bool isAvailable)> SeleniumInstances { get; set; }
        public static List<(int index, string account, string battleResult, string rating, string ECR, string questStatus)> LogSummaryList { get; set; }

        public static CHived oHived;

        public static JArray CardsDetails;
       
        //TODO: leave in settings?
        public static readonly string[] PhantomCards = { "1","2","3","4","5","6","7","8","12","13","14","15","16","17","18","19","23","24","25","26","27","28","29","30","34","35","36","37","38","39","40","41","42","45","46","47","48","49","50","51","52","60","61","62","63","64","65","66","79","157","158","159","160","161","162","163","167","168","169","170","171","172","173","174","178","179","180","181","182","183","184","185","189","140","141","145","146","147","148","149","150","151","152","156","135","136","137","138","139","140","141","145","185","189","224","190","191","192","193","194","195","196","" };

        public const string JavaScriptClickFunction = @"function simulate(element, eventName)
{
    var options = extend(defaultOptions, arguments[2] || {});
    var oEvent, eventType = null;

    for (var name in eventMatchers)
    {
        if (eventMatchers[name].test(eventName)) { eventType = name; break; }
    }

    if (!eventType)
        throw new SyntaxError('Only HTMLEvents and MouseEvents interfaces are supported');

    if (document.createEvent)
    {
        oEvent = document.createEvent(eventType);
        if (eventType == 'HTMLEvents')
        {
            oEvent.initEvent(eventName, options.bubbles, options.cancelable);
        }
        else
        {
            oEvent.initMouseEvent(eventName, options.bubbles, options.cancelable, document.defaultView,
            options.button, options.pointerX, options.pointerY, options.pointerX, options.pointerY,
            options.ctrlKey, options.altKey, options.shiftKey, options.metaKey, options.button, element);
        }
        element.dispatchEvent(oEvent);
    }
    else
    {
        options.clientX = options.pointerX;
        options.clientY = options.pointerY;
        var evt = document.createEventObject();
        oEvent = extend(evt, options);
        element.fireEvent('on' + eventName, oEvent);
    }
    return element;
}

function extend(destination, source) {
    for (var property in source)
      destination[property] = source[property];
    return destination;
}

var eventMatchers = {
    'HTMLEvents': /^(?:load|unload|abort|error|select|change|submit|reset|focus|blur|resize|scroll)$/,
    'MouseEvents': /^(?:click|dblclick|mouse(?:down|up|over|move|out))$/
}
var defaultOptions = {
    pointerX: 0,
    pointerY: 0,
    button: 0,
    ctrlKey: false,
    altKey: false,
    shiftKey: false,
    metaKey: false,
    bubbles: true,
    cancelable: true
}";

        private static readonly Dictionary<string, string> SettingsMapping = new Dictionary<string, string>()
        {
            { "ERC_THRESHOLD", "EcrThreshold" },
            { "API_URL", "PublicApiUrl" },
            { "DEBUG", "DebugMode" },
        };

        private static void parseSetting(string setting)
        {
            string[] parts = setting.Split('=');
            if (parts.Length != 2 || setting[0] == '#')
            {
                return;
            }

            string settingsKey = parts[0];
            string settingsName = "";

            if (SettingsMapping.ContainsKey(settingsKey))
            {
                settingsName = SettingsMapping[settingsKey];
            }
            else 
            {
                string[] keyParts = settingsKey.Split('_');
                settingsName = String.Join("", keyParts.Select(part => Helper.capitalize(part)).ToArray());
            }

            string value = parts[1];

            Type type = typeof(Settings);
            FieldInfo field = type.GetField(settingsName, BindingFlags.Public | BindingFlags.Static);
            
            if (field == null) {
                Console.WriteLine($"could not find setting for: '{parts[0]}'");
                return;
            }

            if (field.FieldType == typeof(bool)) {
                field.SetValue(null, Boolean.Parse(value));
            }
            else if (field.FieldType == typeof(string)) 
            {
                field.SetValue(null, value);
            }
            else if (field.FieldType == typeof(Int32)) 
            {   
                field.SetValue(null, Convert.ToInt32(value));
            }
            else
            {
                Console.WriteLine("Field Value: '{0}'", field.GetValue(null));
                Console.WriteLine("Field Type: {0}", field.FieldType);
                Log.WriteToLog($"UNKOWN type '{field.FieldType}'");
            }           
        }

        public static bool Initialize()
        {
            // Setup startup path
            string path = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string directory = System.IO.Path.GetDirectoryName(path);
            Settings.StartupPath = directory;

            string configFile = Settings.StartupPath + @"/config/config.txt";
            
            if (!File.Exists(configFile))
            {
                Log.WriteToLog("No config.txt in config folder - see config-example.txt!", Log.LogType.CriticalError);
                return false;
            }

            Log.WriteToLog("Reading config...");
            foreach (string setting in File.ReadAllLines(configFile))
            {
                parseSetting(setting);

                // string[] temp = setting.Split('=');
                // if (temp.Length != 2 || setting[0] == '#')
                // {
                //     continue;
                // }

                // switch (temp[0])
                // {
                    //TODO: RENT?
                    // case "RENTAL_BOT":
                    //     if (Boolean.Parse(temp[1]))
                    //     {
                    //         var moduleInstance = Activator.CreateInstanceFrom(Settings.RentalBotDllPath, "Splinterlands_Rental_Bot.RentalBot");
                    //         Settings.RentalBot = moduleInstance;
                    //         MethodInfo mi = moduleInstance.Unwrap().GetType().GetMethod("Setup");
                            
                    //         mi.Invoke(moduleInstance.Unwrap(), new object[] { HttpClient.getInstance(), false });
                    //         Settings.RentalBotMethodCheckRentals = moduleInstance.Unwrap().GetType().GetMethod("CheckRentals");
                    //         Settings.RentalBotMethodIsAvailable = moduleInstance.Unwrap().GetType().GetMethod("IsAvailable");
                    //         Settings.RentalBotMethodSetActive = moduleInstance.Unwrap().GetType().GetMethod("SetActive");
                    //         Settings.RentalBotActivated = true;
                    //     }
                    //     break;
                    // case "RENT_DAYS":
                    //     Settings.DaysToRent = Convert.ToInt32(temp[1]);
                    //     break;
                    // case "RENT_POWER":
                    //     Settings.DesiredRentalPower = Convert.ToInt32(temp[1]);
                    //     break;
                    // case "RENT_MAX_PRICE_PER_500":
                    //     Settings.MaxRentalPricePer500 = Convert.ToDecimal(temp[1], System.Globalization.CultureInfo.InvariantCulture);
                    //     break;
                //     default:
                //         break;
                // }
            }
            
            if (Settings.UsePrivateApi)
            {
                string[] loginData = File.ReadAllText(Settings.StartupPath + @"/config/login.txt").Split(':');
                Settings.PrivateAPIUsername = loginData[0];
                Settings.PrivateAPIPassword = loginData[1];
            }

            if (Settings.RequestNewQuest != "") {
                Settings.BadQuests = RequestNewQuest.Split(',');
            }

            if (Settings.DisableConsoleColors) {
                Log.WriteToLog("Console colors disabled!");
                ConsoleExtensions.Disable();
            }

            if (Settings.UseBrowserMode == Settings.UseLightningMode)
            {
                Log.WriteToLog("Please set either USE_LIGHTNING_MODE OR USE_BROWSER_MODE to true (not both) - see updated config-example.txt!", Log.LogType.CriticalError);
                return false;
            }

            Log.WriteToLog("Config loaded!", Log.LogType.Success);
            Log.WriteToLog($"Config parameters:{Environment.NewLine}" +
                $"MODE: {(Settings.UseLightningMode ? "LIGHTNING (blockchain)" : "BROWSER")}{Environment.NewLine}" +
                $"DEBUG: {Settings.DebugMode}{Environment.NewLine}" +
                $"WRITE_LOG_TO_FILE: {Settings.WriteLogToFile}{Environment.NewLine}" +
                $"SHOW_API_RESPONSE: {Settings.ShowApiResponse}{Environment.NewLine}" +
                $"PRIORITIZE_QUEST: {Settings.PrioritizeQuest}{Environment.NewLine}" +
                $"CLAIM_QUEST_REWARD: {Settings.ClaimQuestReward}{Environment.NewLine}" +
                $"CLAIM_SEASON_REWARD: {Settings.ClaimSeasonReward}{Environment.NewLine}" +
                $"REQUEST_NEW_QUEST: {String.Join(",", Settings.BadQuests)}{Environment.NewLine}" +
                $"DONT_CLAIM_QUEST_NEAR_HIGHER_LEAGUE: {Settings.DontClaimQuestNearHigherLeague}{Environment.NewLine}" +
                $"WAIT_FOR_MISSING_CP_AT_QUEST_CLAIM: {Settings.WaitForMissingCpAtQuestClaim}{Environment.NewLine}" +
                $"ADVANCE_LEAGUE: {Settings.AdvanceLeague}{Environment.NewLine}" +
                $"SLEEP_BETWEEN_BATTLES: {Settings.SleepBetweenBattles}{Environment.NewLine}" +
                $"ECR_THRESHOLD: {Settings.EcrThreshold}{Environment.NewLine}" +
                $"USE_API: {Settings.UseApi}{Environment.NewLine}" +
                $"USE_PRIVATE_API: {Settings.UsePrivateApi}");
            
            Settings.CardsDetails = Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(Settings.StartupPath + @"/data/cardsDetails.json"));

            Settings.LogSummaryList = new List<(int index, string account, string battleResult, string rating, string ECR, string questStatus)>();

            return true;
        }

    }
}
