using HarmonyLib;

namespace BepInEx_DemulShooter_Plugin.Patch
{
    internal class mqiehuan_xiaoqiangjieshu
    {
        /// <summary>
        /// Replace the original [J] key to enter TEST menu
        /// </summary>
        [HarmonyPatch(typeof(qiehuan_xiaoqiangjieshu), "Update")]
        class Update
        {
            static bool Prefix(qiehuan_xiaoqiangjieshu __instance)
            {
               if (DemulShooter_Plugin.Test_Key.GetButton())
                    __instance.normal_control_work();
                return false;
            }
        }
    }
}
