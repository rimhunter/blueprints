using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class BlueprintsPlace
    {
        [Scenario("blueprints-create-and-place", MapSize = 80)]
        static void CreateAndPlace(ScenarioContext s) {
            Bp.Begin(s);
            try {
                var parts = Bp.Room();
                var source = s.Center + Bp.Source;
                var dest = s.Center + Bp.Dest;
                parts = Bp.BuildRoom(s, parts, source);
                var created = Bp.Create(s, Bp.DragRect(source, 3, 2), "RoomOne");
                s.AreEqual("RoomOne", created.Label, "the new blueprint's name");
                s.Check(Bp.SavedNames.Contains("RoomOne"), "the Blueprints menu should list the new blueprint; it lists: " + string.Join(", ", Bp.SavedNames));
                Bp.Place(created, dest);
                Bp.CheckLayout(s, Bp.ExpectedAfter(parts, dest, 0), "placed unrotated");
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        [Scenario("blueprints-place-rotated", MapSize = 80)]
        static void PlaceRotated(ScenarioContext s) {
            Bp.Begin(s);
            try {
                var parts = Bp.Room();
                parts.Add(Bp.Table());
                var source = s.Center + Bp.Source;
                parts = Bp.BuildRoom(s, parts, source, 4);
                var created = Bp.Create(s, Bp.DragRect(source, 3, 4), "RoomTwo");
                var steps = new[] { 1, 2, 3 };
                var spots = new[] { new IntVec3(18, 0, -14), new IntVec3(18, 0, 0), new IntVec3(18, 0, 14) };
                var turned = 0;
                for (var i = 0; i < steps.Length; i++) {
                    while (turned < steps[i]) { Bp.RotateRight(created); turned++; }
                    var dest = s.Center + spots[i];
                    Bp.Place(created, dest);
                    var expected = Bp.ExpectedAfter(parts, dest, steps[i]);
                    var actual = Bp.PlacedConstruction(s.Map).Where(a => InRange(a, dest)).ToList();
                    var diff = Bp.Diff(expected, actual);
                    s.Check(diff.Count == 0, "rotated " + steps[i] + " step(s) clockwise: " + diff.Count + " difference(s) from the rotated layout: " + string.Join(" ;; ", diff.Take(12)));
                }

                Bp.RotateLeft(created);
                var back = s.Center + new IntVec3(0, 0, 20);
                Bp.Place(created, back);
                var expectedBack = Bp.ExpectedAfter(parts, back, 2);
                var actualBack = Bp.PlacedConstruction(s.Map).Where(a => InRange(a, back)).ToList();
                var diffBack = Bp.Diff(expectedBack, actualBack);
                s.Check(diffBack.Count == 0, "rotated three steps clockwise then one step counterclockwise: " + diffBack.Count + " difference(s) from the layout turned half way round: " + string.Join(" ;; ", diffBack.Take(12)));
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        [Scenario("blueprints-flip-and-warn", MapSize = 80)]
        static void FlipAndWarn(ScenarioContext s) {
            Bp.Begin(s);
            try {
                var parts = Bp.Room();
                parts.Add(Part.Thing("BlueprintsTest_EvenBench", -2, 1));
                parts.Add(Bp.Table());
                var source = s.Center + Bp.Source;
                parts = Bp.BuildRoom(s, parts, source, 4);
                var created = Bp.Create(s, Bp.DragRect(source, 3, 4), "RoomFlip");
                var dest = s.Center + Bp.Dest;
                Bp.ClearMessages();
                Bp.Flip(created);
                Bp.Place(created, dest);
                var mirrored = parts.Select(p => Mirror(p)).ToList();
                var expected = Bp.ExpectedAfter(mirrored, dest, 0);
                var diff = Bp.Diff(expected, Bp.PlacedConstruction(s.Map));
                s.Check(diff.Count == 0, "flipped layout: " + diff.Count + " difference(s) from the mirrored layout: " + string.Join(" ;; ", diff.Take(12)));
                var warning = "Fluffy.Blueprints.UnFlippable.HasInteractionCell".Translate(ScenarioContext.Def("BlueprintsTest_EvenBench").LabelCap).Resolve();
                var shown = Bp.LiveMessages().Count(m => m == warning);
                s.AreEqual(1, shown, "times the flip warning for the bench with an interaction spot is shown");
                Bp.Flip(created);
                s.AreEqual(1, Bp.LiveMessages().Count(m => m == warning), "times the warning is shown after flipping back (it is shown once per blueprint)");
                Bp.RotateRight(created);
                Bp.Flip(created);
                var second = s.Center + new IntVec3(18, 0, 14);
                Bp.Place(created, second);
                var expectedTurned = Bp.ExpectedAfter(parts.Where(p => p.Def != "BlueprintsTest_EvenBench").Select(p => Mirror(Turned(p))).ToList(), second, 0);
                var actualTurned = Bp.PlacedConstruction(s.Map).Where(a => InRange(a, second) && !a.StartsWith("BlueprintsTest_EvenBench|")).ToList();
                var diffTurned = Bp.Diff(expectedTurned, actualTurned);
                s.Check(diffTurned.Count == 0, "turned one step clockwise and then flipped: " + diffTurned.Count + " difference(s): " + string.Join(" ;; ", diffTurned.Take(12)));
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        static Part Turned(Part p) {
            if (p.IsTerrain) return Part.Floor(p.Def, Bp.RotateOffset(p.At, 1));
            var def = ScenarioContext.Def(p.Def);
            var rot = def.rotatable ? new Rot4((p.Rot.AsInt + 1) % 4) : p.Rot;
            var turned = GenAdj.OccupiedRect(p.At, p.Rot, def.Size).Cells.Select(c => Bp.RotateOffset(c, 1)).ToList();
            foreach (var candidate in turned) {
                var cells = GenAdj.OccupiedRect(candidate, rot, def.Size).Cells.ToList();
                if (cells.Count == turned.Count && cells.All(turned.Contains))
                    return Part.Thing(p.Def, candidate, p.Stuff, rot);
            }
            throw new ScenarioFailure("cannot turn " + p.Def);
        }

        static Part Mirror(Part p) {
            if (p.IsTerrain) return Part.Floor(p.Def, -p.At.x, p.At.z);
            var def = ScenarioContext.Def(p.Def);
            var rot = def.rotatable && (p.Rot == Rot4.East || p.Rot == Rot4.West) ? p.Rot.Opposite : p.Rot;
            var mirrored = GenAdj.OccupiedRect(p.At, p.Rot, def.Size).Cells.Select(c => new IntVec3(-c.x, 0, c.z)).ToList();
            foreach (var candidate in mirrored) {
                var cells = GenAdj.OccupiedRect(candidate, rot, def.Size).Cells.ToList();
                if (cells.Count == mirrored.Count && cells.All(mirrored.Contains))
                    return Part.Thing(p.Def, candidate, p.Stuff, rot);
            }
            throw new ScenarioFailure("cannot mirror " + p.Def);
        }

        [Scenario("blueprints-choose-stuff", MapSize = 80)]
        static void ChooseStuff(ScenarioContext s) {
            Bp.Begin(s);
            try {
                var parts = Bp.Room();
                var source = s.Center + Bp.Source;
                parts = Bp.BuildRoom(s, parts, source);
                var created = Bp.Create(s, Bp.DragRect(source, 3, 2), "RoomStuff");
                Bp.ChooseStuffForEveryBuildable(created, parts, "Steel");
                var dest = s.Center + Bp.Dest;
                Bp.Place(created, dest);
                var expected = Bp.ExpectedAfter(parts, dest, 0, p => !p.IsTerrain && ScenarioContext.Def(p.Def).MadeFromStuff ? "Steel" : null);
                var diff = Bp.Diff(expected, Bp.PlacedConstruction(s.Map));
                s.Check(diff.Count == 0, "after choosing steel: " + diff.Count + " difference(s): " + string.Join(" ;; ", diff.Take(12)));
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        [Scenario("blueprints-place-as-plans", MapSize = 80)]
        static void PlaceAsPlans(ScenarioContext s) {
            Bp.Begin(s);
            try {
                var parts = Bp.Room();
                var source = s.Center + Bp.Source;
                parts = Bp.BuildRoom(s, parts, source);
                var created = Bp.Create(s, Bp.DragRect(source, 3, 2), "RoomPlan");
                var dest = s.Center + Bp.Dest;
                Bp.Place(created, dest, shift: true);
                var wallCells = parts.Where(p => p.Def == "Wall").Select(p => dest + p.At).ToList();
                var planned = wallCells.Where(c => s.Map.planManager.PlanAt(c) != null).ToList();
                s.AreEqual(wallCells.Count, planned.Count, "wall cells of the placed copy that are in a plan");
                s.AreEqual(0, Bp.PlacedConstruction(s.Map).Count, "construction orders created by a shift-click (plans only)");
                var remove = new Designator_Plan_Remove();
                var removable = wallCells.Concat(parts.Where(p => p.Def == "Door").Select(p => dest + p.At)).Where(c => remove.CanDesignateCell(c).Accepted).ToList();
                s.Check(removable.Count == wallCells.Count + 1 || removable.Count == wallCells.Count, "vanilla plan removal should accept the planned cells; it accepted " + removable.Count);
                remove.DesignateMultiCell(removable);
                var left = new List<string>();
                foreach (var c in wallCells.Concat(parts.Select(p => dest + p.At))) {
                    if (s.Map.planManager.PlanAt(c) != null) left.Add("plan at " + c);
                    if (s.Map.designationManager.AllDesignationsAt(c).Any(d => d.def == DesignationDefOf.Plan)) left.Add("stuck plan tile at " + c);
                }
                s.AreEqual(0, left.Distinct().Count(), "plan tiles left after vanilla plan removal: " + string.Join(", ", left.Distinct().Take(8)));
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        [Scenario("blueprints-shift-click-skips-parts-already-placed", MapSize = 80)]
        static void ShiftClickSkipsPlacedParts(ScenarioContext s) {
            Bp.Begin(s);
            try {
                var parts = Bp.Room();
                var source = s.Center + Bp.Source;
                parts = Bp.BuildRoom(s, parts, source);
                var created = Bp.Create(s, Bp.DragRect(source, 3, 2), "RoomPlanSkip");
                var wallCells = parts.Where(p => p.Def == "Wall").Select(p => p.At).ToList();

                var dest = s.Center + Bp.Dest;
                var built = dest + new IntVec3(3, 0, 2);
                s.SpawnBuilding("Wall", built, "BlocksGranite");
                Bp.Place(created, dest, shift: true);
                s.Check(s.Map.planManager.PlanAt(built) == null, "a wall that is already built at " + built + " should not be planned over");
                var planned = wallCells.Where(c => s.Map.planManager.PlanAt(dest + c) != null).ToList();
                s.AreEqual(wallCells.Count - 1, planned.Count, "wall cells planned around the built wall");


                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        [Scenario("blueprints-report-floors-that-fail-under-things", MapSize = 80)]
        static void ReportFloorsThatFailUnderThings(ScenarioContext s) {
            Bp.Begin(s);
            try {
                var parts = Bp.Room();
                var source = s.Center + Bp.Source;
                parts = Bp.BuildRoom(s, parts, source);
                var created = Bp.Create(s, Bp.DragRect(source, 3, 2), "RoomFloors");
                var dest = s.Center + Bp.Dest;

                Bp.ClearMessages();
                Bp.Place(created, dest);
                s.Check(!Bp.LiveMessages().Any(m => m.Contains("RoomFloors")), "floors left out under the blueprint's own walls are not failures; messages: " + string.Join(" | ", Bp.LiveMessages()));
                Bp.ClearConstruction(s.Map);

                var second = s.Center + new IntVec3(18, 0, 14);
                s.SpawnBuilding("Bed", second + new IntVec3(1, 0, 0), "WoodLog", Rot4.East);
                foreach (var cell in new[] { new IntVec3(1, 0, 0), new IntVec3(2, 0, 0) })
                    s.Map.terrainGrid.SetTerrain(second + cell, TerrainDefOf.WaterDeep);
                Bp.ClearMessages();
                Bp.Place(created, second);
                var total = parts.Count - Bp.FloorsLeftOut(parts);
                var expected = "Fluffy.Blueprints.CouldNotPlaceParts".Translate("RoomFloors", 2, total).Resolve();
                s.Check(Bp.LiveMessages().Contains(expected), "the two floors that cannot be built under the existing bed should be reported as '" + expected + "'; messages: " + string.Join(" | ", Bp.LiveMessages()));
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        [Scenario("blueprints-place-where-parts-do-not-fit", MapSize = 80)]
        static void PlacePartly(ScenarioContext s) {
            Bp.Begin(s);
            try {
                var parts = Bp.Room();
                var source = s.Center + Bp.Source;
                parts = Bp.BuildRoom(s, parts, source);
                var created = Bp.Create(s, Bp.DragRect(source, 3, 2), "RoomPartly");
                var dest = s.Center + Bp.Dest;
                var blocker = new IntVec3(dest.x + 2, 0, dest.z + 1);
                s.SpawnBuilding("Wall", blocker, "BlocksGranite");
                var deep = new IntVec3(dest.x - 3, 0, dest.z - 2);
                s.Map.terrainGrid.SetTerrain(deep, TerrainDefOf.WaterDeep);
                Bp.ClearMessages();
                Bp.Place(created, dest);
                var messages = Bp.LiveMessages();
                s.Check(messages.Any(m => m.Contains("RoomPartly")), "the player should be told some parts of RoomPartly could not be placed; messages: " + string.Join(" | ", messages));
                var placed = Bp.PlacedConstruction(s.Map);
                s.Check(placed.Count > 30, "the parts that fit should still be placed; " + placed.Count + " construction orders exist");
                s.Check(!placed.Any(d => d.StartsWith("Wall|") && d.EndsWith("|" + (deep.x) + "," + deep.z)), "no wall order on deep water");
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        [Scenario("blueprints-capture-only-what-the-player-owns", MapSize = 80)]
        static void CaptureOnlyOwned(ScenarioContext s) {
            Bp.Begin(s);
            try {
                var parts = Bp.Room();
                var source = s.Center + Bp.Source;
                parts = Bp.BuildRoom(s, parts, source);
                var foreign = (Building) Verse.ThingMaker.MakeThing(ScenarioContext.Def("Wall"), ScenarioContext.Def("BlocksGranite"));
                Verse.GenSpawn.Spawn(foreign, source + new IntVec3(0, 0, 1), s.Map);
                s.SpawnItem("Steel", 5, source + new IntVec3(1, 0, 1));
                var created = Bp.Create(s, Bp.DragRect(source, 3, 2), "RoomOwned");
                var dest = s.Center + Bp.Dest;
                Bp.Place(created, dest);
                Bp.CheckLayout(s, Bp.ExpectedAfter(parts, dest, 0), "only player-owned buildings and the terrain");
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        [Scenario("blueprints-research-finished-after-creation", MapSize = 80)]
        static void ResearchAfterCreation(ScenarioContext s) {
            Bp.Begin(s);
            try {
                Find.ResearchManager.ResetAllProgress();
                var electricity = DefDatabase<ResearchProjectDef>.GetNamed("Electricity");
                if (electricity.IsFinished) throw new ScenarioFailure("Electricity research is still finished after resetting research");
                var source = s.Center + Bp.Source;
                s.SpawnBuilding("Wall", source, "BlocksGranite");
                s.SpawnBuilding("StandingLamp", source + new IntVec3(1, 0, 0));
                var created = Bp.Create(s, Bp.DragRect(source, 1, 0), "NeedsPower");
                var dest = s.Center + Bp.Dest;
                Bp.Place(created, dest);
                var first = Bp.PlacedConstruction(s.Map);
                s.Check(first.Count == 1 && first[0].StartsWith("Wall|"), "before the research only the wall can be placed; placed: " + string.Join(" ;; ", first));
                Find.ResearchManager.FinishProject(electricity, doCompletionDialog: false);
                var second = s.Center + new IntVec3(18, 0, 10);
                Bp.Place(created, second, select: false);
                var lamps = Bp.PlacedConstruction(s.Map).Count(d => d.StartsWith("StandingLamp|"));
                s.AreEqual(1, lamps, "standing lamp orders after the research finished, without selecting the blueprint again");
                s.RequireNoLogErrors();
            }
            finally { Find.ResearchManager.DebugSetAllProjectsFinished(); Bp.End(); }
        }

        static bool InRange(string description, IntVec3 dest) {
            var cells = description.Split('|')[3].Split(' ');
            var first = cells[0].Split(',');
            var x = int.Parse(first[0]);
            var z = int.Parse(first[1]);
            return System.Math.Abs(x - dest.x) <= 4 && System.Math.Abs(z - dest.z) <= 4;
        }
    }
}
