using HiveAPI.CS;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Remoting;
using System.Text;
using System.Threading.Tasks;

namespace Ultimate_Splinterlands_Bot_V2.Classes
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
        public static CookieContainer CookieContainer = new();
        public static string StartupPath = "";

        public static bool LegacyWindowsMode = false;
        public static bool DebugMode = false;
        public static bool WriteLogToFile = false;
        public static bool ShowAPIResponse = true;

        public static bool LightningMode = false;
        public static bool ShowBattleResults = true;
        public static int Threads = 1;

        public static bool BrowserMode = false;
        public static bool ChromeNoSandbox = false;
        public static bool Headless = false;
        public static string ChromeBinaryPath = "";
        public static string ChromeDriverPath = "";
        public static int MaxBrowserInstances = 2;

        public static bool UseAPI = true;
        public static string PublicAPIUrl = "";
        public static bool UsePrivateAPI = false;
        public static string PrivateAPIUrl = "";
        public static string PrivateAPIShop= "";
        public static string PrivateAPIUsername= "";
        public static string PrivateAPIPassword= "";

        public static bool PrioritizeQuest = true;
        public static bool ClaimQuestReward = false;
        public static bool ClaimSeasonReward = false;
        public static bool DontClaimQuestNearHigherLeague = false;
        public static bool IgnoreMissingCPAtQuestClaim = false;
        public static bool AdvanceLeague = false;
        public static int SleepBetweenBattles = 30;
        public static int ECRThreshold = 75;
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

        public readonly static HttpClient _httpClient = new HttpClient();
        public static CHived oHived;

        public static JArray CardsDetails;
        public static Dictionary<string, string> QuestTypes;
        public static Dictionary<string, string> Summoners;
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
    }
}
