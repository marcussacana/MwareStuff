using StringReloads.Engine;
using StringReloads.Engine.Interface;
using StringReloads.Hook.Base;
using System;

namespace MwareHook
{
    public class EntryPoint : IPlugin
    {
        public string Name => throw new NotImplementedException();

        public IAutoInstall[] GetAutoInstallers() => null;

        public Hook[] GetHooks() => null;

        public IMatch[] GetMatchs() => null;

        public IStringModifier[] GetModifiers() => null;

        public IMod[] GetMods() => null;

        public IReloader[] GetReloaders() => null;

        public void Initialize(Main Engine) { }
    }
}
