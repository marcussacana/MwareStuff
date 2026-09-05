using Iced.Intel;
using StringReloads.Hook.Base;
using System;

namespace MwareHook
{
    unsafe class DiasmHelper
    {
        MemoryCodeReader Reader;
        Decoder Decoder;

        public DiasmHelper(void* Address) : this(Address, Environment.Is64BitProcess ? 64 : 32) { }

        public DiasmHelper(void* Address, int bitness)
        {
            if (bitness != 32 && bitness != 64)
                bitness = Environment.Is64BitProcess ? 64 : 32;

            Reader = new MemoryCodeReader(Address);
            Decoder = Decoder.Create(bitness, Reader);
            Decoder.IP = (ulong)Address;
        }

        public Instruction Diassembly()
        {
            return Decoder.Decode();
        }

        public void Reset()
        {
            Reader.Reset();
        }
    }
}

