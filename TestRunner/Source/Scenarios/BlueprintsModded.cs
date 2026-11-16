using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class BlueprintsModded
    {
        static readonly List<string> ghostThingsDrawn = new List<string>();

        static void CountGhostThing(ThingDef thingDef) => ghostThingsDrawn.Add(thingDef.defName);

        static string DrawWholeGhost(Designator blueprint, IntVec3 origin) {
            var model = AccessTools.Property(blueprint.GetType(), "Blueprint").GetValue(blueprint);
            try {
                AccessTools.Method(model.GetType(), "DrawGhost").Invoke(model, new object[] { origin });
                return null;
            }
            catch (System.Reflection.TargetInvocationException e) {
                return e.InnerException.ToString();
            }
        }

        static void CheckSorted(ScenarioContext s, string[] expected, string what) {
            var drawn = ghostThingsDrawn.OrderBy(x => x, System.StringComparer.Ordinal).ToArray();
            s.Check(expected.SequenceEqual(drawn), what + ": expected " + string.Join(" ", expected) + " but drew " + string.Join(" ", drawn));
        }

        [Scenario("blueprints-ghost-draws-a-linked-thing-with-a-plain-graphic", MapSize = 80)]
        static void GhostOfLinkedThingWithPlainGraphic(ScenarioContext s) {
            Bp.Begin(s);
            var harmony = new Harmony("rimhunter.blueprints.testrunner.ghost");
            try {
                var source = s.Center + Bp.Source;
                var parts = new List<Part>();
                for (var x = -3; x <= 3; x++)
                    parts.Add(x == 0
                        ? Part.Thing("BlueprintsTest_PlainLinked", x, 0)
                        : Part.Thing("Wall", x, 0, "BlocksGranite"));
                parts.Add(Part.Thing("TorchLamp", -3, 2));
                parts.Add(Part.Thing("TorchLamp", 3, 2));
                foreach (var p in parts) Bp.Spawn(s, p, source);
                var created = Bp.Create(s, Bp.DragRect(source, 3, 2), "WindowRow");
                harmony.Patch(AccessTools.Method(typeof(GhostDrawer), nameof(GhostDrawer.DrawGhostThing)),
                    prefix: new HarmonyMethod(typeof(BlueprintsModded), nameof(CountGhostThing)));
                ghostThingsDrawn.Clear();
                var failure = DrawWholeGhost(created, s.Center + Bp.Dest);
                s.Check(failure == null, "drawing the whole ghost, walls included, completed without an exception: " + failure);
                CheckSorted(s, new[] { "BlueprintsTest_PlainLinked", "TorchLamp", "TorchLamp" }, "things drawn as plain ghosts besides the walls");
                s.RequireNoLogErrors();
            }
            finally {
                harmony.UnpatchAll(harmony.Id);
                Bp.End();
            }
        }

        internal const string BadHygienePackage = "dubwise.dubsbadhygiene";

        internal static readonly string[] BadHygieneResearch = { "Plumbing", "ModernFixtures", "Saunas" };

        internal static Part Mod(string def, int x, int z, Rot4? rot = null) =>
            Part.Thing(def, x, z, ScenarioContext.Def(def).MadeFromStuff ? GenStuff.DefaultStuffFor(ScenarioContext.Def(def)).defName : null, rot ?? Rot4.North);

        internal static List<Part> Bathroom() {
            var parts = new List<Part>();
            for (var x = -3; x <= 3; x++)
            for (var z = -2; z <= 2; z++) {
                parts.Add(Part.Floor("WoodPlankFloor", x, z));
                if (x == -3 || x == 3 || z == -2 || z == 2)
                    parts.Add(x == 0 && z == -2
                        ? Part.Thing("Door", x, z, "WoodLog")
                        : Part.Thing("Wall", x, z, "BlocksGranite"));
            }
            parts.Add(Mod("ToiletStuff", -2, 1, Rot4.South));
            parts.Add(Mod("KitchenSink", 0, 1));
            parts.Add(Mod("ShowerSimple", 2, 1, Rot4.West));
            parts.Add(Mod("LitterBox", -2, 0));
            parts.Add(Mod("BathMat", -1, 0));
            parts.Add(Mod("sewagePipeStuff", 0, 0));
            parts.Add(Mod("WashBucket", 1, 0));
            parts.Add(Mod("DBHSaunaSeating", 2, 0, Rot4.East));
            parts.Add(Mod("sewagePipeStuff", -2, -1));
            parts.Add(Mod("sewagePipeStuff", -1, -1));
            parts.Add(Mod("BathtubStuff", 1, -1, Rot4.East));
            return parts;
        }

        internal static void FinishResearch(params string[] names) {
            foreach (var name in names)
                Find.ResearchManager.FinishProject(DefDatabase<ResearchProjectDef>.GetNamed(name), doCompletionDialog: false);
        }

        internal static void CheckNineBuildingsStanding(ScenarioContext s, List<Part> parts, string when) {
            var modded = parts.Where(p => !p.IsTerrain && ScenarioContext.Def(p.Def).modContentPack?.PackageId.ToLowerInvariant() == BadHygienePackage).Select(p => p.Def).Distinct().ToList();
            s.AreEqual(9, modded.Count, "different Dubs Bad Hygiene buildings standing in the room " + when + ": " + string.Join(", ", modded));
        }

        [Scenario("blueprints-capture-and-place-buildings-from-dubs-bad-hygiene", MapSize = 80, Mods = "836308268")]
        static void BadHygieneBuildings(ScenarioContext s) {
            Bp.RequireBadHygiene();
            Bp.Begin(s);
            var harmony = new Harmony("rimhunter.blueprints.testrunner.dbh");
            try {
                FinishResearch(BadHygieneResearch);
                var source = s.Center + Bp.Source;
                var parts = Bp.BuildRoom(s, Bathroom(), source);
                CheckNineBuildingsStanding(s, parts, "after building it");
                var created = Bp.Create(s, Bp.DragRect(source, 3, 2), "Bathroom");

                harmony.Patch(AccessTools.Method(typeof(GhostDrawer), nameof(GhostDrawer.DrawGhostThing)),
                    prefix: new HarmonyMethod(typeof(BlueprintsModded), nameof(CountGhostThing)));
                ghostThingsDrawn.Clear();
                var failure = DrawWholeGhost(created, s.Center + Bp.Dest);
                s.Check(failure == null, "drawing the whole ghost, with linked walls, pipes and sauna seating, completed without an exception: " + failure);
                CheckSorted(s, new[] { "BathMat", "BathtubStuff", "Door", "KitchenSink", "LitterBox", "ShowerSimple", "ToiletStuff", "WashBucket" }, "things drawn as plain ghosts (the linked walls, pipes and seating are drawn from the atlas)");
                Bp.DrawArchitectTab();

                var dest = s.Center + Bp.Dest;
                Bp.Place(created, dest);
                Bp.CheckLayout(s, Bp.ExpectedAfter(parts, dest, 0), "placed unrotated");
                Bp.ClearConstruction(s.Map);
                Bp.RotateRight(created);
                var turned = s.Center + new IntVec3(18, 0, 14);
                Bp.Place(created, turned);
                Bp.CheckLayout(s, Bp.ExpectedAfter(parts, turned, 1), "placed one step clockwise");
                s.RequireNoLogErrors();
            }
            finally {
                harmony.UnpatchAll(harmony.Id);
                Bp.End();
            }
        }
    }
}
