using Iced.Intel;
using StringReloads.Hook.Base;
using System.Linq;

namespace MwareHook
{
    unsafe class DiasmHelper
    {
        MemoryCodeReader Reader;
        Decoder Decoder;
        public DiasmHelper(void* Address) {
            Reader = new MemoryCodeReader(Address);
            Decoder = Decoder.Create(32, Reader);
        }

        public Instruction Diassembly() {
            return Decoder.DecodeAmount(1).Single();
        }

        public void Reset() {
            Reader.Reset();
        }
    }
}
