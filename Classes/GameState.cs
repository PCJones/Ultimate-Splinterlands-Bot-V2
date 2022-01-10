using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ultimate_Splinterlands_Bot_V2.Classes
{
    public enum GameState
    {
        match_found,
        opponent_submit_team,
        battle_result,
        transaction_complete,
        rating_update,
        ecr_update,
        balance_update,
        quest_progress,
        battle_cancelled,
        received_gifts
    };
}
