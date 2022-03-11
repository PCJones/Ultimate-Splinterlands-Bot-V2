using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Ultimate_Splinterlands_Bot_V2.Classes
{
    public record CardSettings
    {
        [JsonIgnore]
        public bool USE_CARD_SETTINGS { get; init; }
        public string PREFERRED_SUMMONER_ELEMENT { get; init; }
        public int CARD_MIN_LEVEL { get; init; }

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
                else
                {
                    Console.WriteLine("Field Value: '{0}'", property.GetValue(this));
                    Console.WriteLine("Field Type: {0}", property.PropertyType);
                    Log.WriteToLog($"UNKOWN type '{property.PropertyType}'");
                }
                
            }
        }
    }
}
