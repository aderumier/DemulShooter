using System;
using HarmonyLib;

namespace BepInEx_DemulShooter_Plugin.Patch
{
    class mbp_gamemgr
    {
        /// <summary>
        /// Intercept damage event
        /// ID = 0 target all players
        /// </summary>
        [HarmonyPatch(typeof(bp_gamemgr), "damageplayer")]
        class damageplayer
        {
            static bool Prefix(int i_dam, int i_targetid, bool b_superattack = false)
            {
                if (i_targetid == 0 || (i_targetid > 0 && gamedata.m_playerdata[i_targetid - 1].life <= 0f))
                {
                    for (int i = 0; i < DemulShooter_Plugin.MAX_PLAYERS; i++)
                    {
                        DemulShooter_Plugin.OutputData.Damaged[i] = 1;
                    }
                }
                else
                {
                    DemulShooter_Plugin.OutputData.Damaged[i_targetid] = 1;
                }
                return true;
            }
        }
    }
}
