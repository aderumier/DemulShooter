using HarmonyLib;
using UnityEngine;

namespace BepInEx_DemulShooter_Plugin.Patch
{
    class mGameMain
    {
        /// <summary>
        /// Change resolution
        /// </summary>
        [HarmonyPatch(typeof(GameMain), "Start")]
        class Start
        {
            static void Postfix()
            {
                DemulShooter_Plugin.MyLogger.LogMessage("GameMain.Start()");
                if (DemulShooter_Plugin.ForceResolution)
                    Screen.SetResolution(DemulShooter_Plugin.ScreenWidth, DemulShooter_Plugin.ScreenHeight, DemulShooter_Plugin.Fullscreen);
            }
        }
    }
}
