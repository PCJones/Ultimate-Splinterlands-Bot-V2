using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ultimate_Splinterlands_Bot_V2.Classes
{
    public record Card
    {
        public string card_detail_id { get; init; }
        public string level { get; init; }
        public bool gold { get; init; }

        [JsonIgnore]
        public string card_long_id { get; init; }

        public Card(string cardId, string _card_long_id, string _level, bool _gold)
        {
            card_detail_id = cardId;
            card_long_id = _card_long_id;
            level = _level;
            gold = _gold;
        }

        public int SortValue()
        {
            return gold ? Convert.ToInt32(level + 1) : Convert.ToInt32(level);
        }
    }
}