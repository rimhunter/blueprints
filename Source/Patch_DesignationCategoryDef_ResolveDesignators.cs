using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Blueprints {
    [HarmonyPatch]
    public static class Patch_DesignationCategoryDef_ResolveDesignators {
        public static MethodBase TargetMethod() {
            return AccessTools.Method(typeof(DesignationCategoryDef), "ResolveDesignators") ??
                   throw new InvalidOperationException("Blueprints: DesignationCategoryDef.ResolveDesignators not found");
        }

        public static void Postfix(DesignationCategoryDef __instance) {
            BlueprintController.Notify_CategoryResolved(__instance);
        }
    }
}
