using HarmonyLib;

namespace BepInEx_DemulShooter_Plugin.Patch
{
    internal class mzzp_houtai_manage
    {
        /// <summary>
        /// Replacing the Menu keys
        /// </summary>
        [HarmonyPatch(typeof(zzp_houtai_manage), "check_for_zzp_houtai_control")]
        class check_for_zzp_houtai_control
        {
            static bool Prefix(zzp_houtai_manage __instance)
            {
                if (DemulShooter_Plugin.MenuDown_Key.GetButtonDown())
                {
                    __instance.myzzp_houtai_control.add_key_work();
                }
                if (DemulShooter_Plugin.MenuSelect_Key.GetButtonDown())
                {
                    __instance.queding_work();
                }
                return false;
            }
        }
    }
}
