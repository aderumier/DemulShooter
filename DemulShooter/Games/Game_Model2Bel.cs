using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using DsCore;
using DsCore.Config;
using DsCore.MameOutput;

namespace DemulShooter
{
    class Game_Model2Bel : Game
    {     
        /*** MEMORY ADDRESSES **/
        private UInt32 _Axis_Injection_Offset = 0x000C8937;
        private UInt32 _P1_X_CaveAddress;
        private UInt32 _P1_Y_CaveAddress;
        private UInt32 _P2_X_CaveAddress;
        private UInt32 _P2_Y_CaveAddress;
   
        //Outputs
        //private UInt32 _OutputsPtr_Offset = 0x001AA730;
        private UInt32 _PlayerStructPtr_Offset = 0x001AA8F4;
        private UInt32 _Outputs_BaseAddress;
        
        /// <summary>
        /// Constructor
        /// </summary>
        ///  public Naomi_Game(String DemulVersion, bool Verbose, bool DisableWindow)
        public Game_Model2Bel(String RomName)
            : base(RomName, "emulator")
        {            
            _KnownMd5Prints.Add("Model2Emulator 1.1a", "26bd488f9a391dcac1c5099014aa1c9e");
            _KnownMd5Prints.Add("Model2Emulator 1.1a multicpu", "ac59ce7cfb95d6d639c0f0d1afba1192");

            _tProcess.Start();
            Logger.WriteLog("Waiting for Model2 " + _RomName + " game to hook.....");
        }

        /// <summary>
        /// Timer event when looking for Process (auto-Hook and auto-close)
        /// </summary>
        protected override void tProcess_Elapsed(Object Sender, EventArgs e)
        {
            if (!_ProcessHooked)
            {
                try
                {
                    Process[] processes = Process.GetProcesses();
                    foreach (Process p in processes)
                    {
                        if (p.ProcessName.Equals("EMULATOR") || p.ProcessName.Equals("emulator_multicpu"))
                        {
                            _Target_Process_Name = p.ProcessName;
                            _TargetProcess = p;
                            _ProcessHandle = _TargetProcess.Handle;
                            _TargetProcess_MemoryBaseAddress = _TargetProcess.MainModule.BaseAddress;

                            if (_TargetProcess_MemoryBaseAddress != IntPtr.Zero)
                            {
                                _GameWindowHandle = _TargetProcess.MainWindowHandle;
                                Logger.WriteLog("Attached to Process " + _Target_Process_Name + ".exe, ProcessHandle = " + _ProcessHandle);
                                Logger.WriteLog(_Target_Process_Name + ".exe = 0x" + _TargetProcess_MemoryBaseAddress.ToString("X8"));
                                CheckExeMd5();
                                Apply_MemoryHacks();
                                _ProcessHooked = true;
                                RaiseGameHookedEvent();
                                break;
                            }
                        }
                    }
                }
                catch
                {
                    Logger.WriteLog("Error trying to hook " + _Target_Process_Name + ".exe");
                }
            }
            else
            {
                Process[] processes = Process.GetProcessesByName(_Target_Process_Name);
                if (processes.Length <= 0)
                {
                    _ProcessHooked = false;
                    _TargetProcess = null;
                    _ProcessHandle = IntPtr.Zero;
                    _TargetProcess_MemoryBaseAddress = IntPtr.Zero;
                    Logger.WriteLog(_Target_Process_Name + ".exe closed");
                    Application.Exit();
                }
            }
        }

        #region Screen

