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
                        Console.WriteLine("-IV [126bit hex]\t\t\tSet the repack IV");
                        Console.WriteLine("-MS 0x20000\t\t\t\tSet the NPK section Size");
                        Console.WriteLine("-EC UTF8\t\t\t\tSet the NPK filename encoding");
                        Console.WriteLine("-KY [256bit hex]\t\t\tSet a custom encyption key");
                        Console.WriteLine("-GM 0\t\t\t\t\tSet the NPK Game ID");
                        Console.WriteLine();
                        Console.WriteLine("Valid Game IDs:");
                        for (int x = 0; x < Games.Keys.Count; x++) {
                            Console.WriteLine($"{x}: {Games.Keys.ElementAt(x)}");
                        }

                        Console.WriteLine();
                        Console.WriteLine("It's hard?");
                        Console.WriteLine("... then just Drag&Drop");
                        Console.ReadKey();
                        break;
                    case "u":
                        EnsureGameSelection();
                        NPK3.Unpack(args[++i], args[++i]);
                        break;
                    case "r":
                        EnsureGameSelection();
                        NPK3.Repack(args[++i], args[++i]);
                        break;
                    case "iv":
                        NPK3.SetIV(args[++i]);
                        break;
                    case "ms":
                        NPK3.SetMaxSectionSize(args[++i]);
                        break;
                    case "ec":
                        NPK3.SetEncoding(args[++i]);
                        break;
                    case "ky":
                        NPK3.SetKey(args[++i]);
                        break;
                    case "gm":
                        if (int.TryParse(args[++i], out int GM))
                            NPK3.CurrentKey = Games.Values.ElementAt(GM);
                        break;
                    default:
                        if (File.Exists(args[i])) {
                            EnsureGameSelection();
                            NPK3.Unpack(args[i]);
                        }
                        else if (Directory.Exists(args[i])) {
                            EnsureGameSelection();
                            NPK3.Repack(args[i]);
                        }
                        break;
                }
            }
        }

        static void EnsureGameSelection() {
            if (NPK3.CurrentKey != null)
                return;

            int Current = 0;
            if (Games.Count > 1) {
                foreach (var Game in Games)
                {
                    Console.WriteLine($"Type {Current++} to \"{Game.Key}\"");
                }

                while (!int.TryParse(Console.ReadLine(), out Current))
                    continue;
            }

            Console.WriteLine($"Game \"{Games.Keys.ElementAt(Current)}\" Selected");
            NPK3.CurrentKey = Games.Values.ElementAt(Current);
        }

        static Dictionary<string, byte[]> Games = new Dictionary<string, byte[]>()
        {
            { "You and Me and Her",  new byte[] {
                0xE7, 0xE8, 0xA5, 0xF9, 0x9B, 0xAF, 0x7C, 0x73, 0xAE, 0x6B, 0xDF, 0x3D, 0x8C, 0x90, 0x26, 0x2F,
                0xF2, 0x50, 0x25, 0xA1, 0x2D, 0xB5, 0x39, 0xF9, 0xCF, 0xD6, 0xE8, 0xE5, 0x79, 0x75, 0xB7, 0x98
            } }
        };
    }
}
