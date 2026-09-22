using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityPlugin_BepInEx_Core;

namespace BepInEx_DemulShooter_Plugin.Patch
{
    internal class mgame_system_control_insert_currency
    {
        /// <summary>
        /// Change COIN keys
        /// Changing the KeysCodes in the list does not really work (??)
        /// So instead, clearing the list and manually adding coin before the procedure starts
        /// </summary>
        [HarmonyPatch(typeof(game_system_control_insert_currency), "update_work")]
        class Update_work
        {
            static bool Prefix(game_system_control_insert_currency __instance)
            {
                __instance.myinsert_keycodes.Clear();
                for (int i = 0; i < DemulShooter_Plugin.PluginControllers.Length; i++)
                {
                    if (DemulShooter_Plugin.PluginControllers[i].GetButtonDown(PluginController.MyInputButtons.Coin))
                    {
                        game_system_control_insert_currency.my_coin_count_s[i]++;
                        if (game_system_control_insert_currency.my_coin_count_s[i] >= 256)
                        {
                            game_system_control_insert_currency.my_coin_count_s[i] -= 256;
                        }
                    }
                }
                
                return true;
            }
        }
    }
}
