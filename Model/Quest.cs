using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Ultimate_Splinterlands_Bot_V2.Model
{
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
        public int TotalItems { get; init; }
        [JsonProperty("completed_items")]
        public int CompletedItems { get; init; }
        [JsonProperty("claim_trx_id")]
        public string ClaimTrxId { get; init; }
        [JsonProperty("claim_date")]
        public DateTime ClaimDate { get; init; }
        [JsonProperty("reward_qty")]
        public int RewardQty { get; init; }
        [JsonProperty("refresh_trx_id")]
        public string RefreshTrxID { get; init; }
        [JsonProperty("rewards")]
        public JToken Rewards { get; init; }
        [JsonProperty("chest_tier")]
        public int ChestTier { get; init; }
        [JsonProperty("rshares")]
        public int RShares { get; init; }
        [JsonProperty("league")]
        public int league { get; init; }
    }
}
