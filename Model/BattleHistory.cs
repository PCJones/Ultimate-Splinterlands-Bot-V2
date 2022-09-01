using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ultimate_Splinterlands_Bot_V2.Model
{
    internal class BattleHistory
    {
        public const int MAX_ENTRIES = 20;
        public const double MIN_RSHARE_AVERAGE = 30;

        private List<int> RShareRewards { get; set; }

        public BattleHistory()
        {
            RShareRewards = new();
        }
        public void AddRShareReward(int amount)
        {
            while (RShareRewards.Count >= MAX_ENTRIES)
            {
                RShareRewards.RemoveAt(RShareRewards.Count - 1);
            }
            RShareRewards.Add(amount);
        }
        public double GetRShareRewardAverage()
        {
            if (RShareRewards.Count < 5)
            {
                return MIN_RSHARE_AVERAGE;
            }

            return Math.Max(RShareRewards.Average(), MIN_RSHARE_AVERAGE);
        }
    }
}
