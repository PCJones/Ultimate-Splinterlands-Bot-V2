using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Splinterlands_Battle_REST_API.Model
{
    public class DetailedCard
    {
        public int id { get; set; }
        public string name { get; set; }
        public string color { get; set; }
        public string type { get; set; }
        public object sub_type { get; set; }
        public int rarity { get; set; }
        // public int drop_rate { get; set; }
        //public Stats stats { get; set; }
        public Stats stats { get; set; }
        public bool is_starter { get; set; }
        public string editions { get; set; }
        // public int? created_block_num { get; set; }
        // public string last_update_tx { get; set; }
        // public int total_printed { get; set; }
        // public bool is_promo { get; set; }
        // public int? tier { get; set; }

        public int ManaCost => GetManaCost();

        private int GetManaCost()
        {
            if (stats.mana is JArray mana)
            {
                return (int)mana[0];
            }
            else return (int)(long)stats.mana;
        }

        public string GetCardColor(bool toLower = true)
        {
            var element = color
            .Replace("Red", "Fire").Replace("Blue", "Water").Replace("White", "Life").Replace("Black", "Death")
            .Replace("Green", "Earth").Replace("Gray", "Neutral").Replace("Gold", "Dragon");

            return toLower ? element.ToLower() : element;
        }

        public bool IsSummoner()
        {
            return type == "Summoner";
        }
    }

    public class Stats
    {
        public object mana { get; set; }
        public object attack { get; set; }
        public object ranged { get; set; }
        public object magic { get; set; }
        public object armor { get; set; }
        public object health { get; set; }
        public object speed { get; set; }
        public object abilities { get; set; }
    }

}