        /// <summary>
        /// Convert client area pointer location to Game speciffic data for memory injection
        /// </summary>
        public override bool GameScale(PlayerSettings PlayerData)
        {
            if (_ProcessHandle != IntPtr.Zero)
            {
                try
                {
                    double TotalResX = _ClientRect.Right - _ClientRect.Left;
                    double TotalResY = _ClientRect.Bottom - _ClientRect.Top;
                    Logger.WriteLog("Game Window Rect (Px) = [ " + TotalResX + "x" + TotalResY + " ]");

                    //X and Y => [0x00 - > 0xFE]
                    double dMaxX = 255.0;
                    double dMaxY = 255.0;

                    PlayerData.RIController.Computed_X = Convert.ToInt16(Math.Round(dMaxX * PlayerData.RIController.Computed_X / TotalResX));
                    PlayerData.RIController.Computed_Y = Convert.ToInt16(dMaxY - Math.Round(dMaxY * PlayerData.RIController.Computed_Y / TotalResY));

                    if (PlayerData.RIController.Computed_X < 0)
                        PlayerData.RIController.Computed_X = 0;
                    if (PlayerData.RIController.Computed_Y < 0)
                        PlayerData.RIController.Computed_Y = 0;
                    if (PlayerData.RIController.Computed_X > (int)dMaxX)
                        PlayerData.RIController.Computed_X = (int)dMaxX;
                    if (PlayerData.RIController.Computed_Y > (int)dMaxY)
                        PlayerData.RIController.Computed_Y = (int)dMaxY;

                    return true;
                }
                catch (Exception ex)
                {
                    Logger.WriteLog("Error scaling mouse coordonates to GameFormat : " + ex.Message.ToString());
                }
            }
            return false;
        }

        #endregion

        #region Memory Hack

        protected override void Apply_InputsMemoryHack()
        {
            Create_InputsDataBank();
            //At runtime, the game is reading 4 bytes with players axis values in that order :
            _P1_X_CaveAddress = _InputsDatabank_Address;
            _P2_X_CaveAddress = _InputsDatabank_Address + 1;
            _P1_Y_CaveAddress = _InputsDatabank_Address + 2;
            _P2_Y_CaveAddress = _InputsDatabank_Address + 3;

            //Axis : The game is reading axis values. Replacing the real address with our own
            byte[] b = BitConverter.GetBytes(_P1_X_CaveAddress);
            WriteBytes((UInt32)_TargetProcess_MemoryBaseAddress + _Axis_Injection_Offset + 2, b);

            //Initial values
            WriteBytes(_P1_X_CaveAddress, new byte[] { 0x55, 0xAA, 0x7F, 0x7F });

            Logger.WriteLog("Inputs Memory Hack complete !");
            Logger.WriteLog("-");
        }

        #endregion   
     
        #region Inputs

        /// <summary>
        /// Writing Axis and Buttons data in memory
        /// </summary>
        public override void SendInput(PlayerSettings PlayerData)
        {
            byte[] bufferX = BitConverter.GetBytes((Int16)PlayerData.RIController.Computed_X);
            byte[] bufferY = BitConverter.GetBytes((Int16)PlayerData.RIController.Computed_Y);

            if (PlayerData.ID == 1)
            {
                WriteByte(_P1_X_CaveAddress, bufferX[0]);
                WriteByte(_P1_Y_CaveAddress, bufferY[0]);
            }
            else if (PlayerData.ID == 2)
            {
                WriteByte(_P2_X_CaveAddress, bufferX[0]);
                WriteByte(_P2_Y_CaveAddress, bufferY[0]);
            }
        }

        #endregion

        #region Outputs

        /// <summary>
        /// Create the Output list that we will be looking for and forward to MameHooker
        /// </summary>
        protected override void CreateOutputList()
        {
            //Gun motor : Is activated for every bullet fired
            _Outputs = new List<GameOutput>();
            _Outputs.Add(new GameOutput(OutputId.P1_LmpStart));
            _Outputs.Add(new GameOutput(OutputId.P2_LmpStart));
            _Outputs.Add(new GameOutput(OutputId.P1_GunMotor));
            _Outputs.Add(new GameOutput(OutputId.P2_GunMotor));
            _Outputs.Add(new GameOutput(OutputId.P1_Ammo));
            _Outputs.Add(new GameOutput(OutputId.P2_Ammo));
            _Outputs.Add(new GameOutput(OutputId.P1_Clip));
            _Outputs.Add(new GameOutput(OutputId.P2_Clip));
            _Outputs.Add(new AsyncGameOutput(OutputId.P1_CtmRecoil, Configurator.GetInstance().OutputCustomRecoilOnDelay, Configurator.GetInstance().OutputCustomRecoilOffDelay, 0));
            _Outputs.Add(new AsyncGameOutput(OutputId.P2_CtmRecoil, Configurator.GetInstance().OutputCustomRecoilOnDelay, Configurator.GetInstance().OutputCustomRecoilOffDelay, 0));
            _Outputs.Add(new GameOutput(OutputId.P1_Life));
            _Outputs.Add(new GameOutput(OutputId.P2_Life));
            _Outputs.Add(new AsyncGameOutput(OutputId.P1_Damaged, Configurator.GetInstance().OutputCustomDamagedDelay, 100, 0));
            _Outputs.Add(new AsyncGameOutput(OutputId.P2_Damaged, Configurator.GetInstance().OutputCustomDamagedDelay, 100, 0));
            _Outputs.Add(new GameOutput(OutputId.Credits));
        }

