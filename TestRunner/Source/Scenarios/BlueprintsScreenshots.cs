using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class BlueprintsScreenshots
    {
        static IEnumerator OpenBlueprintsTab(ScenarioContext s) {
            MainButtonDefOf.Architect.Worker.InterfaceTryActivate();
            yield return null;
            var window = Find.WindowStack.WindowOfType<MainTabWindow_Architect>() ?? throw new ScenarioFailure("the Architect window did not open");
            var panels = (List<ArchitectCategoryTab>)AccessTools.Field(typeof(MainTabWindow_Architect), "desPanelsCached").GetValue(window);
            window.selectedDesPanel = panels.FirstOrDefault(p => p.def == Bp.Category) ?? throw new ScenarioFailure("the Architect window has no " + Bp.CategoryName + " category");
            yield return null;
        }

        static void CloseArchitect() {
            Find.DesignatorManager.Deselect();
            Find.MainTabsRoot.EscapeCurrentTab(playSound: false);
        }

        [Scenario("screenshot-blueprints-menu-lists-the-saved-blueprints", MapSize = 80, Windowed = true, MaxSeconds = 150)]
        static IEnumerator MenuListsSavedBlueprints(ScenarioContext s) {
            Bp.Begin(s);
            try {
                var source = s.Center + Bp.Source;
                Bp.BuildRoom(s, Bp.Room(), source);
                Bp.Create(s, Bp.DragRect(source, 3, 2), "Bedroom");
                Bp.Create(s, Bp.DragRect(source, 1, 1), "Corner");
                Find.DesignatorManager.Deselect();
                s.LookAt(source, 24);
                yield return OpenBlueprintsTab(s);
                var shot = s.Screenshot("01-blueprints-menu");
                yield return shot;
                s.Check(shot.ShowsText(Bp.CreateTool.LabelCap), "the Blueprints menu should show the create tool '" + Bp.CreateTool.LabelCap + "'; it showed: " + shot.TextsMatching("Blueprint"));
                foreach (var name in new[] { "Bedroom", "Corner" })
                    s.Check(shot.ShowsText(name), "the Blueprints menu should list " + name + "; it showed: " + shot.TextsMatching(name.Substring(0, 3)));
                s.RequireNoLogErrors();
            }
            finally {
                CloseArchitect();
                Bp.End();
            }
            yield return null;
        }

        [Scenario("screenshot-blueprints-a-blueprint-being-created", MapSize = 80, Windowed = true, MaxSeconds = 150)]
        static IEnumerator BlueprintBeingCreated(ScenarioContext s) {
            Bp.Begin(s);
            try {
                var source = s.Center + Bp.Source;
                Bp.BuildRoom(s, Bp.Room(), source);
                s.LookAt(source, 24);
                Bp.Create(s, Bp.DragRect(source, 3, 2), null);
                s.Check(Bp.NameDialog != null, "dragging the create tool over the room should open the naming dialog");
                yield return null;
                var shot = s.Screenshot("02-naming-a-new-blueprint");
                yield return shot;
                var ok = "OK".Translate().Resolve();
                s.Check(shot.ShowsText(ok), "the naming dialog should show its '" + ok + "' button; the screenshot showed: " + string.Join(" | ", shot.Texts.Select(t => t.Text).Distinct().Take(20)));
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
            yield return null;
        }

        [Scenario("screenshot-blueprints-rotated-placement-with-a-chosen-stuff", MapSize = 80, Windowed = true, MaxSeconds = 150)]
        static IEnumerator RotatedPlacementWithStuff(ScenarioContext s) {
            Bp.Begin(s);
            try {
                var parts = Bp.Room();
                parts.Add(Bp.Table());
                var source = s.Center + Bp.Source;
                parts = Bp.BuildRoom(s, parts, source, 4);
                var created = Bp.Create(s, Bp.DragRect(source, 3, 4), "Steel_Room");
                Bp.ChooseStuffForEveryBuildable(created, parts, "Steel");
                Bp.RotateRight(created);
                var dest = s.Center + Bp.Dest;
                Bp.Place(created, dest);
                Bp.CheckLayout(s, Bp.ExpectedAfter(parts, dest, 1, p => !p.IsTerrain && ScenarioContext.Def(p.Def).MadeFromStuff ? "Steel" : null), "placed turned one step clockwise in steel");
                s.LookAt(dest, 20);
                yield return OpenBlueprintsTab(s);
                s.Check(Find.DesignatorManager.SelectedDesignator == created, "the blueprint should still be the selected tool");
                var shot = s.Screenshot("03-rotated-placement-in-steel");
                yield return shot;
                var steel = ScenarioContext.Def("Steel");
                foreach (var def in parts.Where(p => !p.IsTerrain).Select(p => ScenarioContext.Def(p.Def)).Where(d => d.MadeFromStuff && d.costStuffCount > 0).Distinct()) {
                    var line = GenLabel.ThingLabel(def, steel).CapitalizeFirst();
                    s.Check(shot.ShowsText(line), "the info panel should show the clickable stuff line '" + line + "'; the screenshot showed: " + shot.TextsMatching(steel.LabelCap));
                }
                s.Check(shot.ShowsText(KeyBindingDefOf.Designator_RotateRight.MainKeyLabel), "the rotation controls of the Blueprints tool should be on screen; the screenshot showed: " + string.Join(" | ", shot.Texts.Select(t => t.Text).Distinct().Take(20)));
                s.RequireNoLogErrors();
            }
            finally {
                CloseArchitect();
                Bp.End();
            }
            yield return null;
        }

        static Blueprint_Build DoorOrderAt(ScenarioContext s, IntVec3 cell) =>
            s.Map.thingGrid.ThingsListAt(cell).OfType<Blueprint_Build>().FirstOrDefault(b => b.def.entityDefToBuild.defName == "Door");

        [Scenario("blueprints-a-placed-door-takes-its-rotation-from-its-neighbors-once-drawn", MapSize = 80, Windowed = true, MaxSeconds = 150)]
        static IEnumerator DoorRotationFollowsNeighbors(ScenarioContext s) {
            Bp.Begin(s);
            try {
                var parts = Bp.Room();
                var source = s.Center + Bp.Source;
                parts = Bp.BuildRoom(s, parts, source);
                var created = Bp.Create(s, Bp.DragRect(source, 3, 2), "Door_Room");
                var doorAt = parts.First(p => p.Def == "Door").At;

                var dest = s.Center + Bp.Dest;
                Bp.Place(created, dest);
                s.LookAt(dest, 20);
                yield return null;
                yield return null;
                var door = DoorOrderAt(s, dest + doorAt);
                s.Check(door != null, "the placed copy should hold a door order at " + (dest + doorAt));
                if (door == null) yield break;
                s.AreEqual(Rot4.North, door.Rotation, "rotation of a door order in a wall that runs east to west, after the game drew it");

                Bp.RotateRight(created);
                var turned = s.Center + new IntVec3(18, 0, -14);
                Bp.Place(created, turned);
                var turnedDoorAt = turned + Bp.RotateOffset(doorAt, 1);
                s.LookAt(turned, 20);
                yield return null;
                yield return null;
                var turnedDoor = DoorOrderAt(s, turnedDoorAt);
                s.Check(turnedDoor != null, "the copy turned one step clockwise should hold a door order at " + turnedDoorAt);
                if (turnedDoor == null) yield break;
                s.AreEqual(Rot4.East, turnedDoor.Rotation, "rotation of a door order in a wall that runs north to south, after the game drew it");
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
            yield return null;
        }

        [Scenario("screenshot-blueprints-placed-as-plans", MapSize = 80, Windowed = true, MaxSeconds = 150)]
        static IEnumerator PlacedAsPlans(ScenarioContext s) {
            Bp.Begin(s);
            try {
                var parts = Bp.Room();
                var source = s.Center + Bp.Source;
                parts = Bp.BuildRoom(s, parts, source);
                var created = Bp.Create(s, Bp.DragRect(source, 3, 2), "Plan_Room");
                var dest = s.Center + Bp.Dest;
                Bp.Place(created, dest, shift: true);
                var wallCells = parts.Where(p => p.Def == "Wall").Select(p => dest + p.At).ToList();
                s.AreEqual(wallCells.Count, wallCells.Count(c => s.Map.planManager.PlanAt(c) != null), "wall cells of the placed copy that are in a plan");
                s.AreEqual(0, Bp.PlacedConstruction(s.Map).Count, "construction orders created by a shift-click");
                Find.DesignatorManager.Deselect();
                s.LookAt(dest, 20);
                yield return null;
                var shot = s.Screenshot("04-placed-as-plans");
                yield return shot;
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
            yield return null;
        }
    }
}
