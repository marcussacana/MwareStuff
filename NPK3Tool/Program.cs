using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NPK3Tool
{
    static class Program
    {
        static string CurrentExe => Path.GetFileName(Assembly.GetExecutingAssembly().Location);
        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Console.Title = "NPK3Tool - By Marcussacana";

            if (args == null || args.Length == 0)
                args = new[] { "-h" };

            for (int i = 0; i < args.Length; i++) {
                var flag = args[i].ToLower().TrimStart('-', '/', '\\');
                switch (flag) {
                    case "h":
                    case "?":
                    case "help":
                        Console.WriteLine("Usage:");
                        Console.WriteLine($"{CurrentExe} [Options] -u Input.npk OutDir");
                        Console.WriteLine($"{CurrentExe} [Options] -r InputDir Output.npk");
                        Console.WriteLine();
                        Console.WriteLine("Options:");
                        Console.WriteLine("-IV [128bit hex]\t\t\tSet the repack IV");
                        Console.WriteLine("-MS 0x20000\t\t\t\tSet the NPK section Size");
                        Console.WriteLine("-EC UTF8\t\t\t\tSet the NPK filename encoding");
                        Console.WriteLine("-KY [256bit hex]\t\t\tSet a custom encyption key");
                        Console.WriteLine("-VS [3/2]\t\t\t\tSet the NPK repack version");
                        Console.WriteLine("-GM 0\t\t\t\t\tSet the NPK Game ID");
                        Console.WriteLine();
                        Console.WriteLine("Valid Game IDs:");
                        for (int x = 0; x < Games.Length; x++) {
                            Console.WriteLine($"{x}: {Games[x].Item1}");
                        }

                        Console.WriteLine();
                        Console.WriteLine("It's hard?");
                        Console.WriteLine("... then just Drag&Drop");
                        Console.ReadKey();
                        break;
                    case "u":
                        EnsureGameSelection();
                        NPK.Unpack(args[++i], args[++i]);
                        break;
                    case "r":
                        EnsureGameSelection();
                        NPK.Repack(args[++i], args[++i]);
                        break;
                    case "iv":
                        NPK.SetIV(args[++i]);
                        break;
                    case "ms":
                        NPK.SetMaxSectionSize(args[++i]);
                        break;
                    case "ec":
                        NPK.SetEncoding(args[++i]);
                        break;
                    case "ky":
                        NPK.SetKey(args[++i]);
                        break;
                    case "gm":
                        if (int.TryParse(args[++i], out int GM)) {
                            NPK.CurrentKey = Games[GM].Item2;
                            NPK.Encoding = Games[GM].Item3;
                            NPK.NPKVersion = Games[GM].Item4;
                        }
                        break;
                    default:
                        if (File.Exists(args[i])) {
                            EnsureGameSelection();
                            NPK.Unpack(args[i]);
                        }
                        else if (Directory.Exists(args[i])) {
                            EnsureGameSelection();
                            NPK.Repack(args[i]);
                        }
                        break;
                }
            }
        }

        static void EnsureGameSelection() {
            if (NPK.CurrentKey != null)
                return;

            int Current = 0;
            if (Games.Length > 1) {
                foreach (var Game in Games)
                {
                    Console.WriteLine($"Type {Current++} to \"{Game.Item1}\"");
                }

                while (!int.TryParse(Console.ReadLine(), out Current))
                    continue;
            }

            Console.WriteLine($"Game \"{Games[Current].Item1}\" Selected");
            NPK.CurrentKey = Games[Current].Item2;
            NPK.Encoding = Games[Current].Item3;
            NPK.NPKVersion = Games[Current].Item4;
        }

        //Name, Key, Encoding, NPKVersion
        readonly static (string, byte[], Encoding, int)[] Games = new[] {
            ("You and Me and Her",  new byte[] {
                0xE7, 0xE8, 0xA5, 0xF9, 0x9B, 0xAF, 0x7C, 0x73, 0xAE, 0x6B, 0xDF, 0x3D, 0x8C, 0x90, 0x26, 0x2F,
                0xF2, 0x50, 0x25, 0xA1, 0x2D, 0xB5, 0x39, 0xF9, 0xCF, 0xD6, 0xE8, 0xE5, 0x79, 0x75, 0xB7, 0x98
            }, Encoding.UTF8, 3),
           ("Tokyo Necro", new byte[] {
                0x96, 0x2C, 0x5F, 0x3A, 0x78, 0x9C, 0x84, 0x37, 0xB7, 0x12, 0x12, 0xA1, 0x15, 0xD6, 0xCA, 0x9F,
                0x9A, 0xE3, 0xFD, 0x21, 0x0F, 0xF6, 0xAF, 0x70, 0xA8, 0xA8, 0xF8, 0xBB, 0xFE, 0x5E, 0x8A, 0xF5
            }, Encoding.GetEncoding(932), 2),
           ("Minikui Mojika no Ko", new byte[] {
               0xAA, 0x45, 0x60, 0xF7, 0x83, 0xF7, 0x8A, 0x90, 0x20, 0x5D, 0xC1, 0x4E, 0x54, 0x09, 0x67, 0x04,
               0x09, 0xBC, 0x00, 0x46, 0x39, 0x17, 0x5A, 0xD9, 0xC0, 0xB3, 0xD2, 0x97, 0xDA, 0x2F, 0x38, 0x68
           }, Encoding.UTF8, 2)
        };
    }
}
