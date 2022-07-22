using HiveAPI.CS;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Remoting;
using System.Text;
using System.Threading.Tasks;
using Ultimate_Splinterlands_Bot_V2.Bot;

namespace Ultimate_Splinterlands_Bot_V2.Config
{
    public static class Settings
    {
        public const string HIVE_NODE = "https://api.deathwing.me/";
        public const string SPLINTERLANDS_API_URL = "https://api2.splinterlands.com";
        public const string SPLINTERLANDS_API_URL_FALLBACK = "https://game-api.splinterlands.io";
        public const string SPLINTERLANDS_BROADCAST_URL = "https://broadcast.splinterlands.com/send";
        public const string SPLINTERLANDS_WEBSOCKET_URL = "wss://ws2.splinterlands.com/";
        public const string SPLINTERLANDS_APP = "usb/1.0";
        public const string BOT_GITHUB_REPO = "Sir-Void/Ultimate-Splinterlands-Bot-V2";
        public static char[] CharSubset = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
        public static Random _Random = new();
        public static CookieContainer CookieContainer = new();
        public static string StartupPath = "";

        public static bool AutoUpdate = false;
        public static bool LegacyWindowsMode = false;
        public static bool DebugMode = false;
        public static bool WriteLogToFile = false;
        public static bool ShowAPIResponse = true;

        public static CardSettings CardSettings;
        public static TeamSettings TeamSettings;

        public static double InstanceWin = 0;
        public static double InstanceDraw = 0;
        public static double InstanceLose = 0;
        public static bool ReportGameResult = false;

        public static bool ShowBattleResults = true;
        public static int Threads = 1;

        public static bool UseAPI = true;
        public static string PublicAPIUrl = "";
        public static string FallBackPublicAPIUrl = "";
        public static bool UsePrivateAPI = false;
        public static string PrivateAPIUrl = "";
        public static string PrivateAPIShop = "";
        public static string PrivateAPIUsername = "";
        public static string PrivateAPIPassword = "";
        public static bool PowerTransferBot = false;
        public static Dictionary<string, BotInstance> PlannedPowerTransfers = new();
        public static Queue<BotInstance> AvailablePowerTransfers;
        public static object PowerTransferBotLock = new();

        public static string RankedFormat = "WILD";
        public static bool PrioritizeQuest = true;
        public static bool ClaimQuestReward = false;
        public static bool ClaimSeasonReward = false;
        public static bool AdvanceLeague = false;
        public static int MaxLeagueTier = 4;
        public static int SleepBetweenBattles = 30;
        public static int StartBattleAboveECR = 0;
        public static int StopBattleBelowECR = 75;
        public static int MinimumBattlePower = 0;
        public static string[] BadQuests = Array.Empty<string>();

        public static bool RateLimited = false;
        public static object RateLimitedLock = new();
        public static List<BotInstance> BotInstances { get; set; }
        public static List<(int index, string account, string battleResult, string rating, string ECR, string questStatus)> LogSummaryList { get; set; }

        public static readonly HttpClient _httpClient = new();
        public static CHived oHived;

        public static JArray CardsDetails;
        public static Dictionary<string, string> QuestTypes;
        public static Dictionary<string, string> Summoners;
        public static readonly string[] PhantomCards = { "157", "158", "159", "160", "395", "396", "397", "398", "399", "161", "162", "163", "167", "400", "401", "402", "403", "440", "441", "168", "169", "170", "171", "381", "382", "383", "384", "385", "172", "173", "174", "178", "386", "387", "388", "389", "437", "179", "180", "181", "182", "367", "368", "369", "370", "371", "183", "184", "185", "189", "372", "373", "374", "375", "439", "146", "147", "148", "149", "409", "410", "411", "412", "413", "150", "151", "152", "156", "414", "415", "416", "417", "135", "136", "137", "138", "353", "354", "355", "356", "357", "139", "140", "141", "145", "358", "359", "360", "361", "438", "224", "190", "191", "192", "157", "423", "424", "425", "426", "194", "195", "196", "427", "428", "429", "" };
    }
}