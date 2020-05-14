using AdvancedBinary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System;
using System.Diagnostics;

namespace NPK3Tool
{
    static class NPK
    {
        public static bool EnableCompression = true;
        public static bool EnableSegmentation = true;
        public static int NPKVersion = 3;
        public static uint NPKMinorVersion = 1;//Not Sure
        public static uint MaxSectionSize = 0x20000;
        public static Encoding Encoding = Encoding.UTF8;
        public static string[] DontCompress = { "png", "ogg", "jpg", "mpg" };

        public static byte[] CurrentKey;
        public static byte[] CurrentIV = new byte[] { 0x42, 0x79, 0x20, 0x4D, 0x61, 0x72, 0x63, 0x75, 0x73, 0x73, 0x61, 0x63, 0x61, 0x6E, 0x61, 0x00 };

        public static void Repack(string InputDirectory, string OutNPK = null) {
            InputDirectory = Path.GetFullPath(InputDirectory);
            if (!InputDirectory.EndsWith(Path.DirectorySeparatorChar) && !InputDirectory.EndsWith(Path.AltDirectorySeparatorChar))
                InputDirectory += Path.DirectorySeparatorChar;

            if (OutNPK == null) {
                OutNPK = InputDirectory.TrimEnd('\\', '/', '~');
                OutNPK = Path.Combine(Path.GetDirectoryName(OutNPK), Path.GetFileNameWithoutExtension(OutNPK) + "_New.npk");
            }

            string[] FilesPath = Directory.GetFiles(InputDirectory, "*.*", SearchOption.AllDirectories);
            string[] RelativeFiles = (from x in FilesPath select x.Substring(InputDirectory.Length)).ToArray();
            Stream[] FilesData = (from x in FilesPath select File.Open(x, FileMode.Open)).ToArray();

            using (Stream Output = File.Create(OutNPK)) {
                switch (NPKVersion) {
                    case 3:
                        Output.WriteUIn32(0x334B504Eu);//NPK3
                        break;
                    case 2:
                        Output.WriteUIn32(0x324B504Eu);//NPK2
                        break;
                    default:
                        throw new NotSupportedException("NPK Version Not Supported");
                }
                Output.WriteUIn32(NPKMinorVersion);

                Output.WriteBytes(CurrentIV);
                Output.WriteUIn32((uint)FilesPath.Length);

                var Entries = CreateInitialEntries(RelativeFiles, FilesData);

                uint TableSize;
                using (Stream TBuilder = BuildEntries(Entries))
                using (Stream TEncryptor = TBuilder.CreateEncryptor(CurrentKey, CurrentIV))
                using (Stream TBuffer = TEncryptor.ToMemory())
                    TableSize = (uint)TBuffer.Length;

                Output.WriteUIn32(TableSize);

                long TablePos = Output.Position;

                Output.WriteBytes(new byte[TableSize]);

                for (int i = 0; i < FilesData.Length; i++) {
                    Console.WriteLine($"Writing File: {RelativeFiles[i]}");
                    string Ext = Path.GetExtension(FilesPath[i]).ToLower().TrimStart('.');
                    bool Compress = EnableCompression && !DontCompress.Contains(Ext);

                    long ReadPos = 0;
                    for (int x = 0; x < Entries[i].SegmentsInfo.Length; x++) {
                        var SegmentData = FilesData[i].CreateStream(ReadPos, Entries[i].SegmentsInfo[x].DecompressedSize);
                        ReadPos += SegmentData.Length;

                        //Compress only if the compressed data is smaller
                        var Stream = Compress ? SegmentData.Compress(NPKVersion) : SegmentData;
                        if (Stream.Length >= Entries[i].SegmentsInfo[x].DecompressedSize) 
                            Stream = SegmentData;

                        using var Crypted = Stream.CreateEncryptor(CurrentKey, CurrentIV).ToMemory();

                        Entries[i].SegmentsInfo[x].RealSize = (uint)Stream.Length;
                        Entries[i].SegmentsInfo[x].AlignedSize = (uint)Crypted.Length;
                        Entries[i].SegmentsInfo[x].Offset = (uint)Output.Position;

                        Crypted.CopyTo(Output);
                    }
                }

                Output.Position = TablePos;

                Console.WriteLine("Writing File Index...");

                using var TableData = BuildEntries(Entries);
                using var TableEncryptor = TableData.CreateEncryptor(CurrentKey, CurrentIV);
                using var OutTableData = TableEncryptor.ToMemory();
                Debug.Assert(OutTableData.Length == TableSize);
                OutTableData.CopyTo(Output);
            }
        }

        public static void SetIV(string IVHex) {
            IVHex = IVHex.Trim().Replace(" ", "");
            if (IVHex.Length != CurrentIV.Length * 2) {
                Console.WriteLine("Warning: Invalid IV");
                return;
            }
            for (int x = 0; x < IVHex.Length; x += 2) {
                string HByte = IVHex.Substring(x, 2);
                CurrentIV[x / 2] = Convert.ToByte(HByte, 16);
            }
        }
        public static void SetKey(string KeyHex) {
            CurrentKey = new byte[0x20];
            KeyHex = KeyHex.Trim().Replace(" ", "");
            if (KeyHex.Length != CurrentKey.Length * 2) {
                Console.WriteLine("Warning: Invalid KEY");
                return;
            }
            for (int x = 0; x < KeyHex.Length; x += 2) {
                string HByte = KeyHex.Substring(x, 2);
                CurrentKey[x / 2] = Convert.ToByte(HByte, 16);
            }
        }

