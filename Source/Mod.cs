// Copyright Karel Kroeze, 2020-2020
using HarmonyLib;
using System.Reflection;
using Verse;

namespace Blueprints {
    public class Mod: Verse.Mod {
        public Mod(ModContentPack content) : base(content) {
            new Harmony("rimhunter.blueprints").PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
