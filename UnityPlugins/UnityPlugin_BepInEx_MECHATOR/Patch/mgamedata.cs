using HarmonyLib;

namespace BepInEx_DemulShooter_Plugin.Patch
{
    class mgamedata
    {
        /// <summary>
        /// Force player number
        /// </summary>
        [HarmonyPatch(typeof(gamedata), "Awake")]
        class Awake
        {
            static void Postfix()
            {
                DemulShooter_Plugin.MyLogger.LogMessage("gamedata.Awake()");
                UIback.playernum = DemulShooter_Plugin.PlayerNum;
            }
        }
    }
}
