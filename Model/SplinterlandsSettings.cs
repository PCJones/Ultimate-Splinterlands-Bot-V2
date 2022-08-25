using Newtonsoft.Json;

namespace Ultimate_Splinterlands_Bot_V2.Model
{
    public partial class SplinterlandsSettings
    {
        [JsonProperty("loot_chests")]
        public LootChestConfig LootChests { get; set; }
    }

    public partial class LootChestConfig
    {
        [JsonProperty("quest")]
        public TypeChestConfig[] Quest { get; set; }

        [JsonProperty("season")]
        public TypeChestConfig[] Season { get; set; }
    }

    public partial class TierChestConfig
    {
        [JsonProperty("rarity_boost")]
        public double RarityBoost { get; set; }

        [JsonProperty("token_multiplier")]
        public long TokenMultiplier { get; set; }
    }

    public partial class TypeChestConfig
    {
        [JsonProperty("base")]
        public long Base { get; set; }

        [JsonProperty("step_multiplier")]
        public double StepMultiplier { get; set; }

        [JsonProperty("max")]
        public long Max { get; set; }
    }
}
