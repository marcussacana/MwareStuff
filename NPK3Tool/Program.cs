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
        //The "MinorVersion" is just something that I suposed, and i'm not sure, maybe that is just a flag
        static string CurrentExe => Path.GetFileName(Assembly.GetExecutingAssembly().Location);
        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Console.Title = "NPK3Tool - By Marcussacana";

            if (args == null || args.Length == 0)
                args = new[] { "-h" };

            for (int i = 0; i < args.Length; i++)
            {
                var flag = args[i].ToLower().TrimStart('-', '/', '\\');
                switch (flag)
                {
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
                        Console.WriteLine("-MV [1/2]\t\t\t\tSet the NPK repack minor version");
                        Console.WriteLine("-SG [1/0]\t\t\t\tEnable/Disable NPK Segmentation");
                        Console.WriteLine("-CP [1/0]\t\t\t\tEnable/Disable NPK Compression");
                        Console.WriteLine("-GM 0\t\t\t\t\tSet the NPK Game ID");
                        Console.WriteLine();
                        Console.WriteLine("Valid Game IDs:");
                        for (int x = 0; x < Games.Length; x++)
                        {
                            Console.WriteLine($"{x}: {Games[x].Game}");
                        }

                        Console.WriteLine();
                        Console.WriteLine("It's hard?");
                        Console.WriteLine("... then just Drag&Drop");
                        Console.WriteLine();
                        Console.WriteLine("Debug:");
                        Console.WriteLine("-DumpTable Input.npk\t\t\tDump File Table from the NPK");
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
                    case "vs":
                        if (int.TryParse(args[++i], out int VS))
                        {
                            if (VS != 2 && VS != 3)
                            {
                                Console.WriteLine("Unsupported NPK Version");
                                continue;
                            }
                            NPK.NPKVersion = VS;
                        }
                        break;
                    case "mv":
                        if (uint.TryParse(args[++i], out uint MV))
                            NPK.NPKMinorVersion = MV;
                        break;
                    case "sg":
                        string SG = args[++i].Trim().ToLower();
                        NPK.EnableSegmentation = SG == "1" || SG == "true" || SG == "yes" || SG == "y";
                        break;
                    case "cp":
                        string CP = args[++i].Trim().ToLower();
                        NPK.EnableCompression = CP == "1" || CP == "true" || CP == "yes" || CP == "y";
                        break;
                    case "gm":
                        if (int.TryParse(args[++i], out int GM))
                        {
                            NPK.CurrentKey = Games[GM].Item2;
                            NPK.Encoding = Games[GM].Item3;
                            NPK.NPKVersion = Games[GM].Item4;
                            NPK.EnableSegmentation = Games[GM].Item5;
                            NPK.NPKMinorVersion = Games[GM].Item6;
                        }
                        break;
                    case "dumptable":
                        EnsureGameSelection();
                        using (var Input = File.Open(args[++i], FileMode.Open))
                        using (var Output = File.Create(args[i] + ".tbl"))
                        {
                            NPK.CurrentIV = Input.ReadBytes(8, 0x10);
                            var Table = NPK.GetEntryTable(Input);
                            Table.Position = 0;
                            Table.CopyTo(Output);
                        }
                        break;
                    default:
                        if (File.Exists(args[i]))
                        {
                            EnsureGameSelection();
                            NPK.Unpack(args[i]);
                        }
                        else if (Directory.Exists(args[i]))
                        {
                            EnsureGameSelection();
                            NPK.Repack(args[i]);
                        }
                        break;
                }
            }
        }

        static void EnsureGameSelection()
        {
            if (NPK.CurrentKey != null)
                return;

            int Current = 0;
            if (Games.Length > 1)
            {
                foreach (var Game in Games)
                {
                    Console.WriteLine($"Type {Current++} to \"{Game.Game}\"");
                }

                while (!int.TryParse(Console.ReadLine(), out Current))
                    continue;
            }

            Console.WriteLine($"Game \"{Games[Current].Game}\" Selected");
            NPK.CurrentKey = Games[Current].Key;
            NPK.Encoding = Games[Current].Encoding;
            NPK.NPKVersion = Games[Current].NPKVersion;
            NPK.EnableSegmentation = Games[Current].NPKSegmentation;
            NPK.NPKMinorVersion = Games[Current].NPKMinorVersion;
        }

        //Name, Key, Encoding, NPKVersion, Segmentation, MinorVersion
        readonly static (string Game, byte[] Key, Encoding Encoding, int NPKVersion, bool NPKSegmentation, uint NPKMinorVersion)[] Games = new[] {
            ("You and Me and Her (Jast USA)", new byte[] {
                0xE7, 0xE8, 0xA5, 0xF9, 0x9B, 0xAF, 0x7C, 0x73, 0xAE, 0x6B, 0xDF, 0x3D, 0x8C, 0x90, 0x26, 0x2F,
                0xF2, 0x50, 0x25, 0xA1, 0x2D, 0xB5, 0x39, 0xF9, 0xCF, 0xD6, 0xE8, 0xE5, 0x79, 0x75, 0xB7, 0x98
            }, Encoding.UTF8            , 3, true , 1u),
            ("You and Me and Her (Steam)", new byte[] {
                0xF8, 0x37, 0x0F, 0x24, 0xCA, 0x4E, 0x84, 0x4C, 0x6E, 0xEB, 0xF8, 0xB8, 0x60, 0x19, 0x5B, 0x6D,
                0x72, 0x26, 0xB0, 0x7D, 0x20, 0x1F, 0x40, 0x31, 0x9C, 0xBC, 0x11, 0x1D, 0x96, 0xE1, 0xC1, 0x31
            }, Encoding.UTF8            , 3, true , 1u),
           ("Tokyo Necro", new byte[] {
                0x96, 0x2C, 0x5F, 0x3A, 0x78, 0x9C, 0x84, 0x37, 0xB7, 0x12, 0x12, 0xA1, 0x15, 0xD6, 0xCA, 0x9F,
                0x9A, 0xE3, 0xFD, 0x21, 0x0F, 0xF6, 0xAF, 0x70, 0xA8, 0xA8, 0xF8, 0xBB, 0xFE, 0x5E, 0x8A, 0xF5
            }, Encoding.GetEncoding(932), 2, false, 1u),
           ("Minikui Mojika no Ko", new byte[] {
               0xAA, 0x45, 0x60, 0xF7, 0x83, 0xF7, 0x8A, 0x90, 0x20, 0x5D, 0xC1, 0x4E, 0x54, 0x09, 0x67, 0x04,
               0x09, 0xBC, 0x00, 0x46, 0x39, 0x17, 0x5A, 0xD9, 0xC0, 0xB3, 0xD2, 0x97, 0xDA, 0x2F, 0x38, 0x68
           }, Encoding.UTF8            , 2, false, 2u),
           ("SoniComi (JastUSA)", new byte[] {
               0x65, 0xAB, 0xB4, 0xA8, 0xCD, 0xE0, 0xC8, 0x10, 0xBB, 0x4A, 0x26, 0x72, 0x37, 0x54, 0xC3, 0xA7,
               0xE4, 0x3D, 0xE9, 0xEA, 0x7F, 0x5B, 0xB8, 0x43, 0x50, 0x1D, 0x05, 0xAB, 0xCF, 0x08, 0xD9, 0xC1
           }, Encoding.GetEncoding(932), 2, false, 1u),
           ("The Song of Saya (Steam)", new byte[] {
               0x76, 0x3A, 0x14, 0x33, 0x8B, 0x0D, 0xAC, 0x04, 0x0A, 0xCC, 0xFC, 0x13, 0x85, 0x1C, 0xFA, 0xCB,
               0xB2, 0x0B, 0x00, 0x0B, 0x01, 0xF8, 0x68, 0x48, 0x6B, 0x46, 0x0F, 0x8C, 0x34, 0xD4, 0x2A, 0x96
           }, Encoding.GetEncoding(932), 2, false, 2u),
           ("The Song of Saya (Steam) [+18]", new byte[] {
               0xD0, 0xB7, 0x1F, 0x3C, 0x4E, 0x24, 0xCE, 0xCF, 0xDD, 0xEE, 0xA9, 0x1D, 0x24, 0xB0, 0x40, 0x32,
               0x29, 0xA3, 0xE5, 0x33, 0x0D, 0x29, 0x51, 0x82, 0x60, 0x51, 0xD6, 0xC9, 0x4A, 0xF5, 0xAF, 0x54
           }, Encoding.GetEncoding(932), 2, false, 2u)
        };
    }
}