        /// <summary>
        /// Update all Outputs values before sending them to MameHooker
        /// </summary>
        public override void UpdateOutputValues()
        { 
            /*SetOutputValue(OutputId.P1_LmpStart, ReadByte(_Outputs_Address) & 0x01);
            SetOutputValue(OutputId.P2_LmpStart, ReadByte(_Outputs_Address) >> 1 & 0x01);
            SetOutputValue(OutputId.P1_GunMotor, ReadByte(_Outputs_Address) >> 2 & 0x01);
            SetOutputValue(OutputId.P2_GunMotor, ReadByte(_Outputs_Address) >> 3 & 0x01);*/

            //custom Outputs  
            UInt32 iTemp = ReadPtr((UInt32)_TargetProcess_MemoryBaseAddress + _PlayerStructPtr_Offset);
            _Outputs_BaseAddress = ReadPtr(iTemp + 0x540);
            _P1_Ammo = BitConverter.ToInt16(ReadBytes(_Outputs_BaseAddress + 0x005B4954, 2), 0);
            if (_P1_Ammo <= 0)
                _P1_Ammo = 0;
            _P2_Ammo = BitConverter.ToInt16(ReadBytes(_Outputs_BaseAddress + 0x005B49D8, 2), 0);
            if (_P2_Ammo <= 0)
                _P2_Ammo = 0;
            _P1_Life = BitConverter.ToUInt16(ReadBytes(_Outputs_BaseAddress + 0x005B4996, 2), 0);
            _P2_Life = BitConverter.ToUInt16(ReadBytes(_Outputs_BaseAddress + 0x005B4A1E, 2), 0);

            //Custom Recoil
            if (_P1_Ammo < _P1_LastAmmo)
                SetOutputValue(OutputId.P1_CtmRecoil, 1);

            //[Clip Empty] custom Output
            if (_P1_Ammo <= 0)
                SetOutputValue(OutputId.P1_Clip, 0);
            else
                SetOutputValue(OutputId.P1_Clip, 1);

            //[Damaged] custom Output                
            if (_P1_Life < _P1_LastLife)
                SetOutputValue(OutputId.P1_Damaged, 1);

            //Custom Recoil
            if (_P2_Ammo < _P2_LastAmmo)
                SetOutputValue(OutputId.P2_CtmRecoil, 1);

            //[Clip Empty] custom Output
            if (_P2_Ammo <= 0)
                SetOutputValue(OutputId.P2_Clip, 0);
            else
                SetOutputValue(OutputId.P2_Clip, 1);

            //[Damaged] custom Output                
            if (_P2_Life < _P2_LastLife)
                SetOutputValue(OutputId.P2_Damaged, 1);

            _P1_LastAmmo = _P1_Ammo;
            _P1_LastLife = _P1_Life;
            _P2_LastAmmo = _P2_Ammo;
            _P2_LastLife = _P2_Life;

            SetOutputValue(OutputId.P1_Life, _P1_Life);
            SetOutputValue(OutputId.P2_Life, _P2_Life);
            SetOutputValue(OutputId.P1_Ammo, _P1_Ammo);
            SetOutputValue(OutputId.P2_Ammo, _P2_Ammo);
            SetOutputValue(OutputId.Credits, ReadByte(_Outputs_BaseAddress + 0x005A87F0));
        }

        #endregion
    }
}
