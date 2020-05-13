using StringReloads.Engine;
using StringReloads.Engine.Interface;
using StringReloads.Hook.Base;
using System;

namespace MwareHook
{
    public class EntryPoint : IPlugin
    {
        public string Name => "MwareHook";

        public IAutoInstall[] GetAutoInstallers() => null;

        public Hook[] GetHooks() => null;

        public IMatch[] GetMatchs() => null;

        public IStringModifier[] GetModifiers() => null;

        public IMod[] GetMods() => new IMod[] { new MwareKeyFinder() };

        public IReloader[] GetReloaders() => null;

        public void Initialize(Main Engine) { }
    }
}
