using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Zstandard.Net;

namespace NPK3Tool
{
    static class Program
    {
        static string CurrentExe => Path.GetFileName(Assembly.GetExecutingAssembly().Location);
        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Console.Title = "NPK3Tool - By Marcussacana";

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
            CurrentKey = Games.Values.ElementAt(Current);

            if (args == null || args.Length == 0)
                args = new[] { "-h" };

            for (int i = 0; i < args.Length; i++) {
                var flag = args[i].ToLower().TrimStart('-', '/', '\\');
                switch (flag) {
                    case "h":
                    case "?":
                    case "help":
                        Console.WriteLine("Usage:");
                        Console.WriteLine($"{CurrentExe} -u Input.npk OutDir");
                        Console.WriteLine($"{CurrentExe} -r InputDir Output.npk");
                        Console.WriteLine();
                        Console.WriteLine("... Or just Drag&Drop");
                        Console.ReadKey();
                        break;
                    case "u":
                        Unpack(args[++i], args[++i]);
                        break;
                    case "r":
                        i += 2;
                        Console.WriteLine("Repack Feature Not Impemented Yet!");
                        break;
                    default:
                        if (File.Exists(args[i])) {
                            Unpack(args[i]);
                        }
                        else if (Directory.Exists(args[i])) {
                            Console.WriteLine("Repack Feature Not Implemented Yet!");
                        }
                        break;
                }
            }
        }

        static Dictionary<string, byte[]> Games = new Dictionary<string, byte[]>()
        {
            { "You and Me and Her",  new byte[] {
                0xE7, 0xE8, 0xA5, 0xF9, 0x9B, 0xAF, 0x7C, 0x73, 0xAE, 0x6B, 0xDF, 0x3D, 0x8C, 0x90, 0x26, 0x2F,
                0xF2, 0x50, 0x25, 0xA1, 0x2D, 0xB5, 0x39, 0xF9, 0xCF, 0xD6, 0xE8, 0xE5, 0x79, 0x75, 0xB7, 0x98
            } }
        };

        public static void Unpack(string Package, string OutDir = null) {
            if (OutDir == null)
                OutDir = Path.Combine(Path.GetDirectoryName(Package), Path.GetFileName(Package) + "~");

            using (Stream NPK = File.Open(Package, FileMode.Open))
            {
                CurrentIV = NPK.ReadBytes(8, 0x10);
                var Table = NPK3.GetEntryTable(NPK);
                var Entries = NPK3.GetEntries(Table);

                foreach (var Entry in Entries)
                {
                    string OutPath = Path.Combine(OutDir, Entry.FilePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

                    if (!Directory.Exists(Path.GetDirectoryName(OutPath)))
                        Directory.CreateDirectory(Path.GetDirectoryName(OutPath));

                    using (Stream Output = File.Create(OutPath))
                    {
                        foreach (var Segment in Entry.SegmentsInfo)
                        {
                            long Offset = Segment.Offset;
                            uint Size = Segment.AlignedSize;

                            using (Stream Buffer = new MemoryStream())
                            {
                                var Reader = NPK.CreateStream(Offset, Size).CreateDecryptor(CurrentKey, CurrentIV);
                                Reader.CopyTo(Buffer);

                                Buffer.Position = 0;
                                if (Segment.IsCompressed) {
                                    var Decompressor = Buffer.CreateDecompressor();
                                    Decompressor.CopyTo(Output);
                                }
                                else
                                    Buffer.CopyTo(Output);
                            }
                        }
                    }
                }
            }
        }

        public static byte[] CurrentKey;
        public static byte[] CurrentIV;
        static byte[] ReadBytes(this Stream Stream, int Pos, int Count) {
            byte[] Buffer = new byte[Count];
            Stream.Position = Pos;
            Stream.Read(Buffer, 0, Count);
            return Buffer;
        }

        public static uint ReadUInt32(this Stream Stream, int Pos) {
            return BitConverter.ToUInt32(Stream.ReadBytes(Pos, 4), 0);
        }

        public static Stream CreateStream(this Stream Stream, long Pos, uint Size) {
            return new VirtStream(Stream, Pos, Size);
        }

        public static Stream CreateDecryptor(this Stream Stream, byte[] Key, byte[] IV) {
            var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = Key;
            if (IV != null)
                aes.IV = IV;
            var decryptor = aes.CreateDecryptor();
            return new CryptoStream(Stream, decryptor, CryptoStreamMode.Read);
        }

        public static Stream CreateDecompressor(this Stream Stream) {
            return new ZstandardStream(Stream, CompressionMode.Decompress, true);
        }

        public static Stream ToMemory(this Stream Stream) {
            var NewStream = new MemoryStream();
            Stream.CopyTo(NewStream);
            NewStream.Position = 0;
            return NewStream;
        }
    }
}
