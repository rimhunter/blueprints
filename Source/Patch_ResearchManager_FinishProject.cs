using HarmonyLib;
using RimWorld;

namespace Blueprints {
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
    public static class Patch_ResearchManager_FinishProject {
        public static void Postfix() {
            BlueprintController.Notify_ResearchFinished();
        }
    }
}
