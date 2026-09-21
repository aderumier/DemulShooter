using System;
using System.IO;
using System.Reflection;
using System.Text;
using CommonBase;
using HarmonyLib;
using LitJson;
using UnityEngine;

namespace BepInEx_DemulShooter_Plugin.Patch
{
    class mPortManager
    {
        /// <summary>
        /// Removing the call to Serial.Close as Serial is null
        /// </summary>
        [HarmonyPatch(typeof(PortManager), "ClosePort")]
        class ClosePort
        {
            static bool Prefix(PortManager __instance, ref bool ____StartThread, System.Threading.Thread ____PortThread)
            {
                DemulShooter_Plugin.MyLogger.LogMessage("PortManager.ClosePort()");
                ____StartThread = false;
                if (____PortThread != null)
                {
                    ____PortThread.Abort();
                }
                return false;
            }
        }

        /// <summary>
        /// Remove Serial port oppening
        /// </summary>
        [HarmonyPatch(typeof(PortManager), "Start")]
        class Start
        {
            static bool Prefix(PortManager __instance)
            {
                DemulShooter_Plugin.MyLogger.LogMessage("PortManager.Start()");
                string json = File.ReadAllText("Game.json", Encoding.UTF8);
                JsonData jsonData = JsonMapper.ToObject(json);
                MonoSingleton<DataRecord>.Instance.PowerNote = jsonData["Power"];
                MonoSingleton<DataRecord>.Instance.BallPowerNote = jsonData["BallPower"];
                MonoSingleton<DataRecord>.Instance.P4GunAngle = jsonData["P4GunAngle"];
                MonoSingleton<DataRecord>.Instance.P3GunAngle = jsonData["P3GunAngle"];
                MonoSingleton<DataRecord>.Instance.P2GunAngle = jsonData["P2GunAngle"];
                MonoSingleton<DataRecord>.Instance.BallSpeed = (int)jsonData["BallSpeed"];
                MonoSingleton<DataRecord>.Instance.Version_code = (string)jsonData["version_code"];
                MonoSingleton<DataRecord>.Instance.VersionNo = (string)jsonData["update_msg"];

                //__instance.StartCheckIOLink();
                foreach (MethodInfo mi in __instance.GetType().GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                {
                    if (mi.Name.Equals("StartCheckIOLink"))
                    {
                        mi.Invoke(__instance, null);
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Removing Serial read
        /// Replacing by packets send according to our needs
        /// Thread is called once per Frame by the FixedUpdate() function
        /// </summary>
        [HarmonyPatch(typeof(PortManager), "ReceiveData")]
        class ReceiveData
        {
            static bool Prefix(PortManager __instance, ref byte[] ___buf, ref bool ____StartThread, ref byte[] ____ReadBuff, ref int ____WriteID, ref int[] ___RemainNeedOutTicket)
            {
                //DemulShooter_Plugin.MyLogger.LogMessage("PortManager.ReceiveData()");
                int num = 0;
                DemulShooter_Plugin.ReceivedDataBuffer = new byte[DemulShooter_Plugin.INPUT_BUFFER_LENGTH];
                DemulShooter_Plugin.ReceivedDataBuffer[0] = 1;  //Cmd 1 = Keys + Axis + Tickets + Credits                                 

                //Water level switch, must be ON
                SetKeyBit(ref DemulShooter_Plugin.ReceivedDataBuffer, 19);

                //System Keys
                if (DemulShooter_Plugin.Test_Key.GetButtonDown())
                    SetKeyBit(ref DemulShooter_Plugin.ReceivedDataBuffer, 20);
                if (DemulShooter_Plugin.MenuSelect_Key.GetButtonDown())
                    SetKeyBit(ref DemulShooter_Plugin.ReceivedDataBuffer, 21);
                if (DemulShooter_Plugin.MenuUp_Key.GetButtonDown())
                    SetKeyBit(ref DemulShooter_Plugin.ReceivedDataBuffer, 22);
                if (DemulShooter_Plugin.MenuDown_Key.GetButtonDown())
                    SetKeyBit(ref DemulShooter_Plugin.ReceivedDataBuffer, 23);

                //Start Keys
                for (int i = 0; i < DemulShooter_Plugin.PluginControllers.Length; i++)
                {
                    if (DemulShooter_Plugin.PluginControllers[i].GetButtonDown(UnityPlugin_BepInEx_Core.PluginController.MyInputButtons.Start))
                        SetKeyBit(ref DemulShooter_Plugin.ReceivedDataBuffer, 4 + i);
                }

                //Credits Keys
                for (int i = 0; i < DemulShooter_Plugin.PluginControllers.Length; i++)
                {
                    if (DemulShooter_Plugin.PluginControllers[i].GetButtonDown(UnityPlugin_BepInEx_Core.PluginController.MyInputButtons.Coin))
                        DemulShooter_Plugin.ReceivedDataBuffer[36 + i] = 1;
                }

                //Players Buttons & Axis
                //000 -> FFF ==> PortManager.GetAxis() between +1.0 and -1.0 
                for (int i = 0; i < Singleton<GameData>.Instance.GetTotalPlayer(); i++)
                {
                    Vector3 ScreenPos = DemulShooter_Plugin.PluginControllers[i].GetAimingPosition();
                    float fx = ScreenPos.x / (float)Screen.width * 4095.0f;
                    float fy = ScreenPos.y / (float)Screen.height * 4095.0f;
                    byte[] bArray = BitConverter.GetBytes((UInt16)fx);
                    Array.Reverse(bArray);
                    Array.Copy(bArray, 0, DemulShooter_Plugin.ReceivedDataBuffer, 4 * i + 4, 2);
                    bArray = BitConverter.GetBytes((UInt16)fy);
                    Array.Reverse(bArray);
                    Array.Copy(bArray, 0, DemulShooter_Plugin.ReceivedDataBuffer, 4 * i + 6, 2);

                    if (DemulShooter_Plugin.PluginControllers[i].GetButton(UnityPlugin_BepInEx_Core.PluginController.MyInputButtons.Trigger))
                        SetKeyBit(ref DemulShooter_Plugin.ReceivedDataBuffer, i);
                }

                //Tickets response to command
                for (int i = 0; i < Singleton<GameData>.Instance.GetTotalPlayer(); i++)
                {
                    if (DemulShooter_Plugin.RemainingTicketsOut[i] > 0)
                    {
                        byte[] bArray = BitConverter.GetBytes(DemulShooter_Plugin.RemainingTicketsOut[i]);
                        Array.Reverse(bArray);
                        Array.Copy(bArray, 0, DemulShooter_Plugin.ReceivedDataBuffer, 4 * i + 20, 4);
                        DemulShooter_Plugin.RemainingTicketsOut[i] = 0;
                    }
                }

                CmdInfo command = new CmdInfo(1, DemulShooter_Plugin.ReceivedDataBuffer);
                ___buf = command.ToArray();
                num = ___buf.Length;
                //DemulShooter_Plugin.MyLogger.LogMessage("PortManager.ReceiveData(): " + DemulShooter_Plugin.ByteArrayToString(___buf));  

                if (num == 0)
                {
                    ____StartThread = true;
                    return false;
                }


                if (___buf != null)
                {
                    for (int i = 0; i < num; i++)
                    {
                        ____ReadBuff[____WriteID] = ___buf[i];
                        ____WriteID = (____WriteID + 1) % ____ReadBuff.Length;
                    }
                }

                ____StartThread = true;
                return false;
            }
        }
        private static void SetKeyBit(ref byte[] Buffer, int KeyId)
        {
            int BufferByte = KeyId / 8 + 1;
            int ByteMask = KeyId % 8;
            Buffer[BufferByte] |= (byte)(1 << ByteMask);
        }

        /// <summary>
        /// Remove Serail port writing
        /// </summary>
        [HarmonyPatch(typeof(PortManager), "SendData")]
        class SendData
        {
            static bool Prefix(PortManager __instance, byte[] data)
            {
                //DemulShooter_Plugin.MyLogger.LogMessage("PortManager.SendData(): length=" + data.Length + " -> " + DemulShooter_Plugin.ByteArrayToString(data));                
                return false;
            }
        }

        /// <summary>
        /// Intercepting SpineMotor outputs, going Negative/Positive on 1 byte (-100; +100)
        /// </summary>
        [HarmonyPatch(typeof(PortManager), "SendMotorSpine")]
        class SendMotorSpine
        {
            static bool Prefix(PortManager __instance, int id, byte speed)
            {
                //DemulShooter_Plugin.MyLogger.LogMessage("PortManager.SendMotorSpine: id=" + id + " ,speed=" + ((sbyte)speed).ToString());
                if (MonoSingleton<DataRecord>.Instance.MachineType == 2)
                {
                    return true;
                }
                DemulShooter_Plugin.OutputData.SpineMotor[id] = speed;
                return true;
            }
        }

        /// <summary>
        /// Intercepting Outputs. Id are:
        /// 00 - 03 : Ball Motor
        /// 04 - 13 : Lighting Effects
        /// 14 - 15 : Seat Vibration
        /// 
        /// 
        /// 00 - 03 : Gun Light / Ball Motor
        /// 10 - 13 : Weapon Bonus Light for player
        /// 14 : SeatVibration Light
        /// 16 : Weapon Bonus Light
        /// 17 : Water Level OK Light
        /// 18 : WaterFall Light
        /// </summary>
        [HarmonyPatch(typeof(PortManager), "SendOpenClosePower")]
        class SendOpenClosePower
        {
            static bool Prefix(PortManager __instance, int id, bool isOpen)
            {
                //DemulShooter_Plugin.MyLogger.LogMessage("PortManager.SendOpenClosePower: id=" + id + " ,isOpen=" + isOpen);
                if (id >= 0 && id < 4)
                    DemulShooter_Plugin.OutputData.BallMotor[id] = isOpen ? (byte)1 : (byte)0;
                else if (id >= 10 && id < 14)
                    DemulShooter_Plugin.OutputData.PlayerBonusWeaponLamp[id - 10] = isOpen ? (byte)1 : (byte)0;
                else if (id == 14)
                    DemulShooter_Plugin.OutputData.SeatVibrationLamp = isOpen ? (byte)1 : (byte)0;
                else if (id == 16)
                    DemulShooter_Plugin.OutputData.BonusWeaponLamp = isOpen ? (byte)1 : (byte)0;
                else if (id == 17)
                    DemulShooter_Plugin.OutputData.WaterLevelLamp = isOpen ? (byte)1 : (byte)0;
                else if (id == 18)
                    DemulShooter_Plugin.OutputData.WaterFallLamp = isOpen ? (byte)1 : (byte)0;
                return true;
            }
        }

        /// <summary>
        /// Storing request to print Tickets
        /// </summary>
        [HarmonyPatch(typeof(PortManager), "SendOut")]
        class SendOut
        {
            static bool Prefix(PortManager __instance, int id, int num, bool keyout = false)
            {
                //DemulShooter_Plugin.MyLogger.LogMessage("PortManager.SendOut: id=" + id + " ,num=" + num + ", keyout=" + keyout);
                DemulShooter_Plugin.RemainingTicketsOut[id] += num;
                return true;
            }
        }

        /// <summary>
        /// Intercepting Blinkin Outputs, mapping it to START Lamp
        /// if On=2 and Off=2 --> State blinking
        /// if On=10 and Off=0 --> Off (?)
        /// </summary>
        [HarmonyPatch(typeof(PortManager), "SendPowerSpark")]
        class SendPowerSpark
        {
            static bool Prefix(PortManager __instance, int id, byte on, byte off)
            {
                //DemulShooter_Plugin.MyLogger.LogMessage("PortManager.SendPowerSpark: id=" + id + " ,on=" + on + ", off=" + off);
                DemulShooter_Plugin.OutputData.StartLamp[id] = (on == 2 && off == 2) ? (byte)1 : (byte)0;
                return true;
            }
        }
        
        /// <summary>
        /// Get Coins
        /// Resetting the _LinkTimeCount value (should be reset when COM packet is received) to remove Connection Error message on screen
        /// </summary>
        [HarmonyPatch(typeof(PortManager), "FixedUpdate")]
        class FixedUpdate
        {
            static bool Prefix(PortManager __instance, int[] ___countCoin, ref float ____LinkTimeCount)
            {
                ____LinkTimeCount = 0.0f;

                for (int i = 0; i < Singleton<GameData>.Instance.GetTotalPlayer(); i++)
                {
                    DemulShooter_Plugin.OutputData.Credits[i] = ___countCoin[i];
                }
                return true;
            }
        }
    }
}
