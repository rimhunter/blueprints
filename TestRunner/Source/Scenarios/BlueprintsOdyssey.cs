using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class BlueprintsOdyssey
    {
        const string Odyssey = "ludeon.rimworld.odyssey";
        static readonly string[] GravResearch = { "BasicGravtech" };

        static void Prepare(ScenarioContext s) {
            if (!ModsConfig.OdysseyActive) throw new ScenarioFailure("Odyssey is not loaded");
            Find.ResearchManager.gravEngineInspected = true;
            foreach (var name in GravResearch) {
                var project = DefDatabase<ResearchProjectDef>.GetNamed(name);
                if (!project.IsFinished) Find.ResearchManager.FinishProject(project, doCompletionDialog: false);
            }
        }

        static Part Thing(string def, int x, int z, Rot4? rot = null, string stuff = null) =>
            Part.Thing(def, x, z, stuff ?? (ScenarioContext.Def(def).MadeFromStuff ? "Steel" : null), rot ?? Rot4.North);

        static Part Terrain(string def, int x, int z) => Part.Floor(def, x, z);

        static List<Part> Substructure(int halfX, int fromZ, int toZ) {
            var parts = new List<Part>();
            for (var x = -halfX; x <= halfX; x++)
            for (var z = fromZ; z <= toZ; z++)
                parts.Add(Terrain("Substructure", x, z));
            return parts;
        }

        static List<Part> Gravship() {
            var parts = Substructure(5, -3, 4);
            for (var x = -5; x <= 5; x++) parts.Add(Thing("GravshipHull", x, 4));
            parts.Add(Thing("PilotConsole", -3, 2, Rot4.East));
            parts.Add(Thing("ChemfuelTank", 3, 1));
            parts.Add(Thing("SmallThruster", 4, -2, Rot4.North));
            return parts;
        }

        static List<Part> ShipWithVanillaParts() {
            var parts = Gravship();
            parts.Add(Thing("TorchLamp", -5, -5));
            parts.Add(Terrain("WoodPlankFloor", -4, -5));
            parts.Add(Terrain("WoodPlankFloor", 0, 0));
            parts.Add(Terrain("WoodPlankFloor", 1, 0));
            return parts;
        }

        static void SetUpPart(ScenarioContext s, Part p, IntVec3 center) {
            var cell = center + p.At;
            if (p.IsTerrain) {
                var def = DefDatabase<TerrainDef>.GetNamed(p.Def);
                if (def.isFoundation) s.Map.terrainGrid.SetFoundation(cell, def);
                else s.Map.terrainGrid.SetTerrain(cell, def);
                return;
            }
            Bp.Spawn(s, p, center);
        }

        static void BuildShip(ScenarioContext s, IEnumerable<Part> parts, IntVec3 center) {
            s.SpawnBuilding("GravEngine", center);
            foreach (var p in parts.Where(p => p.IsTerrain)) SetUpPart(s, p, center);
            foreach (var p in parts.Where(p => !p.IsTerrain)) SetUpPart(s, p, center);
        }

        static List<Part> Harvest(Map map, IntVec3 center, int halfX, int halfZ) {
            var result = new List<Part>();
            var rect = CellRect.FromLimits(center.x - halfX, center.z - halfZ, center.x + halfX, center.z + halfZ);
            foreach (var cell in rect) {
                var foundation = map.terrainGrid.FoundationAt(cell);
                if (foundation != null && foundation.designationCategory != null)
                    result.Add(Part.Floor(foundation.defName, cell - center));
                var terrain = map.terrainGrid.TerrainAt(cell);
                if (terrain != foundation && terrain.designationCategory != null)
                    result.Add(Part.Floor(terrain.defName, cell - center));
            }
            foreach (var thing in rect.SelectMany(c => c.GetThingList(map)).OfType<Building>().Where(b => b.Faction == Faction.OfPlayer && b.def.designationCategory != null).Distinct())
                result.Add(Part.Thing(thing.def.defName, thing.Position - center, thing.Stuff?.defName, thing.Rotation));
            return result;
        }

        static void CheckLayout(ScenarioContext s, List<string> expected, string when) {
            var diff = Bp.Diff(expected, Bp.PlacedConstruction(s.Map));
            var summary = diff.GroupBy(d => d.Split('|')[0]).Select(g => g.Count() + "x " + g.Key + " [" + string.Join(" ", g.Take(3).Select(d => d.Split('|').Last())) + "]");
            s.Check(diff.Count == 0, when + ": " + diff.Count + " difference(s) from the saved layout: " + string.Join(" ;; ", summary));
        }

        static readonly IntVec3 ShipSource = new IntVec3(-22, 0, 0);
        static readonly IntVec3 ShipDest = new IntVec3(20, 0, 0);

        static void SetUpEngineAt(ScenarioContext s, IntVec3 center) => s.SpawnBuilding("GravEngine", center);

        static void BuildOrders(ScenarioContext s, bool floorsToo = false) {
            foreach (var order in s.Map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint).OfType<Blueprint_Build>().Where(b => b.def.entityDefToBuild is TerrainDef t && (t.isFoundation || floorsToo)).ToList()) {
                var cell = order.Position;
                var terrain = (TerrainDef)order.def.entityDefToBuild;
                order.Destroy();
                if (terrain.isFoundation) s.Map.terrainGrid.SetFoundation(cell, terrain);
                else s.Map.terrainGrid.SetTerrain(cell, terrain);
            }
        }

        static List<string> SubstructureOrders(Map map) => Bp.PlacedConstruction(map).Where(o => o.StartsWith("Substructure|")).ToList();

        static List<string> Notices() => Bp.LiveMessages();

        [Scenario("blueprints-odyssey-a-gravship-is-captured-and-placed-around-a-new-grav-engine", MapSize = 100, MaxSeconds = 150, Mods = "ludeon.rimworld.odyssey")]
        static void GravshipCapturedAndPlaced(ScenarioContext s) {
            Bp.Begin(s);
            try {
                Prepare(s);
                var source = s.Center + ShipSource;
                var dest = s.Center + ShipDest;
                var turned = s.Center + new IntVec3(20, 0, 24);
                var ship = Gravship();
                BuildShip(s, ship, source);
                SetUpEngineAt(s, dest);
                SetUpEngineAt(s, turned);
                var parts = Harvest(s.Map, source, 6, 5);
                s.AreEqual(88, parts.Count(p => p.IsTerrain && p.Def == "Substructure"), "cells of substructure the ship stands on");
                s.AreEqual(ship.Count, parts.Count, "parts of the ship found on the map");
                var created = Bp.Create(s, Bp.DragRect(source, 6, 5), "Ship");
                var things = parts.Where(p => !p.IsTerrain).ToList();

                Bp.ClearMessages();
                Bp.Place(created, dest);
                CheckLayout(s, Bp.ExpectedAfter(parts.Where(p => p.IsTerrain).ToList(), dest, 0), "first stamp: the substructure goes down and nothing else can yet");
                var told = Notices();
                var waiting = things.Count;
                s.Check(told.Any(m => m.Contains("Ship") && m.Contains(waiting + " parts") && m.Contains("not built yet")), "the first stamp should tell the player that " + waiting + " parts wait for the substructure to be built; the messages are: " + (told.Count == 0 ? "none" : string.Join(" | ", told)));
                s.Check(!told.Any(m => m.Contains("could not be placed")), "parts that only wait for the substructure are not reported as left out; the messages are: " + string.Join(" | ", told));
                BuildOrders(s);
                Bp.ClearMessages();
                Bp.Place(created, dest);
                CheckLayout(s, Bp.ExpectedAfter(things, dest, 0), "second stamp, once the substructure is built: every gravship part");
                s.Check(!Notices().Any(m => m.Contains("Ship")), "the second stamp should place everything; the messages are: " + string.Join(" | ", Notices()));

                Bp.ClearConstruction(s.Map);
                Bp.RotateRight(created);
                Bp.Place(created, turned);
                CheckLayout(s, Bp.ExpectedAfter(parts.Where(p => p.IsTerrain).ToList(), turned, 1), "turned one step clockwise, first stamp");
                BuildOrders(s);
                Bp.Place(created, turned);
                CheckLayout(s, Bp.ExpectedAfter(things, turned, 1), "turned one step clockwise, second stamp");
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        [Scenario("screenshot-blueprints-odyssey-gravship-blueprint", MapSize = 100, Windowed = true, MaxSeconds = 150, Mods = "ludeon.rimworld.odyssey")]
        static IEnumerator GravshipScreenshot(ScenarioContext s) {
            Bp.Begin(s);
            try {
                Prepare(s);
                var source = s.Center + ShipSource;
                var dest = s.Center + ShipDest;
                BuildShip(s, Gravship(), source);
                SetUpEngineAt(s, dest);
                var parts = Harvest(s.Map, source, 6, 5);
                var created = Bp.Create(s, Bp.DragRect(source, 6, 5), "Ship");
                Bp.Place(created, dest);
                BuildOrders(s);
                Bp.ClearMessages();
                Bp.Place(created, dest);
                CheckLayout(s, Bp.ExpectedAfter(parts.Where(p => !p.IsTerrain).ToList(), dest, 0), "the gravship parts placed once the substructure is built");
                s.Check(!Notices().Any(m => m.Contains("Ship")), "the second stamp should place everything; the messages are: " + string.Join(" | ", Notices()));
                Find.DesignatorManager.Deselect();
                s.LookAt(dest, 28);
                yield return null;
                var shot = s.Screenshot("07-odyssey-gravship-blueprint");
                yield return shot;
                s.Check(!shot.ShowsText("foundation"), "the screenshot should show no message about a foundation that is not built yet; it showed: " + shot.TextsMatching("foundation"));
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
            yield return null;
        }

        static List<Part> DeckWithFloors() {
            var parts = Substructure(4, -2, 2);
            foreach (var (x, z) in new[] { (-2, -1), (-1, -1), (-2, 0), (-1, 0), (-3, 1) })
                parts.Add(Terrain("WoodPlankFloor", x, z));
            parts.Add(Thing("GravshipHull", -4, 2));
            parts.Add(Thing("GravshipHull", 4, 2));
            parts.Add(Thing("ChemfuelTank", 3, 1));
            return parts;
        }

        [Scenario("blueprints-odyssey-a-floor-laid-on-substructure-keeps-both-layers", MapSize = 100, MaxSeconds = 150, Mods = "ludeon.rimworld.odyssey")]
        static void FloorOnSubstructure(ScenarioContext s) {
            Bp.Begin(s);
            try {
                Prepare(s);
                var source = s.Center + ShipSource;
                var dest = s.Center + ShipDest;
                var deck = DeckWithFloors();
                BuildShip(s, deck, source);
                SetUpEngineAt(s, dest);
                var parts = Harvest(s.Map, source, 5, 3);
                s.AreEqual(45, parts.Count(p => p.Def == "Substructure"), "cells of substructure under the deck");
                s.AreEqual(5, parts.Count(p => p.Def == "WoodPlankFloor"), "floors on the deck");
                var created = Bp.Create(s, Bp.DragRect(source, 5, 3), "Deck");
                var layers = parts.Where(p => p.IsTerrain && p.Def == "Substructure").ToList();
                var rest = parts.Where(p => !(p.IsTerrain && p.Def == "Substructure")).ToList();

                Bp.ClearMessages();
                Bp.Place(created, dest);
                CheckLayout(s, Bp.ExpectedAfter(layers, dest, 0), "first stamp: the substructure goes down under every cell, floors included");
                BuildOrders(s);
                Bp.ClearMessages();
                Bp.Place(created, dest);
                CheckLayout(s, Bp.ExpectedAfter(rest, dest, 0), "second stamp, once the substructure is built: the floors and the ship parts");
                s.Check(!Notices().Any(m => m.Contains("Deck")), "the second stamp should place everything; the messages are: " + string.Join(" | ", Notices()));
                BuildOrders(s, floorsToo: true);
                var before = Bp.PlacedConstruction(s.Map);
                Bp.ClearMessages();
                Bp.Place(created, dest);
                CheckLayout(s, before, "third stamp over a finished deck: nothing new");
                s.Check(!Notices().Any(m => m.Contains("Deck")), "stamping over a finished deck should not report parts left out; the messages are: " + string.Join(" | ", Notices()));
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        static bool InFootprint(IntVec3 cell, IntVec3 engine) {
            var dx = cell.x - engine.x;
            var dz = cell.z - engine.z;
            return dx * dx + dz * dz <= 18.9 * 18.9;
        }

        [Scenario("blueprints-odyssey-parts-where-the-grav-engine-cannot-reach-are-left-out-and-reported", MapSize = 100, MaxSeconds = 150, Mods = "ludeon.rimworld.odyssey")]
        static void OutOfRangeIsReported(ScenarioContext s) {
            Bp.Begin(s);
            try {
                Prepare(s);
                var source = s.Center + ShipSource;
                BuildShip(s, Gravship(), source);
                var parts = Harvest(s.Map, source, 6, 5);
                var created = Bp.Create(s, Bp.DragRect(source, 6, 5), "Ship");

                var far = s.Center + new IntVec3(0, 0, 30);
                Bp.ClearMessages();
                Bp.Place(created, far);
                s.AreEqual(0, Bp.PlacedConstruction(s.Map).Count, "construction orders placed with no grav engine on the map");
                var none = Notices();
                s.Check(none.Count == 1 && none[0].Contains("Ship") && none[0].Contains(parts.Count + " parts") && none[0].Contains("could not be placed"), "with no grav engine every part is reported as left out, once; the messages are: " + (none.Count == 0 ? "none" : string.Join(" | ", none)));

                var engine = s.Center + ShipDest;
                SetUpEngineAt(s, engine);
                var stamp = engine + new IntVec3(14, 0, 0);
                var reach = parts.Where(p => p.IsTerrain && p.Def == "Substructure" && InFootprint(stamp + p.At, engine)).ToList();
                var refused = parts.Where(p => p.IsTerrain && p.Def == "Substructure" && !reach.Contains(p)).Select(p => p.At).ToHashSet();
                s.Check(refused.Count > 0 && reach.Count > refused.Count, "the stamp should sit half out of the engine's reach; " + refused.Count + " of " + parts.Count(p => p.Def == "Substructure") + " cells are out");
                var things = parts.Where(p => !p.IsTerrain).ToList();
                bool OnRefused(Part p) => GenAdj.OccupiedRect(p.At, p.Rot, ScenarioContext.Def(p.Def).Size).Cells.Any(refused.Contains);
                var cutOff = things.Count(OnRefused);
                Bp.ClearMessages();
                Bp.Place(created, stamp);
                CheckLayout(s, Bp.ExpectedAfter(reach, stamp, 0), "substructure placed only inside the engine's reach");
                var told = Notices();
                var waiting = things.Count - cutOff;
                s.Check(told.Any(m => m.Contains("Ship") && m.Contains(waiting + " parts") && m.Contains("not built yet")), waiting + " parts wait for the substructure; the messages are: " + (told.Count == 0 ? "none" : string.Join(" | ", told)));
                var left = refused.Count + cutOff;
                s.Check(told.Any(m => m.Contains("Ship") && m.Contains(left + " of " + parts.Count + " parts could not be placed")), left + " parts are out of reach; the messages are: " + (told.Count == 0 ? "none" : string.Join(" | ", told)));
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        [Scenario("blueprints-place-half-off-the-top-edge-of-the-map", MapSize = 100, MaxSeconds = 100)]
        static void StampAcrossTheMapEdge(ScenarioContext s) {
            Bp.Begin(s);
            try {
                var source = s.Center + Bp.Source;
                var parts = Bp.BuildRoom(s, Bp.Room(), source);
                var created = Bp.Create(s, Bp.DragRect(source, 3, 2), "EdgeRoom");
                foreach (var dest in new[] { new IntVec3(s.Center.x, 0, s.Map.Size.z - 1), new IntVec3(0, 0, s.Center.z) }) {
                    Bp.ClearMessages();
                    Bp.Place(created, dest);
                    var told = Bp.LiveMessages();
                    var expected = "Fluffy.Blueprints.CouldNotPlaceParts".Translate("EdgeRoom", parts.Count, parts.Count).Resolve();
                    s.Check(told.Contains(expected), "stamping at " + dest + ", half off the map, should report every part as left out ('" + expected + "'); the messages are: " + (told.Count == 0 ? "none" : string.Join(" | ", told)));
                    s.AreEqual(0, Bp.PlacedConstruction(s.Map).Count, "construction orders placed at " + dest + ", where every part is off the map or too near its edge");
                }
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        static Designator ScatteredFloorsBlueprint(ScenarioContext s, IntVec3 source, string name) {
            var floor = DefDatabase<TerrainDef>.GetNamed("WoodPlankFloor");
            s.Map.terrainGrid.SetFoundation(source, DefDatabase<TerrainDef>.GetNamed("Substructure"));
            foreach (var cell in CellRect.FromLimits(source.x - 39, source.z - 39, source.x + 39, source.z + 39))
                s.Map.terrainGrid.SetTerrain(cell, floor);
            return Bp.Create(s, Bp.DragRect(source, 39, 39), name);
        }

        static double DrawGhostMilliseconds(Designator blueprint, IntVec3 origin) {
            var model = AccessTools.Property(blueprint.GetType(), "Blueprint").GetValue(blueprint);
            var draw = AccessTools.Method(model.GetType(), "DrawGhost");
            draw.Invoke(model, new object[] { origin });
            var best = double.MaxValue;
            for (var i = 0; i < 3; i++) {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                draw.Invoke(model, new object[] { origin });
                best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
            }
            return best;
        }

        [Scenario("blueprints-odyssey-drawing-the-ghost-of-a-big-blueprint-stays-fast", MapSize = 100, MaxSeconds = 150, Mods = "ludeon.rimworld.odyssey")]
        static void GhostOfBigBlueprintIsCheap(ScenarioContext s) {
            Bp.Begin(s);
            try {
                Prepare(s);
                var center = s.Center;
                SetUpEngineAt(s, center);
                var created = ScatteredFloorsBlueprint(s, center, "Big");
                var ms = DrawGhostMilliseconds(created, center);
                s.Check(ms < 60, "drawing the ghost of 6241 floors and one foundation took " + ms.ToString("F0") + " ms; it should stay under 60 ms");
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        static void FinishResearchFor(TerrainDef terrain) {
            foreach (var project in terrain.researchPrerequisites ?? new List<ResearchProjectDef>())
                if (!project.IsFinished) Find.ResearchManager.FinishProject(project, doCompletionDialog: false);
        }

        [Scenario("blueprints-a-floor-laid-on-a-bridge-is-placed-on-land", MapSize = 100, MaxSeconds = 100)]
        static void FloorOnBridgeStampedOnLand(ScenarioContext s) {
            Bp.Begin(s);
            try {
                var bridge = DefDatabase<TerrainDef>.GetNamed("Bridge");
                s.Check(bridge.isFoundation, "Bridge should be a foundation in this game");
                FinishResearchFor(bridge);
                var source = s.Center + Bp.Source;
                var cells = new List<IntVec3>();
                for (var x = -1; x <= 1; x++) for (var z = -1; z <= 1; z++) cells.Add(new IntVec3(x, 0, z));
                foreach (var c in cells) {
                    s.Map.terrainGrid.SetTerrain(source + c, TerrainDefOf.WaterShallow);
                    s.Map.terrainGrid.SetFoundation(source + c, bridge);
                    s.Map.terrainGrid.SetTerrain(source + c, DefDatabase<TerrainDef>.GetNamed("WoodPlankFloor"));
                }
                var created = Bp.Create(s, Bp.DragRect(source, 1, 1), "BridgeDeck");
                var floors = cells.Select(c => Part.Floor("WoodPlankFloor", c)).ToList();
                var dest = s.Center + Bp.Dest;
                Bp.ClearMessages();
                Bp.Place(created, dest);
                Bp.CheckLayout(s, Bp.ExpectedAfter(floors, dest, 0), "the floors on the bridge, stamped on land");
                var told = Bp.LiveMessages();
                s.Check(!told.Any(m => m.Contains("not built yet")), "floors whose bridge cannot be placed should not be told to wait for it; the messages are: " + string.Join(" | ", told));
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        [Scenario("blueprints-odyssey-floors-do-not-wait-for-substructure-the-player-cannot-build-yet", MapSize = 100, MaxSeconds = 120, Mods = "ludeon.rimworld.odyssey")]
        static void FloorsDoNotWaitForUnavailableSubstructure(ScenarioContext s) {
            Bp.Begin(s);
            try {
                Prepare(s);
                var source = s.Center + ShipSource;
                var dest = s.Center + ShipDest;
                var deck = Substructure(2, -1, 1);
                deck.Add(Terrain("WoodPlankFloor", 0, 0));
                BuildShip(s, deck, source);
                SetUpEngineAt(s, dest);
                var created = Bp.Create(s, Bp.DragRect(source, 2, 1), "LockedDeck");
                Find.ResearchManager.gravEngineInspected = false;
                Bp.ClearMessages();
                Bp.Place(created, dest);
                Bp.CheckLayout(s, Bp.ExpectedAfter(new List<Part> { Terrain("WoodPlankFloor", 0, 0) }, dest, 0), "the floor, whose substructure the player cannot build yet");
                var told = Bp.LiveMessages();
                s.Check(!told.Any(m => m.Contains("not built yet")), "the floor should not be told to wait for substructure that cannot be built; the messages are: " + string.Join(" | ", told));
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        [Scenario("blueprints-odyssey-one-part-waiting-for-substructure-is-told-in-the-singular", MapSize = 100, MaxSeconds = 120, Mods = "ludeon.rimworld.odyssey")]
        static void OnePartWaitsIsTold(ScenarioContext s) {
            Bp.Begin(s);
            try {
                Prepare(s);
                var source = s.Center + ShipSource;
                var dest = s.Center + ShipDest;
                var deck = Substructure(1, 0, 0);
                deck.Add(Terrain("WoodPlankFloor", 0, 0));
                BuildShip(s, deck, source);
                SetUpEngineAt(s, dest);
                var created = Bp.Create(s, Bp.DragRect(source, 1, 0), "OnePart");
                Bp.ClearMessages();
                Bp.Place(created, dest);
                var told = Bp.LiveMessages();
                s.Check(told.Any(m => m.Contains("OnePart") && m.Contains("1 part ") && !m.Contains("1 parts") && m.Contains("not built yet")), "one waiting part should be told as '1 part'; the messages are: " + (told.Count == 0 ? "none" : string.Join(" | ", told)));
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        static readonly IntVec3 EngineA = new IntVec3(20, 0, 0);
        static readonly IntVec3 EngineB = new IntVec3(20, 0, 24);
        static readonly IntVec3 EngineC = new IntVec3(20, 0, -24);

        static string Folder => (string)AccessTools.Method(typeof(GenFilePaths), "FolderUnderSaveData").Invoke(null, new object[] { "Blueprints" });

        static string FileOf(string name) => Path.Combine(Folder, name + ".xml");

        static void StartStage(ScenarioContext s) {
            if (s.Stage == SaveStage.Setup || s.Stage == SaveStage.Normal) { Bp.Begin(s); return; }
            Current.Game.CurrentMap = s.Map;
            Event.current = new Event { type = EventType.Repaint };
            Bp.ClearWindows();
            Find.DesignatorManager.Deselect();
            Bp.ClearMessages();
        }

        static bool OnDeck(Part p) => p.At.z >= -3;

        static void StampShip(ScenarioContext s, Designator ship, List<Part> parts, IntVec3 engine, string when) {
            Bp.ClearConstruction(s.Map);
            Bp.ClearMessages();
            Bp.Place(ship, engine);
            var onDeck = parts.Where(p => OnDeck(p) && !(p.IsTerrain && p.Def == "Substructure")).ToList();
            CheckLayout(s, Bp.ExpectedAfter(parts.Except(onDeck).ToList(), engine, 0), when + ": first stamp");
            BuildOrders(s);
            Bp.Place(ship, engine);
            CheckLayout(s, Bp.ExpectedAfter(parts.Where(p => !(p.IsTerrain && p.Def == "Substructure")).ToList(), engine, 0), when + ": second stamp, once the substructure is built");
        }

        static string FixtureDir {
            get {
                var mod = LoadedModManager.RunningModsListForReading.First(m => string.Equals(m.PackageIdPlayerFacing, "rimhunter.blueprints.testrunner", StringComparison.OrdinalIgnoreCase));
                return Path.Combine(mod.RootDir, "Fixtures", "exports");
            }
        }

        static List<string> PartLines(string path) => XDocument.Load(path).Root.Element("Blueprint").Element("BuildableThings").Elements("li")
            .Select(li => string.Join("|", li.Elements().Where(e => !(e.Name.LocalName == "Rotation" && li.Element("TerrainDef") != null)).OrderBy(e => e.Name.LocalName, StringComparer.Ordinal).Select(e => e.Name.LocalName + "=" + e.Value)))
            .OrderBy(l => l, StringComparer.Ordinal).ToList();

        static void CheckFile(ScenarioContext s, List<Part> parts, string when) {
            s.Check(File.Exists(FileOf("Ship")), when + ": the export Ship.xml should exist");
            if (!File.Exists(FileOf("Ship"))) return;
            var written = PartLines(FileOf("Ship"));
            var recorded = PartLines(Path.Combine(FixtureDir, "Odyssey_Ship.xml"));
            s.Check(written.SequenceEqual(recorded), when + ": Ship.xml should hold the parts of the recorded export Odyssey_Ship.xml; " + written.Except(recorded).Count() + " lines differ, for example " + string.Join(" ;; ", written.Except(recorded).Take(2)));
            var list = XDocument.Load(FileOf("Ship")).Root.Element("Blueprint").Element("BuildableThings").Elements("li").ToList();
            string Def(XElement li) => li.Element("ThingDef")?.Value ?? li.Element("TerrainDef")?.Value;
            foreach (var group in parts.GroupBy(p => p.Def))
                s.AreEqual(group.Count(), list.Count(li => Def(li) == group.Key), when + ": " + group.Key + " parts in Ship.xml");
            s.AreEqual(parts.Count, list.Count, when + ": parts in Ship.xml");
            foreach (var p in parts.Where(p => !p.IsTerrain && ScenarioContext.Def(p.Def).rotatable)) {
                var rotations = list.Where(li => Def(li) == p.Def).Select(li => li.Element("Rotation")?.Value ?? "0").ToList();
                s.Check(rotations.Count == 1 && (rotations[0] == p.Rot.AsInt.ToString() || rotations[0] == p.Rot.ToStringHuman()), when + ": " + p.Def + " should be stored facing " + p.Rot + "; the file says " + string.Join(",", rotations));
            }
            var stored = list.Where(li => Def(li) == "Substructure").Select(li => li.Element("Position") == null ? IntVec3.Zero : IntVec3.FromString(li.Element("Position").Value)).ToList();
            var wanted = parts.Where(p => p.Def == "Substructure").Select(p => p.At).ToList();
            var dx = stored.Min(c => c.x) - wanted.Min(c => c.x);
            var dz = stored.Min(c => c.z) - wanted.Min(c => c.z);
            s.Check(stored.Select(c => new IntVec3(c.x - dx, 0, c.z - dz)).ToHashSet().SetEquals(wanted), when + ": the substructure cells in Ship.xml should be the cells of the ship");
            var deckFloors = parts.Where(p => p.IsTerrain && p.Def == "WoodPlankFloor" && wanted.Contains(p.At)).Select(p => p.At).ToHashSet();
            var storedFloors = list.Where(li => Def(li) == "WoodPlankFloor").Select(li => li.Element("Position") == null ? IntVec3.Zero : IntVec3.FromString(li.Element("Position").Value)).Select(c => new IntVec3(c.x - dx, 0, c.z - dz)).ToHashSet();
            s.Check(deckFloors.Count == 2 && deckFloors.IsSubsetOf(storedFloors), when + ": Ship.xml should hold both layers of the two deck cells, the substructure and the floor on it; the floors on the deck are " + string.Join(" ", deckFloors) + " and the file holds floors at " + string.Join(" ", storedFloors));
        }

        [Scenario("blueprints-odyssey-a-gravship-blueprint-survives-save-reload-and-export", MapSize = 100, MaxSeconds = 170, Mods = "ludeon.rimworld.odyssey", SaveWithMods = "{mod} ludeon.rimworld.odyssey", SetupInSaveLaunch = true, ReloadInSaveLaunch = true)]
        static void GravshipSurvivesSaveAndExport(ScenarioContext s) {
            StartStage(s);
            try {
                var source = s.Center + ShipSource;
                var export = "Fluffy.Blueprints.SaveToXML".Translate().Resolve();
                var removeAndDelete = "Fluffy.Blueprints.RemoveAndDeleteXML".Translate().Resolve();
                var remove = "Fluffy.Blueprints.Remove".Translate().Resolve();
                if (s.Stage == SaveStage.Setup) {
                    Prepare(s);
                    BuildShip(s, ShipWithVanillaParts(), source);
                    foreach (var engine in new[] { EngineA, EngineB, EngineC }) SetUpEngineAt(s, s.Center + engine);
                    var created = Bp.Create(s, Bp.DragRect(source, 6, 5), "Ship");
                    Bp.Choose(Bp.Option(Bp.OpenRightClickMenu(created), export));
                    CheckFile(s, Harvest(s.Map, source, 6, 5), "after exporting");
                    return;
                }
                var stage = "after the reload (" + s.Stage + ")";
                s.Check(ModsConfig.OdysseyActive, stage + ": Odyssey should be active");
                s.Check(Find.ResearchManager.gravEngineInspected, stage + ": the grav engine should still count as inspected in the saved game");
                var parts = Harvest(s.Map, source, 6, 5);
                s.Check(parts.Count(p => p.Def == "Substructure") == 88 && parts.Count(p => p.Def == "GravshipHull") == 11, stage + ": the saved map should still hold the ship; it holds " + parts.Count + " parts");
                var ship = Bp.SavedNamed("Ship");
                s.Check(ship != null, stage + ": the menu should list Ship; it lists: " + string.Join(", ", Bp.SavedNames));
                if (ship == null) return;
                var labels = Bp.OpenRightClickMenu(ship).Select(o => o.Label).ToList();
                Bp.ClearWindows();
                s.Check(labels.Contains(removeAndDelete), stage + ": an exported Ship should offer '" + removeAndDelete + "'; the menu offers: " + string.Join(" | ", labels));
                CheckFile(s, parts, stage + ", export");
                var dest = s.Center + (s.Stage == SaveStage.ReloadedInSameLaunch ? EngineA : EngineB);
                StampShip(s, ship, parts, dest, stage + ", from the saved blueprint");
                if (s.Stage == SaveStage.ReloadedInSameLaunch) {
                    Bp.Choose(Bp.Option(Bp.OpenRightClickMenu(ship), remove));
                    s.Check(Bp.SavedNamed("Ship") == null, "removing Ship should take it out of the menu");
                    var importLabel = "Fluffy.Blueprints.LoadFromXML".Translate("Ship").Resolve();
                    Bp.Choose(Bp.Option(Bp.CreateTool.RightClickFloatMenuOptions.ToList(), importLabel));
                    var imported = Bp.SavedNamed("Ship");
                    s.Check(imported != null, stage + ": the import menu should bring Ship back; the menu lists: " + string.Join(", ", Bp.SavedNames));
                    if (imported != null) StampShip(s, imported, parts, s.Center + EngineC, stage + ", from the imported file");
                } else {
                    Bp.Choose(Bp.Option(Bp.OpenRightClickMenu(ship), removeAndDelete));
                    s.Check(!File.Exists(FileOf("Ship")), "removing and deleting Ship should delete its file");
                }
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        static readonly string[] OdysseyNames = { "Substructure", "GravshipHull", "PilotConsole", "ChemfuelTank", "SmallThruster" };

        static List<Part> VanillaPartsOfShip() => new List<Part> {
            Part.Thing("TorchLamp", -5, -5),
            Part.Floor("WoodPlankFloor", -4, -5),
            Part.Floor("WoodPlankFloor", 0, 0),
            Part.Floor("WoodPlankFloor", 1, 0),
        };

        static List<string> AllNotices() =>
            Find.Archive.ArchivablesListForReading.OfType<Message>().Select(m => m.text).Concat(Bp.LiveMessages()).ToList();

        static void CheckMissingMessage(ScenarioContext s, string when) {
            var notices = AllNotices();
            var told = notices.Where(n => n.Contains("Odyssey_Ship") && OdysseyNames.All(n.Contains)).ToList();
            s.Check(told.Count > 0, when + ": the player should be told that " + string.Join(", ", OdysseyNames) + " were left out of Odyssey_Ship; the messages are: " + (notices.Count == 0 ? "none" : string.Join(" | ", notices)));
        }

        static void CheckKeepsOdysseyParts(ScenarioContext s, string name, string when) {
            var written = PartLines(FileOf(name));
            var recorded = PartLines(Path.Combine(FixtureDir, "Odyssey_Ship.xml"));
            s.Check(written.SequenceEqual(recorded), when + ": " + name + ".xml should still hold every part of the recorded export, Odyssey parts included; " + recorded.Except(written).Count() + " are missing: " + string.Join(" ;; ", recorded.Except(written).Take(3)) + " and " + written.Except(recorded).Count() + " are new: " + string.Join(" ;; ", written.Except(recorded).Take(3)));
        }

        [Scenario("blueprints-odyssey-without-odyssey-an-imported-gravship-loads-the-rest-and-says-what-is-missing", MapSize = 100, MaxSeconds = 150)]
        static void ImportedGravshipWithoutOdyssey(ScenarioContext s) {
            StartStage(s);
            try {
                s.Check(!ModsConfig.OdysseyActive, "this launch should not have Odyssey");
                File.Copy(Path.Combine(FixtureDir, "Odyssey_Ship.xml"), FileOf("Odyssey_Ship"), true);
                Bp.ClearMessages();
                Bp.Choose(Bp.Option(Bp.CreateTool.RightClickFloatMenuOptions.ToList(), "Fluffy.Blueprints.LoadFromXML".Translate("Odyssey_Ship").Resolve()));
                var imported = Bp.SavedNamed("Odyssey_Ship");
                s.Check(imported != null, "the blueprint should be imported although Odyssey is not loaded; the menu lists: " + string.Join(", ", Bp.SavedNames));
                if (imported == null) return;
                CheckMissingMessage(s, "importing the file");
                Bp.Choose(Bp.Option(Bp.OpenRightClickMenu(imported), "Fluffy.Blueprints.Rename".Translate().Resolve()));
                s.Check(Bp.TypeNameAndAccept("Odyssey_Copy"), "the rename dialog should accept a new name");
                var dest = s.Center + ShipDest;
                Bp.Place(imported, dest);
                Bp.CheckLayout(s, Bp.ExpectedAfter(VanillaPartsOfShip(), dest, 0), "the parts this game has, placed");
                Bp.DrawArchitectTab();
                CheckKeepsOdysseyParts(s, "Odyssey_Copy", "after a rename");
                File.Delete(FileOf("Odyssey_Copy"));
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        const string SavedWithOdysseyParts = "rimhunter-blueprints-odyssey-without-odyssey-a-saved-gravship-loads-the-rest-and-says-what-is-missing";

        static void PutOdysseyShipInSavedGame(string fileName) {
            if (fileName != SavedWithOdysseyParts) return;
            var path = GenFilePaths.FilePathForSavedGame(fileName);
            var doc = XDocument.Load(path);
            var component = doc.Descendants("li").FirstOrDefault(e => (string)e.Attribute("Class") == "Blueprints.BlueprintController")
                ?? throw new ScenarioFailure("the saved game has no world component of the class Blueprints.BlueprintController");
            var recorded = XDocument.Load(Path.Combine(FixtureDir, "Odyssey_Ship.xml")).Root.Element("Blueprint");
            var entry = new XElement("li", recorded.Elements());
            entry.Element("Name")?.Remove();
            entry.Add(new XElement("Name", "Odyssey_Ship"));
            component.Element("Blueprints")?.Remove();
            component.Add(new XElement("Blueprints", entry));
            doc.Save(path);
        }

        [Scenario("blueprints-odyssey-without-odyssey-a-saved-gravship-loads-the-rest-and-says-what-is-missing", MapSize = 100, MaxSeconds = 150, SaveWithMods = "{mod}", SetupInSaveLaunch = true)]
        static void SavedGravshipWithoutOdyssey(ScenarioContext s) {
            StartStage(s);
            try {
                if (s.Stage == SaveStage.Setup) {
                    Bp.BuildRoom(s, new List<Part> { Part.Thing("TorchLamp", IntVec3.Zero) }, s.Center + ShipSource, 1);
                    Bp.Create(s, Bp.DragRect(s.Center + ShipSource, 1, 1), "Seed");
                    new Harmony("rimhunter.blueprints.testrunner.odysseysave").Patch(AccessTools.Method(typeof(GameDataSaveLoader), nameof(GameDataSaveLoader.SaveGame)),
                        postfix: new HarmonyMethod(typeof(BlueprintsOdyssey), nameof(PutOdysseyShipInSavedGame)));
                    return;
                }
                s.Check(!ModsConfig.OdysseyActive, "the launch that loads the save should not have Odyssey");
                s.Check(Bp.SavedNames.SequenceEqual(new[] { "Odyssey_Ship" }), "the loaded save should list Odyssey_Ship; it lists: " + string.Join(", ", Bp.SavedNames));
                CheckMissingMessage(s, "loading the save");
                var ship = Bp.SavedNamed("Odyssey_Ship");
                if (ship == null) return;
                var dest = s.Center + ShipDest;
                Bp.Place(ship, dest);
                Bp.CheckLayout(s, Bp.ExpectedAfter(VanillaPartsOfShip(), dest, 0), "the parts this game has, placed from the loaded save");
                Bp.DrawArchitectTab();
                Bp.Choose(Bp.Option(Bp.OpenRightClickMenu(ship), "Fluffy.Blueprints.SaveToXML".Translate().Resolve()));
                CheckKeepsOdysseyParts(s, "Odyssey_Ship", "after exporting the loaded blueprint");
                File.Delete(FileOf("Odyssey_Ship"));
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }
    }
}
