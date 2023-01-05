using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ultimate_Splinterlands_Bot_V2.Config;

namespace Ultimate_Splinterlands_Bot_V2.Model
{
    public record UserCard : IComparable
    {
        public string card_detail_id { get; init; }
        public string level { get; init; }
        public bool gold { get; init; }
        public bool starter { get; init; }

        [JsonIgnore]
        public string card_long_id { get; init; }
        [JsonIgnore]
        public bool IsSummoner { get; init; }

        public UserCard(string cardId, string _card_long_id, string _level, bool _gold, bool _starter)
        {
            card_detail_id = cardId;
            card_long_id = _card_long_id;
            level = _level;
            gold = _gold;
            starter = _starter;
            IsSummoner = cardId == "" ? false : (string)Settings.CardsDetails[Convert.ToInt32(cardId) - 1]["type"] == "Summoner";
        }

        public UserCard(string cardId, string _card_long_id, string _level, bool _gold, bool _starter, bool _summoner)
        {
            card_detail_id = cardId;
            card_long_id = _card_long_id;
            level = _level;
            gold = _gold;
            starter = _starter;
            IsSummoner = _summoner;
        }

        public int CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            UserCard otherCard = obj as UserCard;
            if (otherCard != null)
            {
                int ownCardLevel = Convert.ToInt32(this.level);
                int otherCardLevel = Convert.ToInt32(otherCard.level);
                if (ownCardLevel == otherCardLevel)
                {
                    if (this.gold == otherCard.gold)
                    {
                        if (this.starter == otherCard.starter)
                        {
                            return 0;
                        }
                        else if (!this.starter && otherCard.starter)
                        {
                            return 1;
                        }
                        else
                        {
                            return -1;
                        }
                    }
                    else if (this.gold)
                    {
                        return 1;
                    }
                    else
                    {
                        return -1;
                    }
                }
                else if (ownCardLevel > otherCardLevel)
                {
                    return 1;
                }
                else
                {
                    return -1;
                }
            }
            else
                throw new ArgumentException("Object is not a Card");
        }
    }
}
