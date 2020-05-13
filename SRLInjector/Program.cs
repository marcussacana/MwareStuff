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
            if (args == null || args.Length == 0 || !File.Exists(args.First())) {
                Console.WriteLine("Drag&Drop the Game Executable");
                Console.ReadKey();
                return;
            }

            var InjectorDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            string[] SRLPaths = new string[] { "SRLWrapper.dll", "SRLx32.dll" };
            string SRLPath = null;
            foreach (var SRL in SRLPaths) {
                var FPath = Path.Combine(InjectorDirectory, SRL);
                if (File.Exists(FPath)) {
                    SRLPath = FPath;
                    break;
                }
            }

            if (SRLPath == null) {
                Console.WriteLine("SRL Not Found in the Current Directory");
                Console.ReadKey();
                return;
            }

            if (!File.Exists(Path.Combine(InjectorDirectory, "SRL.ini"))) {
                Console.WriteLine("SRL.ini Not Found in the Current Directory");
                Console.ReadKey();
                return;
            }

            if (!File.Exists(Path.Combine(InjectorDirectory, "Plugins", "MwareHook.dll")))
            {
                Console.WriteLine("MwareHook.dll Not Found in the \"Plugins\" Directory");
                Console.ReadKey();
                return;
            }

            string FullExePath = Path.GetFullPath(args.First());
            string Dir = Path.GetDirectoryName(FullExePath);

            if (!File.Exists(Path.Combine(Dir, "SRL.ini")))
                File.Copy(Path.Combine(InjectorDirectory, "SRL.ini"), Path.Combine(Dir, "SRL.ini"));

            RemoteControl Control = new RemoteControl(args.First(), out Process Game, WorkingDirectory: Dir);
            Control.WaitInitialize();
            Control.LockEntryPoint();
            Control.Invoke(SRLPath, "Process", IntPtr.Zero);
            Control.UnlockEntryPoint();
        }
    }
}
