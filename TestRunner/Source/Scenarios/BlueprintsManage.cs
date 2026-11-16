using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class BlueprintsManage
    {
        static readonly string[] ManyDefs = {
            "Wall", "Door", "Bed", "TorchLamp", "PlantPot", "TorchWallLamp", "Stool", "DiningChair", "Armchair", "Couch",
            "Shelf", "Table2x2c", "Table1x2c", "Bookcase", "SleepingSpot", "Cooler", "Campfire", "CraftingSpot",
        };

        static Designator MakeBigBlueprint(ScenarioContext s, string name) {
            var source = s.Center + Bp.Source;
            var x = -8;
            foreach (var defName in ManyDefs) {
                var def = ScenarioContext.Def(defName);
                if (def.designationCategory == null) continue;
                s.SpawnBuilding(defName, source + new IntVec3(x, 0, 0), GenStuff.DefaultStuffFor(def)?.defName);
                x += 2;
                if (x > 12) x = -8;
            }
            for (var i = 0; i < 18; i++) {
                var def = ScenarioContext.Def(ManyDefs[i % ManyDefs.Length]);
                if (def.designationCategory == null || def.Size != IntVec2.One) continue;
                var cell = source + new IntVec3(-8 + (i % 12) * 2, 0, 3 + i / 12 * 2);
                if (s.Map.thingGrid.ThingsListAtFast(cell).Any(t => t is Building)) continue;
                s.SpawnBuilding(ManyDefs[i % ManyDefs.Length], cell, GenStuff.DefaultStuffFor(def)?.defName);
            }
            return Bp.Create(s, Bp.DragRect(source, 11, 5), name);
        }

        [Scenario("blueprints-rename-and-delete", MapSize = 80)]
        static void RenameAndDelete(ScenarioContext s) {
            Bp.Begin(s);
            try {
                var source = s.Center + Bp.Source;
                s.SpawnBuilding("Wall", source, "BlocksGranite");
                s.SpawnBuilding("Wall", source + new IntVec3(1, 0, 0), "BlocksGranite");
                var created = Bp.Create(s, Bp.DragRect(source, 1, 0), "Alpha");
                s.Check(Bp.SavedNames.SequenceEqual(new[] { "Alpha" }), "the Blueprints menu should list exactly Alpha; it lists: " + string.Join(", ", Bp.SavedNames));

                var options = Bp.OpenRightClickMenu(created);
                Bp.Choose(Bp.Option(options, "Fluffy.Blueprints.Rename".Translate().Resolve()));
                s.Check(Bp.TypeNameAndAccept("Beta"), "the rename dialog should accept Beta and close");
                s.Check(Bp.SavedNames.SequenceEqual(new[] { "Beta" }), "after renaming, the Blueprints menu should list exactly Beta; it lists: " + string.Join(", ", Bp.SavedNames));
                s.AreEqual("Beta", created.Label, "the renamed blueprint's label");

                options = Bp.OpenRightClickMenu(created);
                Bp.Choose(Bp.Option(options, "Fluffy.Blueprints.Remove".Translate().Resolve()));
                Bp.DrawArchitectTab();
                s.AreEqual(0, Bp.Saved.Count, "blueprints in the menu after deleting the only one");
                s.Check(Find.DesignatorManager.SelectedDesignator != created, "a deleted blueprint should not stay selected for placing");

                var again = Bp.Create(s, Bp.DragRect(source, 1, 0), "Beta");
                s.Check(Bp.SavedNames.SequenceEqual(new[] { "Beta" }), "a deleted blueprint's name should be usable again; the menu lists: " + string.Join(", ", Bp.SavedNames));
                Bp.DrawArchitectTab();
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        [Scenario("blueprints-menu-draws-for-a-long-item-list", MapSize = 80)]
        static void MenuDrawsForLongList(ScenarioContext s) {
            Bp.Begin(s);
            try {
                var big = MakeBigBlueprint(s, "BigOne");
                Bp.DrawArchitectTab();
                s.RequireNoLogErrors();
                var options = Bp.OpenRightClickMenu(big);
                var labels = options.Select(o => o.Label).ToList();
                s.Check(labels.Contains("Fluffy.Blueprints.Rename".Translate().Resolve()), "right-click menu should offer rename; it offers: " + string.Join(" | ", labels));
                s.Check(labels.Contains("Fluffy.Blueprints.Remove".Translate().Resolve()), "right-click menu should offer remove; it offers: " + string.Join(" | ", labels));
                s.Check(labels.Contains("Fluffy.Blueprints.SaveToXML".Translate().Resolve()), "right-click menu should offer export; it offers: " + string.Join(" | ", labels));
                Bp.Choose(Bp.Option(options, "Fluffy.Blueprints.Remove".Translate().Resolve()));
                Bp.DrawArchitectTab();
                s.AreEqual(0, Bp.Saved.Count, "blueprints left after removing the long one");
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        [Scenario("blueprints-tab-keeps-blueprints-after-the-category-is-rebuilt", MapSize = 80)]
        static void TabSurvivesRebuild(ScenarioContext s) {
            Bp.Begin(s);
            try {
                var source = s.Center + Bp.Source;
                s.SpawnBuilding("Wall", source, "BlocksGranite");
                s.SpawnBuilding("Wall", source + new IntVec3(1, 0, 0), "BlocksGranite");
                Bp.Create(s, Bp.DragRect(source, 1, 0), "One");
                Bp.Create(s, Bp.DragRect(source, 1, 0), "Two");
                s.Check(Bp.SavedNames.OrderBy(x => x).SequenceEqual(new[] { "One", "Two" }), "before the rebuild the menu lists One and Two; it lists: " + string.Join(", ", Bp.SavedNames));

                AccessTools.Method(typeof(DesignationCategoryDef), "ResolveDesignators").Invoke(Bp.Category, null);

                s.Check(Bp.SavedNames.OrderBy(x => x).SequenceEqual(new[] { "One", "Two" }), "after the category was rebuilt the menu should still list One and Two; it lists: " + string.Join(", ", Bp.SavedNames));
                s.AreEqual(1, Bp.Tab.Count(d => d.GetType().Name == "Designator_CreateBlueprint"), "create tools in the Blueprints menu after the rebuild");

                var two = Bp.SavedNamed("Two");
                if (two != null) {
                    Bp.Choose(Bp.Option(Bp.OpenRightClickMenu(two), "Fluffy.Blueprints.Remove".Translate().Resolve()));
                    s.Check(Bp.SavedNames.SequenceEqual(new[] { "One" }), "removing Two after a rebuild should leave One; the menu lists: " + string.Join(", ", Bp.SavedNames));
                }
                var again = Bp.Create(s, Bp.DragRect(source, 1, 0), "Two");
                s.Check(Bp.SavedNamed("Two") != null, "the name Two should be usable again after it was removed");
                Bp.DrawArchitectTab();
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        [Scenario("blueprints-copy-gizmo-draws-outside-a-map", MapSize = 30)]
        static void GizmoOutsideAMap(ScenarioContext s) {
            Bp.Begin(s);
            var root = Current.Root;
            var play = root.uiRoot;
            try {
                root.uiRoot = new UIRoot_Entry();
                Event.current = new Event { type = EventType.Repaint };
                GizmoGridDrawer.DrawGizmoGridFor(new List<object>(), out _);
                s.RequireNoLogErrors();
            }
            finally {
                root.uiRoot = play;
                Bp.End();
            }
        }
    }
}
