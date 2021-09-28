using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ultimate_Splinterlands_Bot_V2.Classes
{
    public static class Settings
    {         
        public static string StartupPath = "";
        public static bool DebugMode = false;
        public static bool WriteLogToFile = false;

        public static bool ChromeNoSandbox = false;
        public static bool Headless = false;
        public static string ChromeBinaryPath = "";
        public static int MaxBrowserInstances = 2;

        public static bool UseAPI = true;
        public static string APIUrl = "";

        public static bool PrioritizeQuest = true;
        public static bool ClaimQuestReward = false;
        public static bool ClaimSeasonReward = false;
        public static int SleepBetweenBattles = 30;
        public static int ERCThreshold = 75;

        public static List<BotInstance> BotInstances;
        public static List<(IWebDriver driver, bool isAvailable)> SeleniumInstances { get; set; }

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
