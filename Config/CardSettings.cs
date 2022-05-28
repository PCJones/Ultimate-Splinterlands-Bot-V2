using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Ultimate_Splinterlands_Bot_V2.Model;
using Ultimate_Splinterlands_Bot_V2.Utils;

namespace Ultimate_Splinterlands_Bot_V2.Config
{
    public record CardSettings
    {
        [JsonIgnore]
        public bool USE_CARD_SETTINGS { get; init; } = false;
        public int WINRATE_MODIFIER_OWNED_CARD_PERCENTAGE { get; init; } = 24;
        public double FLAT_NEGATIVE_MODIFIER_PER_UNOWNED_CARD { get; init; } = 2.5;
        public string PREFERRED_SUMMONER_ELEMENT { get; init; } = null;
        public int CARD_MIN_LEVEL { get; init; } = 1;
        public int WINRATE_TRESHOLD { get; init; } = 45;
        public int MINIMUM_GAMES { get; init; } = 10;
        public bool ADD_ZERO_MANA_CARDS { get; init; } = true;
        public bool PLAY_STARTER_CARDS { get; init; } = true;

        public CardSettings()
        {

        }
        public CardSettings(string config)
        {
            foreach (var setting in config.Split(Environment.NewLine))
            {
                string[] parts = setting.Split('=');
                if (parts.Length != 2 || setting[0] == '#' || setting.Length < 3)
                {
                    continue;
                }

                string settingsKey = parts[0];

                string value = parts[1];

                var property = this.GetType().GetProperty(settingsKey, BindingFlags.Public | BindingFlags.Instance);

                if (property == null)
                {
                    Console.WriteLine($"could not find setting for: '{parts[0]}'");
                    return;
                }

                if (property.PropertyType == typeof(bool))
                {
                    property.SetValue(this, Boolean.Parse(value));
                }
                else if (property.PropertyType == typeof(string))
                {
                    property.SetValue(this, value);
                }
                else if (property.PropertyType == typeof(Int32))
                {
                    property.SetValue(this, Convert.ToInt32(value));
                }
                else if (property.PropertyType == typeof(double))
                {
                    property.SetValue(this, Convert.ToDouble(value, CultureInfo.InvariantCulture));
                }
                else
                {
                    Console.WriteLine("Field Value: '{0}'", property.GetValue(this));
                    Console.WriteLine("Field Type: {0}", property.PropertyType);
                    Log.WriteToLog($"UNKOWN type '{property.PropertyType}'");
                }
            }
        }

        public Card[] FilterByCardSettings(Card[] unfilteredCards)
        {
            try
            {
                if (!USE_CARD_SETTINGS)
                {
                    return unfilteredCards;
                }

                var filteredCards = unfilteredCards.Where(x => Convert.ToInt32(x.level) >= CARD_MIN_LEVEL);
                return filteredCards.ToArray();
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"Error at applying card settings: { ex.Message }", Log.LogType.Error);
                return unfilteredCards;
            }
        }
    }
}
