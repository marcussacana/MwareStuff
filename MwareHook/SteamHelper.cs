using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using StringReloads.Engine;
using StringReloads.Tools;

namespace MwareHook
{
    internal static unsafe class SteamHelper
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern void* GetModuleHandleA(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern void* GetProcAddress(void* hModule, string lpProcName);

        public static List<IntPtr> FindSteamClientCallers(ProcessModule mainModule)
        {
            var callers = new List<IntPtr>();
            bool is64 = Environment.Is64BitProcess;
            byte* pBase = (byte*)mainModule.BaseAddress.ToPointer();

            void* exportAddress = FindSteamClientExport(is64);
            void* iatAddress = FindSteamClientIAT(pBase, is64, out void* iatExport);
            if (exportAddress == null)
                exportAddress = iatExport;

            // If IAT was not found by parsing import headers, but we have the export address,
            // scan module memory for any pointer equal to exportAddress
            if (iatAddress == null && exportAddress != null)
            {
                ulong targetVal = (ulong)exportAddress;
                ulong modSize = (ulong)mainModule.ModuleMemorySize;
                int step = is64 ? 8 : 4;
                for (ulong off = 0; off + (ulong)step <= modSize; off += (ulong)step)
                {
                    ulong val = is64 ? *(ulong*)(pBase + off) : *(uint*)(pBase + off);
                    if (val == targetVal)
                    {
                        iatAddress = pBase + off;
                        Log.Debug($"SteamClient IAT slot found by memory scan at 0x{(ulong)iatAddress:X}");
                        break;
                    }
                }
            }

            if (iatAddress == null && exportAddress == null)
            {
                Log.Trace("SteamClient not imported or loaded.");
                return callers;
            }

            Log.Debug($"SteamClient Target: IAT=0x{(ulong)iatAddress:X}, Export=0x{(ulong)exportAddress:X}");

            // Generic caller search provided by StringReloads.Tools.Scanner
            foreach (var caller in Scanner.SearchCallers(iatAddress, exportAddress))
            {
                callers.Add(new IntPtr(caller));
            }

            return callers;
        }

        private static void* FindSteamClientExport(bool is64)
        {
            void* hSteam = GetModuleHandleA(is64 ? "steam_api64.dll" : "steam_api.dll");
            if (hSteam == null)
                hSteam = GetModuleHandleA(is64 ? "steam_api.dll" : "steam_api64.dll");

            if (hSteam == null)
            {
                foreach (ProcessModule mod in Process.GetCurrentProcess().Modules)
                {
                    if (mod.ModuleName.IndexOf("steam_api", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hSteam = mod.BaseAddress.ToPointer();
                        break;
                    }
                }
            }

            if (hSteam != null)
            {
                void* pProc = GetProcAddress(hSteam, "SteamClient");
                if (pProc != null)
                {
                    Log.Debug($"Found SteamClient export at 0x{(ulong)pProc:X}");
                    return pProc;
                }
            }

            return null;
        }

        private static void* FindSteamClientIAT(byte* pBase, bool is64, out void* exportAddress)
        {
            exportAddress = null;
            try
            {
                uint peOffset = *(uint*)(pBase + 0x3C);
                byte* pNtHeader = pBase + peOffset;
                if (*(uint*)pNtHeader != 0x00004550) // "PE\0\0"
                    return null;

                byte* pOptHeader = pNtHeader + 0x18;
                ushort magic = *(ushort*)pOptHeader;
                bool optIs64 = (magic == 0x20B);

                uint importDirRva = optIs64 ? *(uint*)(pOptHeader + 0x78) : *(uint*)(pOptHeader + 0x68);
                uint importDirSize = optIs64 ? *(uint*)(pOptHeader + 0x7C) : *(uint*)(pOptHeader + 0x6C);

                if (importDirRva == 0 || importDirSize == 0)
                    return null;

                byte* pImportDesc = pBase + importDirRva;
                while (true)
                {
                    uint originalFirstThunk = *(uint*)(pImportDesc + 0);
                    uint nameRva = *(uint*)(pImportDesc + 12);
                    uint firstThunk = *(uint*)(pImportDesc + 16);

                    if (nameRva == 0 && firstThunk == 0)
                        break;

                    if (nameRva != 0 && firstThunk != 0)
                    {
                        string modName = Marshal.PtrToStringAnsi(new IntPtr(pBase + nameRva));
                        if (modName != null && modName.IndexOf("steam_api", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            uint thunkRva = originalFirstThunk != 0 ? originalFirstThunk : firstThunk;
                            if (optIs64)
                            {
                                ulong* pThunk = (ulong*)(pBase + thunkRva);
                                ulong* pIat = (ulong*)(pBase + firstThunk);
                                while (*pThunk != 0)
                                {
                                    if ((*pThunk & (1UL << 63)) == 0)
                                    {
                                        byte* pByName = pBase + (uint)(*pThunk);
                                        string funcName = Marshal.PtrToStringAnsi(new IntPtr(pByName + 2));
                                        if (string.Equals(funcName, "SteamClient", StringComparison.OrdinalIgnoreCase))
                                        {
                                            exportAddress = (void*)(*pIat);
                                            Log.Debug($"Found SteamClient in IAT at 0x{(ulong)pIat:X}");
                                            return pIat;
                                        }
                                    }
                                    pThunk++;
                                    pIat++;
                                }
                            }
                            else
                            {
                                uint* pThunk = (uint*)(pBase + thunkRva);
                                uint* pIat = (uint*)(pBase + firstThunk);
                                while (*pThunk != 0)
                                {
                                    if ((*pThunk & (1U << 31)) == 0)
                                    {
                                        byte* pByName = pBase + (*pThunk);
                                        string funcName = Marshal.PtrToStringAnsi(new IntPtr(pByName + 2));
                                        if (string.Equals(funcName, "SteamClient", StringComparison.OrdinalIgnoreCase))
                                        {
                                            exportAddress = (void*)(*pIat);
                                            Log.Debug($"Found SteamClient in IAT at 0x{(ulong)pIat:X}");
                                            return pIat;
                                        }
                                    }
                                    pThunk++;
                                    pIat++;
                                }
                            }
                        }
                    }

                    pImportDesc += 20;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"Error parsing PE imports for SteamClient: {ex.Message}");
            }

            return null;
        }
    }
}
