using System.Collections.Generic;

namespace Ultimate_Splinterlands_Bot_V2.Classes.Model
{
    public static class Quest
    {
        public static readonly Dictionary<string, string> Types = new Dictionary<string, string>()
            {
                { "Defend the Borders", Element.Life },
                { "Pirate Attacks", Element.Water },
                { "High Priority Targets", "snipe" },
                { "Lyanna's Call", Element.Earth },
                { "Stir the Volcano", Element.Fire },
                { "Rising Dead", Element.Death },
                { "Stubborn Mercenaries", Element.Neutral },
                { "Gloridax Revenge", Element.Dragon },
                { "Stealth Mission", "sneak" }
            };
    }
}