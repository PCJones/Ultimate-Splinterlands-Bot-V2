using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Ultimate_Splinterlands_Bot_V2.Config;

namespace Ultimate_Splinterlands_Bot_V2.Model
{
    #nullable enable
    public record Quest
    {
        [JsonProperty("id")]
        public string Id { get; init; }
        [JsonProperty("player")]
        public string Player { get; init; }
        [JsonProperty("created_date")]
        public DateTime CreatedDate { get; init; }
        [JsonProperty("created_block")]
        public int CreatedBlock { get; init; }
        [JsonProperty("name")]
        public string Name { get; init; }
        [JsonProperty("total_items")]
        public int TotalItems { get; set; }
        [JsonProperty("completed_items")]
        public int CompletedItems { get; init; }
        [JsonProperty("claim_trx_id")]
        public string? ClaimTrxId { get; init; }
        [JsonProperty("claim_date")]
        public DateTime? ClaimDate { get; init; }
        [JsonProperty("reward_qty")]
        public int RewardQty { get; init; }
        [JsonProperty("refresh_trx_id")]
        public string? RefreshTrxID { get; init; }
        [JsonProperty("rewards")]
        public JToken? Rewards { get; init; }
        [JsonProperty("chest_tier")]
        public int? ChestTier { get; init; }
        [JsonProperty("rshares")]
        public int RShares { get; init; }
        [JsonProperty("league")]
        public int League { get; init; }
        [JsonIgnore]
        public bool IsComplete => DateTime.UtcNow.Date > CreatedDate.ToUniversalTime().Date;

        public Quest(string id, string player, DateTime createdDate, int createdBlock, string name, int totalItems, int completedItems, string? claimTrxId, DateTime claimDate, int rewardQty, string? refreshTrxID, JToken rewards, int chestTier, int rShares, int league)
        {
            Id = id;
            Player = player;
            CreatedDate = createdDate;
            CreatedBlock = createdBlock;
            Name = name;
            TotalItems = totalItems;
            CompletedItems = completedItems;
            ClaimTrxId = claimTrxId;
            ClaimDate = claimDate;
            RewardQty = rewardQty;
            RefreshTrxID = refreshTrxID;
            Rewards = rewards;
            ChestTier = chestTier;
            RShares = rShares;
            League = league;
        }

        public int GetChestAmount()
        {
            var questChestConfig = Settings.SplinterlandsSettings.LootChests.Quest[ChestTier ?? 0];
            var chests = -1;
            double totalRshares = 0;
            double nextChest = 0;

            while (RShares >= JavaScriptRound(totalRshares))
            {
                chests++;
                nextChest = questChestConfig.Base * Math.Pow(questChestConfig.StepMultiplier, chests);
                totalRshares += nextChest;
            }

            if (chests >= questChestConfig.Max)
            {
                return chests;
            }
            var progress = JavaScriptRound(nextChest) - (JavaScriptRound(totalRshares) - RShares);
            var rSharesUntilNextChest = Convert.ToInt32(JavaScriptRound(nextChest - progress));
            var progressPercentage = progress / nextChest;
            return 0;
        }

        // simulate java script Math.Round() - https://stackoverflow.com/a/1863604
        private static double JavaScriptRound(double value)
        {
            double rounded = Math.Floor(value + 0.5);
            return rounded;
        }
    }
}
