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
        // Default remains the upstream repacker. Only -TM 1 enables template lookup.
        public static bool TemplateMode = false;
        public static bool EnableCompression = true;
        public static bool EnableSegmentation = true;
        public static bool ForceSegmentation = true;
        public static int NPKVersion = 3;
        public static uint NPKMinorVersion = 1;
        public static uint MaxSectionSize = 0x10000;
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

            // As upstream: the input directory controls which files are included.
            string[] FilesPath = Directory.GetFiles(InputDirectory, "*.*", SearchOption.AllDirectories);
            string[] RelativeFiles = (from x in FilesPath select x.Substring(InputDirectory.Length)).ToArray();

            // Template-only state: do not leak original header options into subsequent repacks.
            int SavedVersion = NPKVersion;
            uint SavedMinorVersion = NPKMinorVersion;
            byte[] SavedIV = CurrentIV;
            uint SavedMaxSectionSize = MaxSectionSize;
            bool SavedEnableSegmentation = EnableSegmentation;
            bool SavedForceSegmentation = ForceSegmentation;

            var OpenedFiles = new List<Stream>();
            try {
                Dictionary<string, NPK3Entry> OriginalByPath = null;
                if (TemplateMode) {
                    string Folder = InputDirectory.TrimEnd('\\', '/');
                    if (!Folder.EndsWith(".npk~", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Template mode requires a directory named <archive>.npk~.");

                    string OriginalNpkPath = Folder.Substring(0, Folder.Length - 1);
                    if (!File.Exists(OriginalNpkPath))
                        throw new FileNotFoundException("Template mode requires the original NPK next to its extracted folder.", OriginalNpkPath);
                    if (string.Equals(Path.GetFullPath(OutNPK), Path.GetFullPath(OriginalNpkPath), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Output NPK must not overwrite the original archive used as the template.");

                    using (Stream Original = File.OpenRead(OriginalNpkPath)) {
                        uint Magic = Original.ReadUInt32(0);
                        if (Magic == 0x324B504Eu) NPKVersion = 2;
                        else if (Magic == 0x334B504Eu) NPKVersion = 3;
                        else throw new InvalidDataException("Original archive is not NPK2/NPK3: " + OriginalNpkPath);

                        NPKMinorVersion = Original.ReadUInt32(4);
                        CurrentIV = Original.ReadBytes(8, 0x10);

                        // Reuse upstream's encrypted index reader; no external .tbl required.
                        using (Stream Table = GetEntryTable(Original)) {
                            NPK3Entry[] OriginalEntries = GetEntries(Table);
                            if (Original.ReadUInt32(0x18) != (uint)OriginalEntries.Length)
                                throw new InvalidDataException("Original archive index count does not match its header. Check the key and filename encoding.");

                            OriginalByPath = new Dictionary<string, NPK3Entry>(StringComparer.OrdinalIgnoreCase);
                            foreach (var Entry in OriginalEntries) {
                                if (string.IsNullOrEmpty(Entry.FilePath) || !OriginalByPath.TryAdd(Entry.FilePath.Replace('\\', '/'), Entry))
                                    throw new InvalidDataException("Invalid or duplicate path in original NPK index: " + Entry.FilePath);
                                if (Entry.SegmentsInfo == null || Entry.SegmentsInfo.Length == 0)
                                    throw new InvalidDataException("Original NPK entry has no segments: " + Entry.FilePath);
                            }

                            // Preserve the original index order for retained files, append new files.
                            var FolderFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            foreach (string FilePath in FilesPath) {
                                string Rel = FilePath.Substring(InputDirectory.Length).Replace('\\', '/');
                                if (!FolderFiles.TryAdd(Rel, FilePath))
                                    throw new InvalidDataException("Duplicate extracted path (case-insensitive): " + Rel);
                            }
                            var OrderedFiles = new List<string>();
                            var OrderedNames = new List<string>();
                            foreach (var Entry in OriginalEntries) {
                                if (FolderFiles.TryGetValue(Entry.FilePath.Replace('\\', '/'), out string PathInFolder)) {
                                    OrderedFiles.Add(PathInFolder);
                                    OrderedNames.Add(Entry.FilePath);
                                    FolderFiles.Remove(Entry.FilePath.Replace('\\', '/'));
                                }
                            }
                            foreach (var Extra in FolderFiles.OrderBy(x => x.Key, StringComparer.Ordinal)) {
                                OrderedFiles.Add(Extra.Value);
                                OrderedNames.Add(Extra.Key);
                            }
                            FilesPath = OrderedFiles.ToArray();
                            RelativeFiles = OrderedNames.ToArray();

                            // Only infer a section size when all nonfinal template chunks agree.
                            // This affects new files; existing files retain their own boundaries.
                            uint InferredSize = GetUniformOriginalSectionSize(OriginalEntries);
                            if (InferredSize > 0) MaxSectionSize = InferredSize;
                            // An archive containing only unsegmented files does not
                            // prove large NEW files should be unsegmented. Keep the
                            // selected -SG/-FG settings in that ambiguous case.
                            if (OriginalEntries.Any(x => x.SegmentationMode == 0)) {
                                EnableSegmentation = true;
                                ForceSegmentation = OriginalEntries.All(x => x.SegmentationMode == 0);
                            }
                            Console.WriteLine($"[Template] {OriginalNpkPath}: NPK{NPKVersion}, minor={NPKMinorVersion}, {OriginalEntries.Length} indexed files.");
                            Console.WriteLine($"[Template] Retained: {FilesPath.Length - FolderFiles.Count}; added: {FolderFiles.Count}; removed: {OriginalEntries.Length - (FilesPath.Length - FolderFiles.Count)}.");
                            Console.WriteLine("[Template] Using original IV, file order, existing segment layouts and compression decisions.");
                        }
                    }
                }

                // Upstream entry builder for ALL input files, including newly added ones.
                foreach (string FilePath in FilesPath)
                    OpenedFiles.Add(File.Open(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read));
                Stream[] FilesData = OpenedFiles.ToArray();
                var Entries = CreateInitialEntries(RelativeFiles, FilesData);

                // Template mode alters only existing entries' layout; sizes/hash are already
                // calculated from CURRENT files by the original CreateInitialEntries().
                if (OriginalByPath != null)
                    ApplyOriginalSegmentLayouts(Entries, OriginalByPath);

                using (Stream Output = File.Create(OutNPK)) {
                    switch (NPKVersion) {
                        case 3: Output.WriteUIn32(0x334B504Eu); break;
                        case 2: Output.WriteUIn32(0x324B504Eu); break;
                        default: throw new NotSupportedException("NPK Version Not Supported");
                    }
                    Output.WriteUIn32(NPKMinorVersion);
                    Output.WriteBytes(CurrentIV);
                    Output.WriteUIn32((uint)FilesPath.Length);

                    uint TableSize;
                    using (Stream TBuilder = BuildEntries(Entries))
                    using (Stream TEncryptor = TBuilder.CreateEncryptor(CurrentKey, CurrentIV))
                    using (Stream TBuffer = TEncryptor.ToMemory())
                        TableSize = checked((uint)TBuffer.Length);

                    Output.WriteUIn32(TableSize);
                    long TablePos = Output.Position;
                    Output.WriteBytes(new byte[TableSize]);

                    // The upstream compression/encryption/writer is shared by BOTH modes.
                    for (int i = 0; i < FilesData.Length; i++) {
                        Console.WriteLine($"Writing File: {RelativeFiles[i]}");
                        string Ext = Path.GetExtension(FilesPath[i]).ToLower().TrimStart('.');
                        bool Compress = EnableCompression && !DontCompress.Contains(Ext);
                        NPK3Entry OldEntry = default;
                        bool HasOriginal = OriginalByPath != null && OriginalByPath.TryGetValue(Entries[i].FilePath.Replace('\\', '/'), out OldEntry);
                        long ReadPos = 0;
                        for (int x = 0; x < Entries[i].SegmentsInfo.Length; x++) {
                            var SegmentData = FilesData[i].CreateStream(ReadPos, Entries[i].SegmentsInfo[x].DecompressedSize);
                            ReadPos += SegmentData.Length;

                            // In template mode, use the original policy for existing segments.
                            // New files retain the normal -CP / DontCompress behavior.
                            bool SegmentCompress = HasOriginal
                                ? OldEntry.SegmentsInfo[Math.Min(x, OldEntry.SegmentsInfo.Length - 1)].IsCompressed
                                : Compress;
                            var Stream = SegmentCompress ? SegmentData.Compress(NPKVersion) : SegmentData;
                            if (Stream.Length >= Entries[i].SegmentsInfo[x].DecompressedSize)
                                Stream = SegmentData;

                            using var Crypted = Stream.CreateEncryptor(CurrentKey, CurrentIV).ToMemory();
                            Entries[i].SegmentsInfo[x].RealSize = checked((uint)Stream.Length);
                            Entries[i].SegmentsInfo[x].AlignedSize = checked((uint)Crypted.Length);
                            Entries[i].SegmentsInfo[x].Offset = Output.Position; // Fix: do NOT truncate at 4 GiB.
                            Crypted.CopyTo(Output);
                        }
                        if (ReadPos != FilesData[i].Length)
                            throw new InvalidDataException("Not all input bytes were packed: " + RelativeFiles[i]);
                    }

                    Output.Position = TablePos;
                    Console.WriteLine("Writing File Index...");
                    using var TableData = BuildEntries(Entries);
                    using var TableEncryptor = TableData.CreateEncryptor(CurrentKey, CurrentIV);
                    using var OutTableData = TableEncryptor.ToMemory();
                    if (OutTableData.Length != TableSize)
                        throw new InvalidDataException("Encrypted table size changed during repacking.");
                    OutTableData.CopyTo(Output);
                }
            }
            finally {
                foreach (Stream FileData in OpenedFiles)
                    FileData.Dispose();
                // The original tool stores settings in statics. Avoid contaminating the
                // next archive when several repacks run in the same process.
                if (TemplateMode) {
                    NPKVersion = SavedVersion;
                    NPKMinorVersion = SavedMinorVersion;
                    CurrentIV = SavedIV;
                    MaxSectionSize = SavedMaxSectionSize;
                    EnableSegmentation = SavedEnableSegmentation;
                    ForceSegmentation = SavedForceSegmentation;
                }
            }
        }

        private static uint GetUniformOriginalSectionSize(NPK3Entry[] OriginalEntries) {
            uint Size = 0;
            foreach (var Entry in OriginalEntries) {
                if (Entry.SegmentsInfo == null) continue;
                for (int i = 0; i + 1 < Entry.SegmentsInfo.Length; i++) {
                    uint ThisSize = Entry.SegmentsInfo[i].DecompressedSize;
                    if (ThisSize == 0) continue;
                    if (Size == 0) Size = ThisSize;
                    else if (Size != ThisSize) return 0;
                }
            }
            return Size;
        }

        // Existing files: preserve original segmentation mode, and, when possible,
        // exact boundaries. Modified sizes are repartitioned without truncating data.
        // Added files: keep entries generated by upstream CreateInitialEntries().
        private static void ApplyOriginalSegmentLayouts(NPK3Entry[] Entries, Dictionary<string, NPK3Entry> Originals) {
            for (int i = 0; i < Entries.Length; i++) {
                NPK3Entry Current = Entries[i];
                if (!Originals.TryGetValue(Current.FilePath.Replace('\\', '/'), out NPK3Entry Original))
                    continue;

                long Remaining = Current.FileSize;
                var Sizes = new List<uint>();
                if (Current.FileSize == Original.FileSize) {
                    foreach (var Segment in Original.SegmentsInfo)
                        Sizes.Add(Segment.DecompressedSize);
                }
                else if (Original.SegmentationMode == 1 && Original.SegmentsInfo.Length == 1) {
                    Sizes.Add(Current.FileSize);
                }
                else {
                    // Reuse original early boundaries, then extend/shrink the last chunks.
                    uint ChunkSize = 0;
                    foreach (var Segment in Original.SegmentsInfo)
                        if (Segment.DecompressedSize > ChunkSize) ChunkSize = Segment.DecompressedSize;
                    if (ChunkSize == 0) ChunkSize = MaxSectionSize;
                    if (ChunkSize == 0) throw new InvalidDataException("Zero chunk size: " + Current.FilePath);

                    for (int x = 0; x + 1 < Original.SegmentsInfo.Length && Remaining > 0; x++) {
                        uint Take = (uint)Math.Min(Remaining, Original.SegmentsInfo[x].DecompressedSize);
                        if (Take == 0) continue;
                        Sizes.Add(Take);
                        Remaining -= Take;
                    }
                    while (Remaining > 0) {
                        uint Take = (uint)Math.Min(Remaining, ChunkSize);
                        Sizes.Add(Take);
                        Remaining -= Take;
                    }
                    if (Sizes.Count == 0) Sizes.Add(0);
                }

                long Sum = 0;
                var Segments = new NPKSegmentInfo[Sizes.Count];
                for (int x = 0; x < Sizes.Count; x++) {
                    Sum += Sizes[x];
                    Segments[x] = new NPKSegmentInfo {
                        Offset = 0, RealSize = Sizes[x],
                        DecompressedSize = Sizes[x], AlignedSize = 0
                    };
                }
                if (Sum != Current.FileSize)
                    throw new InvalidDataException("Template segments do not cover the whole file: " + Current.FilePath);

                Current.SegmentationMode = Original.SegmentationMode;
                Current.SegmentsInfo = Segments;
                Entries[i] = Current;
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
            if (MaxSize.ToLower().StartsWith("0x")) {
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
                if (Streams[i].Length > uint.MaxValue)
                    throw new InvalidDataException("A single NPK entry cannot exceed 4 GiB: " + Entry.FilePath);
                Entry.FileSize = (uint)Streams[i].Length;
                Entry.SHA256 = Streams[i].SHA256Checksum();
                long Reaming = Entry.FileSize;
                if (EnableSegmentation || Reaming > uint.MaxValue || ForceSegmentation) {
                    if (!EnableSegmentation) {
                        var OriColor = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Big File Detected: '{Entry.FilePath}', Enforcing Segmentation");
                        Console.ForegroundColor = OriColor;
                    }
                    Entry.SegmentsInfo = new NPKSegmentInfo[1 + (Entry.FileSize / MaxSectionSize)];
                    Entry.SegmentationMode = (byte)(Entry.SegmentsInfo.Length > 1 ? 0 : 1);
                    if (ForceSegmentation)
                        Entry.SegmentationMode = 0;
                    for (int x = 0; x < Entry.SegmentsInfo.Length; x++) {
                        uint MaxBytes = Reaming < MaxSectionSize ? (uint)Reaming : MaxSectionSize;
                        Entry.SegmentsInfo[x] = new NPKSegmentInfo() {
                            Offset = 0,
                            DecompressedSize = MaxBytes,
                            RealSize = MaxBytes,
                            AlignedSize = MaxBytes + (0x10 - (MaxBytes % 0x10))
                        };
                        Reaming -= MaxBytes;
                    }
                }
                else {
                    Entry.SegmentationMode = 1;
                    Entry.SegmentsInfo = new NPKSegmentInfo[] {
                        new NPKSegmentInfo() {
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

        public static void Unpack(string Package, string OutDir = null) {
            if (OutDir == null)
                OutDir = Path.Combine(Path.GetDirectoryName(Package), Path.GetFileName(Package) + "~");
            if (new FileInfo(Package).IsReadOnly)
                throw new Exception("Can't Unpack Read-Only Files");
            using (Stream NPK = File.Open(Package, FileMode.Open)) {
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
                foreach (var Entry in Entries) {
                    Console.WriteLine($"Extracting File: {Entry.FilePath}");
                    string OutPath = Path.Combine(OutDir, Entry.FilePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                    if (!Directory.Exists(Path.GetDirectoryName(OutPath)))
                        Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
                    using (Stream Output = File.Create(OutPath)) {
                        foreach (var Segment in Entry.SegmentsInfo) {
                            long Offset = Segment.Offset;
                            uint Size = Segment.AlignedSize;
                            using (Stream Buffer = new MemoryStream()) {
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
    struct NPK3Entry {
        public byte SegmentationMode;
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
