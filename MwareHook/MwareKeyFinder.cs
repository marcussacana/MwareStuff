using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using Iced.Intel;
using StringReloads.Engine;
using StringReloads.Engine.Interface;
using StringReloads.Engine.Unmanaged;

namespace MwareHook
{
    unsafe class MwareKeyFinder : IMod
    {
        CreateMutexInterceptor MInterceptor;
        KeyInterceptor KInterceptor;
        DiasmHelper Helper;
        CodeInfo? Info;
        byte?[] Pattern = { 0x66, 0x0F, 0x3A, 0xDF, null, 0x01 };
        byte?[] FunctionHeader = { 0xCC, 0xCC, 0xCC, 0xCC };

        void* Address;

        public string Name => "MwareKeyFinder";

        public void Install()
        {
            if (Info == null)
            {
                var MainModule = Process.GetCurrentProcess().MainModule;
                foreach (var Module in Process.GetCurrentProcess().Modules.Cast<ProcessModule>())
                {
                    if (Module.ModuleName.ToLower().Trim() == "main.bin")
                    {
                        MainModule = Module;//SoftDenchi OEP
                    }
                }

                var Exe = File.ReadAllBytes(Config.Default.GameExePath);
                fixed (void* pExe = &Exe[0])
                {
                    var SteamStub = new byte?[] { 0x2E, 0x62, 0x69, 0x6E, 0x64 };
                    for (int i = 0; i < Exe.Length; i++)
                    {
                        bool Protected = CheckPattern((byte*)pExe + i, SteamStub);
                        if (Protected)
                        {
                            User.ShowMessageBox("This Game is protected with the Steam Stub DRM\nTo the Key Finder works you must crack it before.", "MwareKeyFinder - By Marcussacana", User.MBButtons.Ok, User.MBIcon.Error);
                            break;
                        }
                    }
                }

                Info = ModuleInfo.GetCodeInfo((byte*)MainModule.BaseAddress.ToPointer());

                if (TryFindKeyViaSteamClient(MainModule, out byte[] steamKey))
                {
                    OnKeyIntercepted(steamKey);
                    return;
                }

                if (!Scan(out Address))
                {
                    Log.Warning("Failed to find the KeyExpander Function, Trying WinMain Method...");
                    MInterceptor = new CreateMutexInterceptor(OnCreateMutexCalled);
                }
                else
                {
                    Helper = new DiasmHelper(Address);
                    var Instruction = Helper.Diassembly();
                    var KeyRegister = Instruction.MemoryBase.GetFullRegister32();
                    KInterceptor = new KeyInterceptor(Address, KeyRegister);
                    KInterceptor.OnKeyIntercepted = OnKeyIntercepted;
                    Log.Debug("Mware Key Interceptor Ready");
                }
            }

            if (KInterceptor != null)
                KInterceptor.Install();

            if (MInterceptor != null)
                MInterceptor.Install();
        }

