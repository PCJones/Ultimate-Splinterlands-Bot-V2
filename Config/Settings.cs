#pragma warning disable CA2211
using HiveAPI.CS;
using Splinterlands_Battle_REST_API.Model;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Ultimate_Splinterlands_Bot_V2.Bot;
using Ultimate_Splinterlands_Bot_V2.Model;

namespace Ultimate_Splinterlands_Bot_V2.Config
{
    public static class Settings
    {
        //public const string HIVE_NODE = "https://api.deathwing.me/";
        public static string HIVE_NODE = "https://api.hive.blog/";
        public const string SPLINTERLANDS_API_URL = "https://api2.splinterlands.com";
        public const string SPLINTERLANDS_API_URL_FALLBACK = "https://api.splinterlands.com";
        public const string SPLINTERLANDS_BROADCAST_URL = "https://broadcast.splinterlands.com/send";
        public const string SPLINTERLANDS_WEBSOCKET_URL = "wss://ws2.splinterlands.com/";
        public const string SPLINTERLANDS_APP = "usb/1.0";
        public const string BOT_GITHUB_REPO = "PCJones/Ultimate-Splinterlands-Bot-V2";
        public static char[] CharSubset = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
        public static Random _Random = new();
        public static CookieContainer CookieContainer = new();
        public static string StartupPath = "";
        public static DateTime LastSerialization = DateTime.Now;

        public static bool AutoUpdate = false;
        public static bool LegacyWindowsMode = false;
        public static bool DebugMode = false;
        public static bool WriteLogToFile = false;
        public static bool ShowAPIResponse = true;

        public static CardSettings CardSettings;
        public static SplinterlandsSettings SplinterlandsSettings;

        public static bool ShowBattleResults = true;
        public static int Threads = 1;

        public static bool UseAPI = true;
        public static string PublicAPIUrl = "";
        public static bool UsePrivateAPI = false;
        public static string PrivateAPIUrl = "";
        public static string PrivateAPIShop= "";
        public static string PrivateAPIUsername= "";
        public static string PrivateAPIPassword= "";
        public static bool PowerTransferBot = false;
        public static Dictionary<string, BotInstance> PlannedPowerTransfers = new();
        public static Queue<BotInstance> AvailablePowerTransfers;
        public static object PowerTransferBotLock = new();

        public static string RankedFormat = "WILD";
        public static bool PrioritizeQuest = true;
        public static bool ClaimQuestReward = false;
        public static bool ClaimSeasonReward = false;
        public static bool ShowSpsReward = false;
        public static bool FocusChestOptimization = false;
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

        public static HttpClient HttpClient;
        public static CHived oHived;

        public static readonly string[] STARTER_EDITIONS = new string[] { "4", "7" };
        public static DetailedCard[] CardsDetails;
        public static UserCard[] StarterCards;
        public static Dictionary<string, string> QuestTypes;
    }
}
#pragma warning restore CA2211