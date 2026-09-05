using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NUTEditor
{
    public class NUT
    {
        public Encoding Encoding = Encoding.UTF8;//Totono (Steam)
       // public Encoding Encoding = Encoding.GetEncoding(932);//JP Games

        readonly byte[] StringPrefix = new byte[] { 0x10, 0x00, 0x00, 0x08 };
        List<int> StringOffsets = new List<int>();
        List<int> StringDataOffsets = new List<int>();
        byte[] Script;

        public NUT(byte[] Script)
        {
            this.Script = Script;
        }

        public string[] Import()
        {
            List<string> Strings = new List<string>();
            StringOffsets = new List<int>();
            StringDataOffsets = new List<int>();

            for (int i = 0; i < Script.Length; i++)
            {
                if (!EqualsAt(Script, StringPrefix, i))
                    continue;

                i += 4;

                int StringOffset = GetStringOffset(Script, i);
                if (StringOffset < 0)
                {
                    i -= 4;
                    continue;
                }

                Strings.Add(ReadStringAt(Script, i, StringOffset));
                StringOffsets.Add(i);
                StringDataOffsets.Add(StringOffset);
            }

            return Strings.ToArray();
        }

        public byte[] Export(string[] Lines)
        {
            if (Lines == null)
                throw new ArgumentNullException(nameof(Lines));

            if (Lines.Length != StringOffsets.Count)
                throw new ArgumentException("The number of lines must match the number of imported strings.");

            byte[] Output = Script.Take(Script.Length).ToArray();

            for (int i = Lines.Length - 1; i >= 0; i--)
                Output = ReplaceStringAt(Output, StringOffsets[i], StringDataOffsets[i] - StringOffsets[i], Lines[i]);

            int Diff = Output.Length - Script.Length;
            Output = UpdateOffset(Output, 0x8, Diff);
            Output = UpdateOffset(Output, 0xC, Diff);

            return Output;
        }

        bool EqualsAt(byte[] Buffer, byte[] Pattern, int Index)
        {
            if (Index < 0 || Buffer.Length < Pattern.Length + Index)
                return false;

            for (int i = 0; i < Pattern.Length; i++)
                if (Buffer[Index + i] != Pattern[i])
                    return false;

            return true;
        }

        private int GetStringOffset(byte[] Buffer, int Index)
        {
            uint Size = ReadU32At(Buffer, Index);

            int OldOffset = Index + 4;
            int NewOffset = Index + 8;

            bool OldValid = IsValidString(Buffer, OldOffset, Size);
            bool NewValid = IsValidString(Buffer, NewOffset, Size);

            if (NewValid)
                return NewOffset;

            if (OldValid)
                return OldOffset;

            return -1;
        }

        private bool IsValidString(byte[] Buffer, int Index, uint Size)
        {
            if (Size == 0 || Index < 0 || Size > int.MaxValue || Index + (long)Size > Buffer.Length)
                return false;

            byte[] Data = GetRange(Buffer, Index, (int)Size);

            foreach (byte Byte in Data)
                if (Byte < 0x0A)
                    return false;

            return true;
        }

        private byte[] GetRange(byte[] Buffer, int Index, int Length)
        {
            byte[] Output = new byte[Length];

            for (int i = 0; i < Length; i++)
                Output[i] = Buffer[Index + i];

            return Output;
        }

        private string ReadStringAt(byte[] Buffer, int Index, int StringOffset)
        {
            uint StrSize = ReadU32At(Buffer, Index);
            byte[] String = GetRange(Buffer, StringOffset, checked((int)StrSize));
            return Encoding.GetString(String);
        }

        private byte[] ReplaceStringAt(byte[] BufferArr, int Index, int HeaderSize, string Data)
        {
            var Buffer = new List<byte>(BufferArr);
            uint OriLen = ReadU32At(BufferArr, Index);
            int ExtraSize = HeaderSize - 4;

            Buffer.RemoveRange(Index, HeaderSize + checked((int)OriLen));

            byte[] NewContent = Encoding.GetBytes(Data);
            byte[] NewStrData = BitConverter.GetBytes(NewContent.Length)
                .Concat(ExtraSize == 4 ? new byte[4] : new byte[0])
                .Concat(NewContent)
                .ToArray();

            Buffer.InsertRange(Index, NewStrData);
            return Buffer.ToArray();
        }

        private uint ReadU32At(byte[] Buffer, int Index)
        {
            return BitConverter.ToUInt32(GetRange(Buffer, Index, 4), 0);
        }

        byte[] UpdateOffset(byte[] Buffer, int Index, int Diff)
        {
            uint OriVal = ReadU32At(Buffer, Index);
            byte[] NewVal = BitConverter.GetBytes((int)(OriVal + Diff));
            byte[] Output = Buffer.Take(Buffer.Length).ToArray();
            NewVal.CopyTo(Output, Index);
            return Output;
        }
    }
}