using System;
using System.Collections.Generic;
using System.Text;

namespace MwareHook
{
    unsafe static class ModuleInfo
    {
        public static CodeInfo GetCodeInfo(void* Address) {
            ulong PEStart = *(uint*)((byte*)Address + 0x3C) + (ulong)Address;
            ulong OptionalHeader = PEStart + 0x18;

            uint SizeOfCode = *(uint*)(OptionalHeader + 0x04);
            uint EntryPoint = *(uint*)(OptionalHeader + 0x10);
            uint BaseOfCode = *(uint*)(OptionalHeader + 0x14);

            return new CodeInfo() {
                CodeAddress = ((byte*)Address) + BaseOfCode,
                EntryPoint  = ((byte*)Address) + EntryPoint,
                CodeSize    = SizeOfCode
            };
        }
    }
    public unsafe struct CodeInfo
    {
        public void* CodeAddress;
        public uint CodeSize;
        public void* EntryPoint;
    }
}