        public static void SetEncoding(string Name) {
            Encoding = Name.ToEncoding();
        }

        public static void SetMaxSectionSize(string MaxSize) {
            MaxSize = MaxSize.Trim();
            if (MaxSize.ToLower().StartsWith("0x"))
            {
                MaxSize = MaxSize.Substring(2);
                MaxSectionSize = uint.Parse(MaxSize, System.Globalization.NumberStyles.HexNumber);
            }
            else
                MaxSectionSize = uint.Parse(MaxSize);
        }

        public static NPK3Entry[] CreateInitialEntries(string[] Files, Stream[] Streams) {
            Console.WriteLine("Loading Files...");

            List<NPK3Entry> Entries = new List<NPK3Entry>();
            for (int i = 0; i < Files.Length; i++) {
                NPK3Entry Entry = new NPK3Entry();
                Entry.FilePath = Files[i].Replace("\\", "/");
                Entry.FileSize = (uint)Streams[i].Length;
                Entry.SHA256 = Streams[i].SHA256Checksum();

                long Reaming = Entry.FileSize;
                if (EnableSegmentation)
                {
                    Entry.SegmentsInfo = new NPKSegmentInfo[1 + (Entry.FileSize / MaxSectionSize)];
                    for (int x = 0; x < Entry.SegmentsInfo.Length; x++)
                    {
                        uint MaxBytes = Reaming < MaxSectionSize ? (uint)Reaming : MaxSectionSize;
                        Entry.SegmentsInfo[x] = new NPKSegmentInfo()
                        {
                            Offset = 0,
                            DecompressedSize = MaxBytes,
                            RealSize = MaxBytes,
                            AlignedSize = MaxBytes + (0x10 - (MaxBytes % 0x10))
                        };

                        Reaming -= MaxBytes;
                    }
                }
                else
                {
                    Entry.SegmentationMode = 1;
                    Entry.SegmentsInfo = new NPKSegmentInfo[] {
                        new NPKSegmentInfo(){
                            Offset = 0,
                            DecompressedSize = (uint)Reaming,
                            RealSize = (uint)Reaming,
                            AlignedSize = (uint)Reaming + (0x10 - ((uint)Reaming % 0x10))
                        }
                    };
                }

                Entries.Add(Entry);
            }

            return Entries.ToArray();
        }

        public static Stream BuildEntries(NPK3Entry[] Entries) {
            Stream Output = new MemoryStream();
            StructWriter Writer = new StructWriter(Output, Encoding: Encoding);
            for (int i = 0; i < Entries.Length; i++) {
                var Entry = Entries[i];
                Writer.WriteStruct(ref Entry);
            }
            Output.Position = 0;
            return Output;
        }

        public static void Unpack(string Package, string OutDir = null)
        {
            if (OutDir == null)
                OutDir = Path.Combine(Path.GetDirectoryName(Package), Path.GetFileName(Package) + "~");

            using (Stream NPK = File.Open(Package, FileMode.Open))
            {
                switch (NPK.ReadUInt32(0)) {
                    case 0x334B504Eu:
                        NPKVersion = 3;
                        break;
                    case 0x324B504Eu:
                        NPKVersion = 2;
                        break;
                    default:
                        throw new NotSupportedException("NPK Version Not Supported");
                }

                CurrentIV = NPK.ReadBytes(8, 0x10);
                var Table = GetEntryTable(NPK);
                var Entries = GetEntries(Table);

                foreach (var Entry in Entries)
                {
                    Console.WriteLine($"Extracting File: {Entry.FilePath}");
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
                                    var Decompressor = Buffer.CreateDecompressor(NPKVersion);
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
        public static NPK3Entry[] GetEntries(Stream EntryTable) {
			List<NPK3Entry> Entries = new List<NPK3Entry>();
			StructReader Reader = new StructReader(EntryTable, Encoding: Encoding);
			while (Reader.BaseStream.Position + 1 < Reader.BaseStream.Length) {
				var Entry = new NPK3Entry();
				Reader.ReadStruct(ref Entry);
				Entries.Add(Entry);
			}
			return Entries.ToArray();
		}

		public static Stream GetEntryTable(Stream Package) {
			uint TableSize = Package.ReadUInt32(0x1C);
			var CryptedTable = Package.CreateStream(0x20, TableSize);
			return CryptedTable.CreateDecryptor(CurrentKey, CurrentIV).ToMemory();
		}
    }
#pragma warning disable 0219, 0649
	struct NPK3Entry
	{
		public byte SegmentationMode;//0 = With Segmentation, 1 = Without Segmentation

		[PString(PrefixType = Const.UINT16)]
		public string FilePath;

		public uint FileSize;

		[FArray(Length = 0x20)]
		public byte[] SHA256;

		[PArray(PrefixType = Const.UINT32), StructField]
		public NPKSegmentInfo[] SegmentsInfo;
	}

	struct NPKSegmentInfo {
		public long Offset;
		public uint AlignedSize;
		public uint RealSize;
		public uint DecompressedSize;

		[Ignore]
		public bool IsCompressed => RealSize < DecompressedSize;
	}
#pragma warning restore 0219, 0649
}
