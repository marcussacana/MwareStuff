using StringReloads.Hook.Base;
using System;

namespace MwareHook
{
    unsafe class CreateMutexInterceptor : Intercept
    {
        Action<uint> OnCalled;
        public CreateMutexInterceptor(Action<uint> OnCalled) : base("kernel32.dll", "CreateMutexA")
        {
            this.OnCalled = OnCalled;
        }

        public override InterceptDelegate HookFunction => new InterceptDelegate(OnCreateMutexIntercepted);

        void OnCreateMutexIntercepted(void* ESP)
        {
            OnCalled(*(((uint*)ESP)+8));
        }
    }
}
