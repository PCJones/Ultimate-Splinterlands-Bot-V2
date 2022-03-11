using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ultimate_Splinterlands_Bot_V2.Classes.Model
{
    public record LogSummary
    {
        public int Index { get; init; }
        public string Account { get; set; }
        public string BattleResult { get; set; }
        public string Rating { get; set; }
        public string ECR { get; set; }
        public string QuestStatus { get; set; }

        public LogSummary(int index, string account)
        {
            Index = index;
            Account = account;
            Reset();
        }

        public void Reset()
        {
            BattleResult = "N/A";
            Rating = "N/A";
            ECR = "N/A";
            QuestStatus = "N/A";
        }
    }
}
