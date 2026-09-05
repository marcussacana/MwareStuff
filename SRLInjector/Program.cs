using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using VNX;

namespace SRLInjector
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "SRLInjector - By Marcussacana";
            if (args == null || args.Length == 0 || !File.Exists(args.First()))
            {
                Console.WriteLine("Drag&Drop the Game Executable");
                WaitKey();
                return;
            }

            var InjectorDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string FullExePath = Path.GetFullPath(args.First());
            string Dir = Path.GetDirectoryName(FullExePath);

            bool is64 = Is64Bit(FullExePath);

            if (is64 && !Environment.Is64BitProcess)
            {
                string x64Exe = Path.Combine(InjectorDirectory, "SRLInjectorX64.exe");
                if (File.Exists(x64Exe))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = x64Exe,
                        Arguments = string.Join(" ", args.Select(a => $"\"{a}\"")),
                        UseShellExecute = false,
                        WorkingDirectory = Environment.CurrentDirectory
                    };
                    var proc = Process.Start(psi);
                    proc.WaitForExit();
                    Environment.ExitCode = proc.ExitCode;
                    return;
                }
                else
                {
                    var oriColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("The target game is 64-bit, but SRLInjectorX64.exe was not found in the current directory!");
                    Console.ForegroundColor = oriColor;
                    WaitKey();
                    return;
                }
            }

            if (!is64 && Environment.Is64BitProcess)
            {
                string x86Exe = Path.Combine(InjectorDirectory, "SRLInjector.exe");
                if (File.Exists(x86Exe))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = x86Exe,
                        Arguments = string.Join(" ", args.Select(a => $"\"{a}\"")),
                        UseShellExecute = false,
                        WorkingDirectory = Environment.CurrentDirectory
                    };
                    var proc = Process.Start(psi);
                    proc.WaitForExit();
                    Environment.ExitCode = proc.ExitCode;
                    return;
                }
                else
                {
                    var oriColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("The target game is 32-bit, but SRLInjector.exe was not found in the current directory!");
                    Console.ForegroundColor = oriColor;
                    WaitKey();
                    return;
                }
            }

            string[] SRLPaths = is64
                ? new string[] { "SRLx64.dll", "SRLWrapper.dll" }
                : new string[] { "SRLWrapper.dll", "SRLx32.dll" };

            string SRLPath = null;
            foreach (var SRL in SRLPaths)
            {
                var FPath = Path.Combine(InjectorDirectory, SRL);
                if (File.Exists(FPath))
                {
                    SRLPath = FPath;
                    break;
                }
            }

            if (SRLPath == null)
            {
                Console.WriteLine($"SRL ({(is64 ? "SRLx64.dll" : "SRLx32.dll")}) Not Found in the Current Directory");
                WaitKey();
                return;
            }

            if (!File.Exists(Path.Combine(InjectorDirectory, "SRL.ini")))
            {
                Console.WriteLine("SRL.ini Not Found in the Current Directory");
                WaitKey();
                return;
            }

            if (!File.Exists(Path.Combine(InjectorDirectory, "Plugins", "MwareHook.dll")))
            {
                Console.WriteLine("MwareHook.dll Not Found in the \"Plugins\" Directory");
                WaitKey();
                return;
            }


            var Exe = File.ReadAllBytes(FullExePath);
            var SteamStub = new byte[] { 0x2E, 0x62, 0x69, 0x6E, 0x64 };
            for (int i = 0; i < Exe.Length; i++)
            {
                bool Protected = EqualsAt(Exe, SteamStub, i);
                if (Protected)
                {
                    var OriForeColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("This Game is protected with the Steam Stub DRM\nTo the Key Finder works you must crack it before.");
                    Console.ForegroundColor = OriForeColor;
                    WaitKey();
                    return;
                }
            }


            if (!File.Exists(Path.Combine(Dir, "SRL.ini")))
                File.Copy(Path.Combine(InjectorDirectory, "SRL.ini"), Path.Combine(Dir, "SRL.ini"));

            RemoteControl Control = new RemoteControl(args.First(), out Process Game, WorkingDirectory: Dir);
            Control.WaitInitialize();
            Control.LockEntryPoint();
            Control.Invoke(SRLPath, "Process", IntPtr.Zero);
            Control.UnlockEntryPoint();
        }

        private static bool EqualsAt(byte[] ArrA, byte[] ArrB, int Index)
        {
            if (Index + ArrB.Length >= ArrA.Length)
                return false;

            for (int i = 0; i < ArrB.Length; i++)
                if (ArrA[i + Index] != ArrB[i])
                    return false;

            return true;
        }

        private static bool Is64Bit(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var br = new BinaryReader(fs))
            {
                fs.Seek(0x3c, SeekOrigin.Begin);
                int peOffset = br.ReadInt32();
                fs.Seek(peOffset, SeekOrigin.Begin);
                uint peHead = br.ReadUInt32();
                if (peHead != 0x00004550) // "PE\0\0"
                    throw new Exception("Invalid PE header");

                ushort machine = br.ReadUInt16();
                return machine == 0x8664 || machine == 0x0200; // AMD64 or IA64
            }
        }

        private static void WaitKey()
        {
            try
            {
                Console.ReadKey();
            }
            catch { }
        }
    }
}