        private bool TryFindKeyViaSteamClient(ProcessModule mainModule, out byte[] key)
        {
            key = null;
            try
            {
                var callers = SteamHelper.FindSteamClientCallers(mainModule);
                if (callers == null || callers.Count == 0)
                {
                    Log.Trace("No SteamClient callers found.");
                    return false;
                }

                int bitness = Environment.Is64BitProcess ? 64 : 32;
                foreach (var caller in callers)
                {
                    void* pCaller = caller.ToPointer();
                    Log.Debug($"Analyzing SteamClient caller at 0x{(ulong)pCaller:X}...");
                    if (TryExtractKeyFromDisassembly(pCaller, bitness, out key))
                    {
                        Log.Debug($"Key successfully extracted from SteamClient caller at 0x{(ulong)pCaller:X}!");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"Error while scanning for SteamClient key: {ex.Message}");
            }

            return false;
        }

        public static bool TryExtractKeyFromDisassembly(void* pCaller, int bitness, out byte[] key)
        {
            key = null;
            if (pCaller == null)
                return false;

            try
            {
                var diasm = new DiasmHelper(pCaller, bitness);
                var dwords = new Dictionary<(Register baseReg, long disp), uint>();

                int tries = 0;
                const int maxInstructions = 1000;

                while (tries < maxInstructions)
                {
                    tries++;
                    Instruction instruction = diasm.Diassembly();

                    if (instruction.IsInvalid)
                        break;

                    // Check for mov dword ptr [base + disp], imm32
                    if (instruction.Code == Code.Mov_rm32_imm32 &&
                        instruction.Op0Kind == OpKind.Memory &&
                        instruction.MemoryIndex == Register.None)
                    {
                        Register baseReg = instruction.MemoryBase;
                        long disp = bitness == 64
                            ? (long)instruction.MemoryDisplacement64
                            : (long)(int)instruction.MemoryDisplacement32;
                        uint val = instruction.Immediate32;

                        dwords[(baseReg, disp)] = val;
                        Log.Trace($"Found key candidate dword: [{baseReg}+0x{disp:X}] = 0x{val:X8} at 0x{instruction.IP:X}");

                        // Check if this completes an 8-dword consecutive sequence
                        for (int offset = 0; offset <= 28; offset += 4)
                        {
                            long baseDisp = disp - offset;
                            bool foundAll = true;
                            byte[] candidateKey = new byte[0x20];

                            for (int i = 0; i < 8; i++)
                            {
                                if (dwords.TryGetValue((baseReg, baseDisp + (i * 4)), out uint dwordVal))
                                {
                                    BitConverter.GetBytes(dwordVal).CopyTo(candidateKey, i * 4);
                                }
                                else
                                {
                                    foundAll = false;
                                    break;
                                }
                            }

                            if (foundAll)
                            {
                                Log.Debug($"Encryption key found at [{baseReg}+0x{baseDisp:X}] after {tries} instructions!");
                                key = candidateKey;
                                return true;
                            }
                        }
                    }

                    // Stop if function returns without finding candidates
                    if (instruction.Code == Code.Retnq || instruction.Code == Code.Retnd)
                    {
                        if (dwords.Count == 0 && tries > 50)
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"Exception during key disassembly: {ex.Message}");
            }

            return false;
        }

        void OnKeyIntercepted(byte[] Key)
        {
            if (KInterceptor != null)
                KInterceptor.Uninstall();

            string KeyStr = string.Empty;
            for (int i = 0; i < Key.Length; i++)
            {
                KeyStr += $"0x{Key[i]:X2}, ";
            }
            KeyStr = KeyStr.TrimEnd(' ', ',');
            User.ShowMessageBox("Encryption Key Found:\n" + KeyStr, "MwareKeyFinder - By Marcussacana", User.MBButtons.Ok, User.MBIcon.Information);
        }

        //Alternative (Less Stable) Key Find Method
        void OnCreateMutexCalled(uint Caller)
        {

            var pCaller = (void*)Caller;
            bool FromMainModule = Info.Value.AddressIsContained(pCaller);
            Log.Trace($"CreateMutex Called At: 0x{Caller:X8} ({(FromMainModule ? "Main Module" : "Secundary Module")})");
            if (!FromMainModule)
                return;

            MInterceptor.Uninstall();

            // Try the updated non-contiguous extraction first
            if (TryExtractKeyFromDisassembly(pCaller, 32, out byte[] key))
            {
                OnKeyIntercepted(key);
                return;
            }

            Helper = new DiasmHelper(pCaller);

            var List = new InstructionList();

            bool InMissmatch = false;
            bool InEnd = false;
            int MovCount = 0;
            int Tries = 0;
            while (Tries <= 500)
            {
                Tries++;
                if (MovCount > 7)
                    InEnd = true;

                var Instruction = Helper.Diassembly();

                bool IsMov = Instruction.Code == Code.Mov_rm32_imm32;

                if (IsMov)
                    Log.Trace($"{Instruction} at ({Instruction.IP:X8})");

                if (IsMov)
                    MovCount++;

                if (!IsMov)
                {
                    if (InMissmatch)
                    {
                        MovCount = 0;
                        InMissmatch = false;
                    }
                    else
                        InMissmatch = true;
                }
                else
                    InMissmatch = false;

                if (InEnd && !IsMov)
                    break;

                if (!InEnd && IsMov)
                    List.Add(Instruction);
                else if (!InEnd && !InMissmatch)
                    List.Clear();
            }

            if (Tries >= 500)
                return;

            byte[] KBuffer = new byte[0x20];
            for (int i = 0; i < List.Count; i++)
                BitConverter.GetBytes(List[i].Immediate32).CopyTo(KBuffer, i * 4);

            OnKeyIntercepted(KBuffer);
        }

        private bool Scan(out void* Address)
        {
            Address = null;
            long CodeAdd = (long)Info.Value.CodeAddress;
            long CodeLen = Info.Value.CodeSize - Pattern.Length;
            for (int i = 0; i < CodeLen; i++)
            {
                byte* pBuffer = (byte*)(Info.Value.CodeAddress) + i;
                if (!CheckPattern(pBuffer, Pattern))
                    continue;
                Log.Debug($"Decryption Key Pattern Found At: 0x{(ulong)pBuffer:X8}");
                for (long x = (long)pBuffer; x > CodeAdd; x--)
                {
                    byte* pFunc = (byte*)x;
                    if (!CheckPattern(pFunc, FunctionHeader))
                        continue;
                    Address = pFunc + FunctionHeader.Length;
                    return true;
                }
            }
            return false;
        }

        private bool CheckPattern(byte* Buffer, byte?[] Pattern)
        {
            for (int i = 0; i < Pattern.Length; i++)
            {
                if (Pattern[i] == null)
                    continue;
                byte bPattern = Pattern[i].Value;
                if (bPattern != Buffer[i])
                    return false;
            }
            return true;
        }

        public bool IsCompatible() => File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mware.dll"));


        public void Uninstall()
        {
            if (KInterceptor != null)
                KInterceptor.Uninstall();

            if (MInterceptor != null)
                MInterceptor.Uninstall();
        }
    }
}
