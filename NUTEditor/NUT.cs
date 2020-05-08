using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NUTEditor
{
    public class NUT
    {
        Encoding Encoding = Encoding.UTF8;

        readonly byte[] StringPrefix = new byte[] { 0x10, 0x00, 0x00, 0x08 };
        List<int> StringOffsets = new List<int>();
        byte[] Script;
        public NUT(byte[] Script) {
            this.Script = Script;
        }

        public string[] Import() {
            List<string> Strings = new List<string>();
            StringOffsets = new List<int>();
            for (int i = 0; i < Script.Length; i++) {
                if (!EqualsAt(Script, StringPrefix, i))
                    continue;
                i += 4;
                if (!IsValidString(Script, i)) {
                    i -= 4;
                    continue;
                }
                Strings.Add(ReadStringAt(Script, i));
                StringOffsets.Add(i);
            }

            return Strings.ToArray();
        }

        public byte[] Export(string[] Lines) {
            byte[] Output = Script.Take(Script.Length).ToArray();
            for (int i = Lines.Length - 1; i >= 0; i--) {
                Output = ReplaceStringAt(Output, StringOffsets[i], Lines[i]);
            }

            int Diff = Output.Length - Script.Length;
            Output = UpdateOffset(Output, 0x8, Diff);
            Output = UpdateOffset(Output, 0xC, Diff);

            return Output;
        }

        bool EqualsAt(byte[] Buffer, byte[] Pattern, int Index) {
            if (Buffer.Length <= Pattern.Length + Index)
                return false;

            for (int i = 0; i < Pattern.Length; i++)
                if (Buffer[Index + i] != Pattern[i])
                    return false;
            return true;
        }

        private bool IsValidString(byte[] Buffer, int Index) {
            var Size = ReadU32At(Buffer, Index);
            if (Size + Index >= Buffer.Length)
                return false;

            var Data = GetRange(Buffer, Index + sizeof(uint), (int)Size);
            foreach (var Byte in Data)
                if (Byte < 0x0A)
                    return false;
            return true;
        }

        private byte[] GetRange(byte[] Buffer, int Index, int Length) {
            byte[] Output = new byte[Length];
            for (int i = 0; i < Length; i++)
                Output[i] = Buffer[i + Index];
            return Output;
        }

        private string ReadStringAt(byte[] Buffer, int Index) {
            uint StrSize = ReadU32At(Buffer, Index);
            byte[] String = GetRange(Buffer, Index + sizeof(uint), (int)StrSize);

            return Encoding.GetString(String);
        }

        private byte[] ReplaceStringAt(byte[] Buffer, int Index, string Data) {
            var PartA = Buffer.Take(Index);
            var OriLen = ReadU32At(Buffer, Index);
            var PartB = Buffer.Skip(Index + sizeof(uint) + (int)OriLen);
            var NewData = Encoding.GetBytes(Data);

            return PartA
                .Concat(BitConverter.GetBytes(NewData.Length))
                .Concat(NewData)
                .Concat(PartB).ToArray();
        }

        private uint ReadU32At(byte[] Buffer, int Index) {
            byte[] tmp = GetRange(Buffer, Index, 4);
            return BitConverter.ToUInt32(tmp, 0);
        }

        byte[] UpdateOffset(byte[] Buffer, int Index, int Diff) {
            var OriVal = ReadU32At(Buffer, Index);
            var NewVal = BitConverter.GetBytes((int)(OriVal + Diff));

            var Output = Buffer.Take(Buffer.Length).ToArray();
            NewVal.CopyTo(Output, Index);
            return Output;
        }
    }
}
