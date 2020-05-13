using Iced.Intel;
using StringReloads.Hook.Base;
using System;

namespace MwareHook
{
    unsafe class KeyInterceptor : Intercept
    {
        int Register;
        public KeyInterceptor(void* Address, Register Register) : base(Address) {
            this.Register = Register switch {
                Register.EDI => 0,
                Register.ESI => 1,
                Register.EBP => 2,
                Register.ESP => 3,
                Register.EBX => 4,
                Register.EDX => 5,
                Register.ECX => 6,
                Register.EAX => 7,
                _ => throw new NotSupportedException("Invalid Key Handler Register")
            };
        }

        public override InterceptDelegate HookFunction => new InterceptDelegate(OnKeyExpanderBegin);

        public Action<byte[]> OnKeyIntercepted;

        void OnKeyExpanderBegin(void* ESP) {
            uint* Stack = (uint*)ESP;
            byte* KeyBuffer = (byte*)*(Stack + Register);
            byte[] Key = new byte[0x20];
            for (int i = 0; i < Key.Length; i++)
                Key[i] = KeyBuffer[i];
            OnKeyIntercepted?.Invoke(Key);
        }
    }
}
