using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class BlueprintsPersist
    {
        static readonly IntVec3 SecondSource = new IntVec3(-18, 0, 14);
        static readonly IntVec3 SecondDest = new IntVec3(18, 0, 14);

        static void Start(ScenarioContext s) {
            if (s.Stage == SaveStage.Setup) { Bp.Begin(s); return; }
            Current.Game.CurrentMap = s.Map;
            Event.current = new Event { type = EventType.Repaint };
            Bp.ClearWindows();
            Find.DesignatorManager.Deselect();
            Bp.ClearMessages();
        }

        static List<Part> RoomWithTable() {
            var parts = Bp.Room();
            parts.Add(Bp.Table());
            return parts;
        }

        static List<Part> SmallLayout() => new List<Part> {
            Part.Thing("Table2x2c", 0, 0, "WoodLog"),
            Part.Thing("TorchLamp", -2, 0),
            Part.Thing("PlantPot", 2, 1, "WoodLog"),
            Part.Floor("WoodPlankFloor", -2, 2),
        };

        static List<Part> SavedRoom(ScenarioContext s, int halfZ) {
            var parts = Bp.Harvest(s.Map, s.Center + Bp.Source, 3, halfZ);
            s.Check(parts.Count > 20, "the saved game should still hold the room the blueprint was made from; it holds " + parts.Count + " parts");
            return parts;
        }

        static List<string> Merge(params List<string>[] lists) => lists.SelectMany(l => l).OrderBy(x => x, System.StringComparer.Ordinal).ToList();

        static List<IntVec3> PlannedAround(ScenarioContext s, IntVec3 center) =>
            Bp.DragRect(center, 8, 8).Where(c => c.InBounds(s.Map) && s.Map.planManager.PlanAt(c) != null).OrderBy(c => c.x).ThenBy(c => c.z).ToList();

        [Scenario("blueprints-keep-their-names-and-contents-through-save-and-reload", MapSize = 80, MaxSeconds = 150, SaveWithMods = "{mod}", SetupInSaveLaunch = true, ReloadInSaveLaunch = true)]
        static void KeepNamesAndContents(ScenarioContext s) {
            Start(s);
            try {
                var source = s.Center + Bp.Source;
                var source2 = s.Center + SecondSource;
                if (s.Stage == SaveStage.Setup) {
                    Bp.BuildRoom(s, RoomWithTable(), source, 4);
                    Bp.BuildRoom(s, SmallLayout(), source2, 2);
                    Bp.Create(s, Bp.DragRect(source, 3, 4), "Alpha");
                    Bp.Create(s, Bp.DragRect(source2, 3, 3), "Beta");
                    s.Check(Bp.SavedNames.OrderBy(n => n).SequenceEqual(new[] { "Alpha", "Beta" }), "before the save the menu should list Alpha and Beta; it lists: " + string.Join(", ", Bp.SavedNames));
                    return;
                }
                var names = Bp.SavedNames.OrderBy(n => n).ToList();
                s.Check(names.SequenceEqual(new[] { "Alpha", "Beta" }), "after the reload (" + s.Stage + ") the menu should list Alpha and Beta; it lists: " + (names.Count == 0 ? "no blueprints" : string.Join(", ", names)));
                Bp.DrawArchitectTab();
                var alpha = Bp.SavedNamed("Alpha");
                var beta = Bp.SavedNamed("Beta");
                if (alpha == null || beta == null) return;
                Find.DesignatorManager.Select(alpha);
                alpha.SelectedUpdate();
                var partsA = SavedRoom(s, 4);
                var partsB = Bp.Harvest(s.Map, source2, 3, 3);
                var dest = s.Center + Bp.Dest;
                var dest2 = s.Center + SecondDest;
                Bp.Place(alpha, dest);
                Bp.Place(beta, dest2);
                Bp.CheckLayout(s, Merge(Bp.ExpectedAfter(partsA, dest, 0), Bp.ExpectedAfter(partsB, dest2, 0)), "both blueprints placed after the reload (" + s.Stage + ")");
                Bp.DrawArchitectTab();
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        [Scenario("blueprints-rotated-placement-keeps-cells-rotation-and-stuff-through-save", MapSize = 80, MaxSeconds = 150, SaveWithMods = "{mod}", SetupInSaveLaunch = true, ReloadInSaveLaunch = true)]
        static void RotatedPlacementSurvives(ScenarioContext s) {
            Start(s);
            try {
                var source = s.Center + Bp.Source;
                var orders = s.Center + new IntVec3(18, 0, -14);
                var plans = s.Center + new IntVec3(18, 0, 14);
                Func<Part, string> steel = p => !p.IsTerrain && ScenarioContext.Def(p.Def).MadeFromStuff ? "Steel" : null;
                if (s.Stage == SaveStage.Setup) {
                    var parts = Bp.BuildRoom(s, RoomWithTable(), source, 4);
                    var created = Bp.Create(s, Bp.DragRect(source, 3, 4), "Turned");
                    Bp.ChooseStuffForEveryBuildable(created, parts, "Steel");
                    Bp.RotateRight(created);
                    Bp.Place(created, orders);
                    Bp.CheckLayout(s, Bp.ExpectedAfter(parts, orders, 1, steel), "construction orders placed rotated, before the save");
                    Bp.Place(created, plans, shift: true);
                    var planned = PlannedAround(s, plans);
                    var expectedPlans = parts.Where(p => p.Def == "Wall").Select(p => plans + Bp.RotateOffset(p.At, 1)).OrderBy(c => c.x).ThenBy(c => c.z).ToList();
                    s.Check(planned.SequenceEqual(expectedPlans), "plans placed rotated, before the save, should cover the rotated walls: " + string.Join(" ", planned) + " vs " + string.Join(" ", expectedPlans));
                    return;
                }
                var saved = SavedRoom(s, 4);
                Bp.CheckLayout(s, Bp.ExpectedAfter(saved, orders, 1, steel), "construction orders after the reload (" + s.Stage + ")");
                var plannedAfter = PlannedAround(s, plans);
                var expectedAfter = saved.Where(p => p.Def == "Wall").Select(p => plans + Bp.RotateOffset(p.At, 1)).OrderBy(c => c.x).ThenBy(c => c.z).ToList();
                s.Check(plannedAfter.SequenceEqual(expectedAfter), "plans after the reload (" + s.Stage + ") should still cover the rotated walls: " + string.Join(" ", plannedAfter) + " vs " + string.Join(" ", expectedAfter));
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        [Scenario("blueprints-placed-again-after-reload-keeps-the-rotation", MapSize = 80, MaxSeconds = 150, SaveWithMods = "{mod}", SetupInSaveLaunch = true, ReloadInSaveLaunch = true)]
        static void PlacedAgainAfterReload(ScenarioContext s) {
            Start(s);
            try {
                var source = s.Center + Bp.Source;
                var first = s.Center + new IntVec3(18, 0, -14);
                if (s.Stage == SaveStage.Setup) {
                    var parts = Bp.BuildRoom(s, RoomWithTable(), source, 4);
                    var created = Bp.Create(s, Bp.DragRect(source, 3, 4), "Again");
                    Bp.RotateRight(created);
                    Bp.Place(created, first);
                    Bp.CheckLayout(s, Bp.ExpectedAfter(parts, first, 1), "placed rotated once, before the save");
                    return;
                }
                var saved = SavedRoom(s, 4);
                Bp.CheckLayout(s, Bp.ExpectedAfter(saved, first, 1), "orders placed before the save, after the reload (" + s.Stage + ")");
                Bp.ClearConstruction(s.Map);
                var blueprint = Bp.SavedNamed("Again");
                s.Check(blueprint != null, "the menu should list Again after the reload (" + s.Stage + "); it lists: " + string.Join(", ", Bp.SavedNames));
                if (blueprint == null) return;
                var again = s.Center + new IntVec3(18, 0, 0);
                Bp.Place(blueprint, again);
                Bp.CheckLayout(s, Bp.ExpectedAfter(saved, again, 1), "placed again after the reload (" + s.Stage + "), with no rotation pressed");
                Bp.ClearConstruction(s.Map);
                Bp.RotateRight(blueprint);
                var turned = s.Center + new IntVec3(18, 0, 14);
                Bp.Place(blueprint, turned);
                Bp.CheckLayout(s, Bp.ExpectedAfter(saved, turned, 2), "rotated once more after the reload (" + s.Stage + ")");
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        static List<string> OrdersMovedBy(Map map, IntVec3 delta) {
            var result = new List<string>();
            foreach (var thing in map.listerThings.AllThings.ToList()) {
                if (thing is not Blueprint_Build b) continue;
                var entity = b.def.entityDefToBuild;
                var isTerrain = entity is TerrainDef;
                result.Add(Bp.Describe(entity.defName, isTerrain ? null : b.stuffToUse?.defName, isTerrain ? Rot4.North : b.Rotation, b.OccupiedRect().Cells.Select(c => c + delta)));
            }
            return result.OrderBy(x => x, System.StringComparer.Ordinal).ToList();
        }

        [Scenario("blueprints-turned-and-flipped-blueprint-places-the-same-after-reload", MapSize = 80, MaxSeconds = 150, SaveWithMods = "{mod}", SetupInSaveLaunch = true, ReloadInSaveLaunch = true)]
        static void TurnedAndFlippedPlacesTheSame(ScenarioContext s) {
            Start(s);
            try {
                var source = s.Center + Bp.Source;
                var first = s.Center + new IntVec3(18, 0, -14);
                var second = s.Center + new IntVec3(18, 0, 14);
                if (s.Stage == SaveStage.Setup) {
                    var parts = RoomWithTable();
                    parts.Add(Part.Thing("BlueprintsTest_EvenBench", -2, 1));
                    Bp.BuildRoom(s, parts, source, 4);
                    var created = Bp.Create(s, Bp.DragRect(source, 3, 4), "Twisted");
                    Bp.RotateRight(created);
                    Bp.RotateRight(created);
                    Bp.RotateRight(created);
                    Bp.Flip(created);
                    Bp.ClearMessages();
                    Bp.Place(created, first);
                    s.Check(Bp.PlacedConstruction(s.Map).Count > 30, "the turned and flipped blueprint should place its parts before the save");
                    return;
                }
                var expected = OrdersMovedBy(s.Map, second - first);
                s.Check(expected.Count > 30, "the construction orders placed before the save should be in the reloaded game (" + s.Stage + "); found " + expected.Count);
                Bp.ClearConstruction(s.Map);
                var blueprint = Bp.SavedNamed("Twisted");
                s.Check(blueprint != null, "the menu should list Twisted after the reload (" + s.Stage + "); it lists: " + string.Join(", ", Bp.SavedNames));
                if (blueprint == null) return;
                Bp.Place(blueprint, second);
                Bp.CheckLayout(s, expected, "turned three steps and flipped, placed again after the reload (" + s.Stage + ")");
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        static string ExportedFile(string name) =>
            System.IO.Path.Combine((string)HarmonyLib.AccessTools.Method(typeof(GenFilePaths), "FolderUnderSaveData").Invoke(null, new object[] { "Blueprints" }), name + ".xml");

        [Scenario("blueprints-remember-that-they-were-exported-after-reload", MapSize = 80, MaxSeconds = 150, SaveWithMods = "{mod}", SetupInSaveLaunch = true, ReloadInSaveLaunch = true)]
        static void RememberExported(ScenarioContext s) {
            Start(s);
            try {
                var source = s.Center + Bp.Source;
                var export = "Fluffy.Blueprints.SaveToXML".Translate().Resolve();
                var removeAndDelete = "Fluffy.Blueprints.RemoveAndDeleteXML".Translate().Resolve();
                if (s.Stage == SaveStage.Setup) {
                    Bp.BuildRoom(s, SmallLayout(), source, 3);
                    var created = Bp.Create(s, Bp.DragRect(source, 3, 3), "Shared");
                    var before = Bp.OpenRightClickMenu(created);
                    Bp.Choose(Bp.Option(before, export));
                    s.Check(System.IO.File.Exists(ExportedFile("Shared")), "exporting Shared should write " + ExportedFile("Shared"));
                    return;
                }
                var blueprint = Bp.SavedNamed("Shared");
                s.Check(blueprint != null, "the menu should list Shared after the reload (" + s.Stage + "); it lists: " + string.Join(", ", Bp.SavedNames));
                if (blueprint == null) return;
                var labels = Bp.OpenRightClickMenu(blueprint).Select(o => o.Label).ToList();
                Bp.ClearWindows();
                s.Check(labels.Contains(removeAndDelete) && !labels.Contains(export), "after the reload (" + s.Stage + ") an exported blueprint should offer '" + removeAndDelete + "' and not '" + export + "'; the menu offers: " + string.Join(" | ", labels));
                s.Check(System.IO.File.Exists(ExportedFile("Shared")), "the exported file should still be in the Blueprints folder after the reload (" + s.Stage + ")");
                if (s.Stage == SaveStage.ReloadedInSameLaunch) return;
                Bp.Choose(Bp.Option(Bp.OpenRightClickMenu(blueprint), removeAndDelete));
                s.Check(Bp.SavedNamed("Shared") == null, "removing and deleting the exported blueprint should take it out of the menu");
                s.Check(!System.IO.File.Exists(ExportedFile("Shared")), "removing and deleting the exported blueprint should delete its file");
                Bp.Create(s, Bp.DragRect(source, 3, 3), "Shared");
                s.Check(Bp.SavedNamed("Shared") != null, "a new blueprint should be able to take the name Shared again");
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        [Scenario("blueprints-deleted-before-a-save-stay-deleted-after-reload", MapSize = 80, MaxSeconds = 150, SaveWithMods = "{mod}", SetupInSaveLaunch = true, ReloadInSaveLaunch = true)]
        static void DeletedStayDeleted(ScenarioContext s) {
            Start(s);
            try {
                var source = s.Center + Bp.Source;
                if (s.Stage == SaveStage.Setup) {
                    Bp.BuildRoom(s, SmallLayout(), source, 3);
                    Bp.Create(s, Bp.DragRect(source, 3, 3), "Keep");
                    var drop = Bp.Create(s, Bp.DragRect(source, 3, 3), "Drop");
                    Bp.Choose(Bp.Option(Bp.OpenRightClickMenu(drop), "Fluffy.Blueprints.Remove".Translate().Resolve()));
                    s.Check(Bp.SavedNames.SequenceEqual(new[] { "Keep" }), "before the save only Keep should be listed; the menu lists: " + string.Join(", ", Bp.SavedNames));
                    return;
                }
                s.Check(Bp.SavedNames.SequenceEqual(new[] { "Keep" }), "after the reload (" + s.Stage + ") only Keep should be listed; the menu lists: " + string.Join(", ", Bp.SavedNames));
                Bp.Create(s, Bp.DragRect(source, 3, 3), "Drop");
                s.Check(Bp.SavedNames.OrderBy(n => n).SequenceEqual(new[] { "Drop", "Keep" }), "a new blueprint should be able to take the deleted name Drop (" + s.Stage + "); the menu lists: " + string.Join(", ", Bp.SavedNames));
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        [Scenario("blueprints-an-empty-library-reloads-clean", MapSize = 80, MaxSeconds = 150, SaveWithMods = "{mod}", SetupInSaveLaunch = true, ReloadInSaveLaunch = true)]
        static void EmptyLibraryReloads(ScenarioContext s) {
            Start(s);
            try {
                var source = s.Center + Bp.Source;
                if (s.Stage == SaveStage.Setup) {
                    Bp.BuildRoom(s, SmallLayout(), source, 3);
                    var gone = Bp.Create(s, Bp.DragRect(source, 3, 3), "Gone");
                    Bp.Choose(Bp.Option(Bp.OpenRightClickMenu(gone), "Fluffy.Blueprints.Remove".Translate().Resolve()));
                    s.AreEqual(0, Bp.Saved.Count, "blueprints listed after removing the only one");
                    return;
                }
                s.AreEqual(0, Bp.Saved.Count, "blueprints listed after reloading an empty library (" + s.Stage + ")");
                s.AreEqual(1, Bp.Tab.Count(d => d.GetType().Name == "Designator_CreateBlueprint"), "create tools in the Blueprints menu (" + s.Stage + ")");
                Bp.DrawArchitectTab();
                Bp.Create(s, Bp.DragRect(source, 3, 3), "Fresh");
                s.Check(Bp.SavedNames.SequenceEqual(new[] { "Fresh" }), "a blueprint made after the reload should be listed (" + s.Stage + "); the menu lists: " + string.Join(", ", Bp.SavedNames));
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }
    }
}
