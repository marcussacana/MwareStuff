using AdvancedBinary;
using static NPK3Tool.Program;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NPK3Tool
{
    static class NPK3
    {
		public static NPK3Entry[] GetEntries(Stream EntryTable) {
			List<NPK3Entry> Entries = new List<NPK3Entry>();
			StructReader Reader = new StructReader(EntryTable, Encoding: Encoding.UTF8);
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
		public byte Unk0;//Segmention ON/OFF?

		[PString(PrefixType = Const.UINT16)]
		public string FilePath;

		public uint FileSize;

		[FArray(Length = 0x20)]
		public byte[] Key;

		[PArray(PrefixType = Const.UINT32), StructField]
		public NPKSegmentInfo[] SegmentsInfo;
	}

	struct NPKSegmentInfo {
		public uint Offset;//Offset?
		public uint UnkValue;
		public uint AlignedSize;//Align?
		public uint RealSize;//Offset?
		public uint DecompressedSize;

		[Ignore]
		public bool IsCompressed => RealSize < DecompressedSize;
	}
#pragma warning restore 0219, 0649
}
