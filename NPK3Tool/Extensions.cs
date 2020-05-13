using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Zstandard.Net;

namespace NPK3Tool
{
    static class Extensions
    {

        public static byte[] ReadBytes(this Stream Stream, int Pos, int Count)
        {
            byte[] Buffer = new byte[Count];
            Stream.Position = Pos;
            Stream.Read(Buffer, 0, Count);
            return Buffer;
        }

        public static void WriteBytes(this Stream Stream, byte[] Buffer) {
            Stream.Write(Buffer, 0, Buffer.Length);
        }

        public static uint ReadUInt32(this Stream Stream, int Pos)
        {
            return BitConverter.ToUInt32(Stream.ReadBytes(Pos, 4), 0);
        }

        public static void WriteUIn32(this Stream Stream, uint Value) {
            Stream.Write(BitConverter.GetBytes(Value), 0, 4);
        }

        public static Stream CreateStream(this Stream Stream, long Pos, uint Size)
        {
            return new VirtStream(Stream, Pos, Size);
        }

        public static Stream CreateDecryptor(this Stream Stream, byte[] Key, byte[] IV)
        {
            var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = Key;
            if (IV != null)
                aes.IV = IV;
            var decryptor = aes.CreateDecryptor();
            return new CryptoStream(Stream, decryptor, CryptoStreamMode.Read);
        }

        public static Stream CreateEncryptor(this Stream Stream, byte[] Key, byte[] IV)
        {
            var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = Key;
            if (IV != null)
                aes.IV = IV;
            var encryptor = aes.CreateEncryptor();
            return new CryptoStream(Stream, encryptor, CryptoStreamMode.Read);
        }

        public static Stream CreateDecompressor(this Stream Stream, int NpkVersion)
        {
            switch (NpkVersion)
            {
                case 3:
                    return new ZstandardStream(Stream, CompressionMode.Decompress, true);
                case 2:
                    return new DeflateStream(Stream, CompressionMode.Decompress, true);
                default:
                    throw new NotSupportedException("NPK Version Not Supported");
            }
        }
        public static Stream Compress(this Stream Stream, int NpkVersion)
        {
            MemoryStream Compressed = new MemoryStream();
            switch (NpkVersion) {
                case 3:
                    using (var Compressor = new ZstandardStream(Compressed, CompressionMode.Compress, true))
                    {
                        Stream.CopyTo(Compressor);
                        Compressor.Close();
                        Compressed.Position = 0;
                        Stream.Position = 0;
                        return Compressed;
                    }
                case 2:
                    using (var Compressor = new DeflateStream(Compressed, CompressionLevel.Optimal, true))
                    {
                        Stream.CopyTo(Compressor);
                        Compressor.Close();
                        Compressed.Position = 0;
                        Stream.Position = 0;
                        return Compressed;
                    }
                default:
                        throw new NotSupportedException("NPK Version Not Supported");

            }
        }

        public static Stream ToMemory(this Stream Stream)
        {
            var NewStream = new MemoryStream();
            Stream.CopyTo(NewStream);
            NewStream.Position = 0;
            return NewStream;
        }
        public static byte[] SHA256Checksum(this Stream Stream)
        {
            long OriPos = Stream.Position;
            using SHA256 SHA256 = SHA256.Create();
            var Data = SHA256.ComputeHash(Stream);
            Stream.Position = OriPos;
            return Data;
        }
        public static Encoding ToEncoding(this string Value)
        {
            if (int.TryParse(Value, out int CP))
                return Encoding.GetEncoding(CP);

            return Value.ToLowerInvariant() switch
            {
                "sjis" => Encoding.GetEncoding(932),
                "shiftjis" => Encoding.GetEncoding(932),
                "shift-jis" => Encoding.GetEncoding(932),
                "unicode" => Encoding.Unicode,
                "utf16" => Encoding.Unicode,
                "utf16be" => Encoding.BigEndianUnicode,
                "utf16wb" => new UnicodeEncoding(false, true),
                "utf16wbbe" => new UnicodeEncoding(true, true),
                "utf16bewb" => new UnicodeEncoding(true, true),
                "utf8" => Encoding.UTF8,
                "utf8wb" => new UTF8Encoding(true),
                "utf7" => Encoding.UTF7,
                _ => Encoding.GetEncoding(Value)
            };
        }
    }
}
