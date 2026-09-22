using HarmonyLib;

namespace BepInEx_DemulShooter_Plugin
{
    class mtest_screen
    {
        [HarmonyPatch(typeof(test_screen), "Start")]
        class Start
        {
            static bool Prefix()
            {
                DemulShooter_Plugin.MyLogger.LogWarning("test_screen.Start()");
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
