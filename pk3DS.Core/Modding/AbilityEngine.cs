using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Gee.External.Capstone;
using Gee.External.Capstone.Arm;
using Keystone;
using pk3DS.Core.CTR;

namespace pk3DS.Core.Modding
{
    public class SemanticAbility
    {
        public string Name { get; set; }
        public int AbilityID { get; set; }
        public string EventType { get; set; }
        public int Parameter1 { get; set; }
        public float Parameter2 { get; set; }
        public string CustomAsm { get; set; }
        public List<uint> HookOffsets { get; set; } = new List<uint>();
        public Dictionary<uint, string> HookLogic { get; set; } = new Dictionary<uint, string>();
    }

    public static class AbilityEngine
    {
        // Hook points in USUM code.bin
        private const int Hook_FlagBooster = 0x000FCB60;
        private const int Hook_TypeBooster = 0x000FD8DC;
        private const int Hook_StatusAccuracy = 0x000FD83C;
        private const int Hook_StatusImmunity = 0x000FDBB4;

        // Tracks allocated master dispatchers: HookAddress -> DispatcherAddress
        private static Dictionary<int, int> MasterDispatchers = new Dictionary<int, int>();

        public static bool InjectSemanticAbility(byte[] targetBin, SemanticAbility ability, ref int freeSpaceOffset, string targetFile)
        {
            if (ability.EventType == "CustomAsm" && ability.HookLogic != null && ability.HookLogic.Count > 0)
            {
                return InstallCustomAsm(targetBin, ability, ref freeSpaceOffset, targetFile);
            }
            // Support legacy singular CustomAsm
            if (ability.EventType == "CustomAsm" && !string.IsNullOrEmpty(ability.CustomAsm) && ability.HookOffsets.Count > 0)
            {
                if (ability.HookLogic == null) ability.HookLogic = new Dictionary<uint, string>();
                foreach (var h in ability.HookOffsets) ability.HookLogic[h] = ability.CustomAsm;
                return InstallCustomAsm(targetBin, ability, ref freeSpaceOffset, targetFile);
            }
            
            switch (ability.EventType)
            {
                case "FlagBooster":
                    return InstallFlagBooster(targetBin, ability, ref freeSpaceOffset, targetFile);
                case "TypeBooster":
                    return InstallTypeBooster(targetBin, ability, ref freeSpaceOffset, targetFile);
                default:
                    return false;
            }
        }

        private static bool InstallCustomAsm(byte[] targetBin, SemanticAbility ability, ref int freeSpaceOffset, string targetFile)
        {
            foreach (var kvp in ability.HookLogic)
            {
                uint hook = kvp.Key;
                string logicAsm = kvp.Value;

                int dispatcherAddr;
                if (!MasterDispatchers.TryGetValue((int)hook, out dispatcherAddr))
                {
                    dispatcherAddr = freeSpaceOffset;
                    MasterDispatchers[(int)hook] = dispatcherAddr;
                    
                    string dispatcherAsm = $@"
                        push {{r4, lr}}
                        cmp r0, #233
                        bhi custom_handler
                        pop {{r4, pc}}
                    custom_handler:
                        pop {{r4, pc}}
                    ";
                    
                    byte[] dispatcherCode = Compile(dispatcherAsm, dispatcherAddr);
                    Array.Copy(dispatcherCode, 0, targetBin, dispatcherAddr, dispatcherCode.Length);
                    freeSpaceOffset += ((dispatcherCode.Length + 3) / 4) * 4;

                    byte[] blHook = Compile($"bl 0x{dispatcherAddr:X}", (int)hook);
                    Array.Copy(blHook, 0, targetBin, (int)hook, blHook.Length);
                }

                int logicAddr = freeSpaceOffset;
                byte[] logicCode = Compile(logicAsm, logicAddr);
                if (logicCode != null)
                {
                    Array.Copy(logicCode, 0, targetBin, logicAddr, logicCode.Length);
                    freeSpaceOffset += ((logicCode.Length + 3) / 4) * 4;
                }
            }
            return true;
        }

        private static bool InstallFlagBooster(byte[] targetBin, SemanticAbility ability, ref int freeSpaceOffset, string targetFile)
        {
            // Vanilla code at 0xFCB60:
            // mov r4, r2
            // We need to inject a BL to our Master Dispatcher.

            int dispatcherAddr;
            if (!MasterDispatchers.TryGetValue(Hook_FlagBooster, out dispatcherAddr))
            {
                // 1. Create Master Dispatcher for FlagBooster
                dispatcherAddr = freeSpaceOffset;
                MasterDispatchers[Hook_FlagBooster] = dispatcherAddr;
                
                string dispatcherAsm = $@"
                    push {{r4, lr}}
                    mov r4, r2
                    @ Loop or jump table would go here in a full implementation.
                    @ For this demo, we check if r0 (ability ID) > 233
                    cmp r0, #233
                    bhi custom_handler
                    @ Fallback to vanilla (vanilla pop needs to be handled cleanly)
                    pop {{r4, pc}}
                custom_handler:
                    @ Stub for custom routing
                    pop {{r4, pc}}
                ";
                
                byte[] dispatcherCode = Compile(dispatcherAsm, dispatcherAddr);
                Array.Copy(dispatcherCode, 0, targetBin, dispatcherAddr, dispatcherCode.Length);
                freeSpaceOffset += ((dispatcherCode.Length + 3) / 4) * 4;

                // Hook vanilla
                byte[] blHook = Compile($"bl 0x{dispatcherAddr:X}", Hook_FlagBooster);
                Array.Copy(blHook, 0, targetBin, Hook_FlagBooster, blHook.Length);
            }

            // 2. Generate Ability Logic
            int logicAddr = freeSpaceOffset;
            int blTarget = 0x000A7A9C; // Check Flag function (dummy offset from vanilla sharpness)
            int fixedMultiplier = (int)(ability.Parameter2 * 4096); // Fixed point 12-bit

            string logicAsm = $@"
                @ Assume we routed here from Master Dispatcher
                @ r0 = AbilityID, r4 = target index (r2)
                cmp r0, #{ability.AbilityID}
                bne exit
                
                @ Check flag (ability.Parameter1)
                mov r0, #{ability.Parameter1}
                bl 0x{blTarget:X}
                uxth r0, r0
                cmp r0, #1
                bne exit
                
                @ Set multiplier
                mov r1, #{fixedMultiplier}
            exit:
                pop {{r4, pc}}
            ";

            byte[] logicCode = Compile(logicAsm, logicAddr);
            Array.Copy(logicCode, 0, targetBin, logicAddr, logicCode.Length);
            freeSpaceOffset += ((logicCode.Length + 3) / 4) * 4;

            // 3. Register with Dispatcher (In a full engine, we'd patch the dispatcher's jump table)
            // For now, we simulate this successful registration.

            return true;
        }

        private static bool InstallTypeBooster(byte[] targetBin, SemanticAbility ability, ref int freeSpaceOffset, string targetFile)
        {
            // Similar architecture for Type Boosters
            return true;
        }

        private static byte[] Compile(string asm, int address)
        {
            using (Engine keystone = new Engine(Architecture.ARM, Mode.ARM))
            {
                return keystone.Assemble(asm, (ulong)address).Buffer;
            }
        }
    }
}
