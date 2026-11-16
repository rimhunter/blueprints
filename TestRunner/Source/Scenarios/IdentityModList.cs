using System.Collections;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class IdentityModList
    {
        const string OurPackageId = "rimhunter.blueprints";
        static readonly string[] Others = { "fluffy.blueprints", "fluffy.blueprints.fork", "defi.blueprints.fork", "dw.defi.fluffy.blueprints.fork" };

        static ModMetaData Mod(string packageId) {
            var mod = ModLister.GetModWithIdentifier(packageId, ignorePostfix: true);
            if (mod == null) throw new ScenarioFailure("the stand-in mod " + packageId + " is not in the Mods folder");
            return mod;
        }

        [Scenario("screenshot-mod-list-warns-when-fluffys-blueprints-is-also-enabled", MapSize = 40, Windowed = true, MaxSeconds = 150,
            StageMods = "TestRunner/Fixtures/fluffy-blueprints")]
        static IEnumerator ModListWarningFluffy(ScenarioContext s) {
            var page = new Page_ModsConfig();
            Find.WindowStack.Add(page);
            yield return null;
            var fluffy = Mod("fluffy.blueprints");
            var ours = Mod(OurPackageId);
            s.Check(!fluffy.Active, "Fluffy's Blueprints should start inactive");
            try {
                ModsConfig.SetActive(fluffy, true);
                page.SelectMod(ours);
                var shot = s.Screenshot("01-mod-list-incompatibility-warning");
                yield return shot;
                ModsConfig.GetModWarnings().TryGetValue(ours.PackageId, out var warning);
                s.Check((warning ?? "").IndexOf(fluffy.Name, System.StringComparison.OrdinalIgnoreCase) >= 0, "the game's warning for this mod should name Fluffy's Blueprints, was: " + warning);
                var incompatible = "ModIncompatibleWith".Translate("").Resolve().Trim();
                s.Check(shot.ShowsText(incompatible), "the mod list should show \"" + incompatible + "\" for this mod; it showed: " + shot.TextsMatching("incompatible"));
                s.Check(shot.Texts.Count(t => t.Text == fluffy.Name) >= 2, "the mod list should show Fluffy's Blueprints among the enabled mods and in the incompatibility row; it showed: " + shot.TextsMatching("Blueprints"));
            }
            finally {
                ModsConfig.SetActive(fluffy, false);
                page.Close(doCloseSound: false);
            }
            yield return null;
            s.Check(!page.IsOpen, "the mod list should close without saving once Fluffy's Blueprints is turned off again");
        }

        [Scenario("mod-list-warns-for-each-other-blueprints-build", MapSize = 30, MaxSeconds = 90,
            StageMods = "TestRunner/Fixtures/fluffy-blueprints TestRunner/Fixtures/fluffy-blueprints-fork TestRunner/Fixtures/defi-blueprints-fork TestRunner/Fixtures/temp-blueprints-fork")]
        static void ModListWarnsForEach(ScenarioContext s) {
            var ours = Mod(OurPackageId);
            foreach (var id in Others) {
                var other = Mod(id);
                try {
                    s.Check(!other.Active, id + " should start inactive");
                    ModsConfig.SetActive(other, true);
                    ModsConfig.GetModWarnings().TryGetValue(ours.PackageId, out var warning);
                    s.Check((warning ?? "").IndexOf(other.Name, System.StringComparison.OrdinalIgnoreCase) >= 0, "the game's warning for this mod should name " + id + " (" + other.Name + "), was: " + warning);
                }
                finally {
                    ModsConfig.SetActive(other, false);
                }
            }
        }
    }
}
