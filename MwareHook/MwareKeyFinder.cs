using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Iced.Intel;
using StringReloads.Engine;
using StringReloads.Engine.Interface;

namespace MwareHook
{
    unsafe class MwareKeyFinder : IMod
    {
        KeyInterceptor Interceptor;
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

                Info = ModuleInfo.GetCodeInfo(MainModule.BaseAddress.ToPointer());
                if (!Scan(out Address)) {
                    Info = null;
                    Log.Error("Failed to find the KeyExpander Function");
                    return;
                }
                Helper = new DiasmHelper(Address);
                var Instruction = Helper.Diassembly();
                var KeyRegister = Instruction.MemoryBase.GetFullRegister32();
                Interceptor = new KeyInterceptor(Address, KeyRegister);
                Interceptor.OnKeyIntercepted = OnKeyIntercepted;
                Log.Debug("Mware Key Interceptor Ready");
            }

            if (Interceptor != null)
                Interceptor.Install();
        }

        void OnKeyIntercepted(byte[] Key) {
            Interceptor.Uninstall();

            string KeyStr = string.Empty;
            for (int i = 0; i < Key.Length; i++) {
                KeyStr += $"0x{Key[i]:X2}, ";
            }
            KeyStr = KeyStr.TrimEnd(' ', ',');
            User.ShowMessageBox("Encryption Key Found:\n" + KeyStr, "MwareKeyFinder - By Marcussacana", User.MBButtons.Ok, User.MBIcon.Information);
        }

        private bool Scan(out void* Address) {
            Address = null;
            long CodeAdd = (long)Info.Value.CodeAddress;
            long CodeLen = Info.Value.CodeSize - Pattern.Length;
            for (int i = 0; i < CodeLen; i++) {
                byte* pBuffer = (byte*)(Info.Value.CodeAddress) + i;
                if (!CheckPattern(pBuffer, Pattern))
                    continue;
                Log.Debug($"Decryption Key Pattern Found At: 0x{(ulong)pBuffer:X8}");
                for (long x = (long)pBuffer; x > CodeAdd; x--) {
                    byte* pFunc = (byte*)x;
                    if (!CheckPattern(pFunc, FunctionHeader))
                        continue;
                    Address = pFunc + FunctionHeader.Length;
                    return true;
                }
            }
            return false;
        }

        private bool CheckPattern(byte* Buffer, byte?[] Pattern) {
            for (int i = 0; i < Pattern.Length; i++) {
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
            if (Interceptor != null)
                Interceptor.Uninstall();
        }
    }
}
