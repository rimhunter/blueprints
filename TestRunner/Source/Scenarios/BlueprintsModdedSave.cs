using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class BlueprintsModdedSave
    {
        static readonly IntVec3 SecondDest = new IntVec3(18, 0, 14);

        static void Start(ScenarioContext s) {
            Bp.RequireBadHygiene();
            BlueprintsExport.Start(s, s.Stage == SaveStage.Setup);
        }

        static List<Part> StandingRoom(ScenarioContext s, string when) {
            var parts = Bp.Harvest(s.Map, s.Center + Bp.Source, 3, 2);
            BlueprintsModded.CheckNineBuildingsStanding(s, parts, when);
            return parts;
        }

        static void CheckPlacedBothWays(ScenarioContext s, Designator blueprint, List<Part> parts, string when) {
            Bp.DrawArchitectTab();
            var dest = s.Center + Bp.Dest;
            Bp.Place(blueprint, dest);
            Bp.CheckLayout(s, Bp.ExpectedAfter(parts, dest, 0), "placed unrotated " + when);
            Bp.ClearConstruction(s.Map);
            Bp.RotateRight(blueprint);
            var turned = s.Center + SecondDest;
            Bp.Place(blueprint, turned);
            Bp.CheckLayout(s, Bp.ExpectedAfter(parts, turned, 1), "placed one step clockwise " + when);
            Bp.ClearConstruction(s.Map);
            Bp.RotateLeft(blueprint);
        }

        [Scenario("blueprints-with-dubs-bad-hygiene-buildings-survive-save-and-reload", MapSize = 80, MaxSeconds = 150, SaveWithMods = "{mod} 836308268", Mods = "836308268", SetupInSaveLaunch = true, ReloadInSaveLaunch = true)]
        static void ModdedBlueprintSurvivesReload(ScenarioContext s) {
            Start(s);
            try {
                var source = s.Center + Bp.Source;
                if (s.Stage == SaveStage.Setup) {
                    BlueprintsModded.FinishResearch(BlueprintsModded.BadHygieneResearch);
                    Bp.BuildRoom(s, BlueprintsModded.Bathroom(), source, 2);
                    StandingRoom(s, "before the save");
                    Bp.Create(s, Bp.DragRect(source, 3, 2), "Bathroom");
                    s.Check(Bp.SavedNames.SequenceEqual(new[] { "Bathroom" }), "before the save the menu should list Bathroom; it lists: " + string.Join(", ", Bp.SavedNames));
                    return;
                }
                var when = "after the reload (" + s.Stage + ")";
                s.Check(Bp.SavedNames.SequenceEqual(new[] { "Bathroom" }), when + " the menu should list Bathroom; it lists: " + string.Join(", ", Bp.SavedNames));
                var blueprint = Bp.SavedNamed("Bathroom");
                if (blueprint == null) return;
                var parts = StandingRoom(s, when);
                CheckPlacedBothWays(s, blueprint, parts, when);
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        [Scenario("blueprints-with-dubs-bad-hygiene-buildings-export-and-import-after-a-restart", MapSize = 80, MaxSeconds = 150, SaveWithMods = "{mod} 836308268", Mods = "836308268", SetupInSaveLaunch = true, ReloadInSaveLaunch = true)]
        static void ModdedBlueprintExportsAndImports(ScenarioContext s) {
            Start(s);
            try {
                var source = s.Center + Bp.Source;
                var export = "Fluffy.Blueprints.SaveToXML".Translate().Resolve();
                var remove = "Fluffy.Blueprints.Remove".Translate().Resolve();
                var import = "Fluffy.Blueprints.LoadFromXML".Translate("Bathroom").Resolve();
                if (s.Stage == SaveStage.Setup) {
                    BlueprintsModded.FinishResearch(BlueprintsModded.BadHygieneResearch);
                    Bp.BuildRoom(s, BlueprintsModded.Bathroom(), source, 2);
                    StandingRoom(s, "before the save");
                    var created = Bp.Create(s, Bp.DragRect(source, 3, 2), "Bathroom");
                    Bp.Choose(Bp.Option(Bp.OpenRightClickMenu(created), export));
                    s.Check(File.Exists(BlueprintsExport.FileOf("Bathroom")), "exporting Bathroom should write " + BlueprintsExport.FileOf("Bathroom"));
                    Bp.Choose(Bp.Option(Bp.OpenRightClickMenu(Bp.SavedNamed("Bathroom")), remove));
                    s.AreEqual(0, Bp.Saved.Count, "blueprints left in the menu after removing the exported one");
                    return;
                }
                var when = " (" + s.Stage + ")";
                s.AreEqual(0, Bp.Saved.Count, "blueprints in the menu before importing" + when);
                var options = Bp.CreateTool.RightClickFloatMenuOptions.ToList();
                Bp.ClearWindows();
                s.Check(options.Any(o => o.Label == import), "the import menu should offer '" + import + "'" + when + "; it offers: " + string.Join(" | ", options.Select(o => o.Label)));
                if (options.All(o => o.Label != import)) return;
                Bp.Choose(Bp.Option(options, import));
                s.Check(Bp.SavedNames.SequenceEqual(new[] { "Bathroom" }), "after importing the menu should list Bathroom" + when + "; it lists: " + string.Join(", ", Bp.SavedNames));
                var blueprint = Bp.SavedNamed("Bathroom");
                if (blueprint == null) return;
                var parts = StandingRoom(s, "in the save that exported it" + when);
                CheckPlacedBothWays(s, blueprint, parts, "after importing" + when);
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }
    }
}
