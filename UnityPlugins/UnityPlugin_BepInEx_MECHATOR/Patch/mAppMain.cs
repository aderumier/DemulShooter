using HarmonyLib;

namespace BepInEx_DemulShooter_Plugin.Patch
{
    class mAppMain
    {
        /// <summary>
        /// Changing resolution
        /// </summary>
        [HarmonyPatch(typeof(AppMain), "Awake")]
        class Awake
        {
            static bool Prefix()
            {
                DemulShooter_Plugin.MyLogger.LogMessage("AppMain.Awake()");
                if (DemulShooter_Plugin.ForceResolution)
                {
                    UnityEngine.Screen.SetResolution(DemulShooter_Plugin.ScreenWidth, DemulShooter_Plugin.ScreenHeight, DemulShooter_Plugin.Fullscreen);
                    return false;
                }
                return true;
            }
        }
    }
}
