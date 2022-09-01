using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ultimate_Splinterlands_Bot_V2.Model
{
    internal class Reward
    {
        [JsonIgnore]
        public Quest Quest { get; set; }
        [JsonProperty]
        public int LastChestQuantity { get; private set; }
        [JsonProperty]
        public DateTime LastChestQuantityUpdate { get; private set; }

        public void SetLastChestQuantity(int quantity)
        {
            LastChestQuantity = quantity;
            LastChestQuantityUpdate = DateTime.Now;
        }
    }
}
