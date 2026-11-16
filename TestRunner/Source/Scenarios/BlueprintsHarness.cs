using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    sealed class Part
    {
        public string Def;
        public string Stuff;
        public IntVec3 At;
        public Rot4 Rot;
        public bool IsTerrain;

        public static Part Thing(string def, IntVec3 at, string stuff = null, Rot4 rot = default) =>
            new Part { Def = def, Stuff = stuff, At = at, Rot = rot };

        public static Part Thing(string def, int x, int z, string stuff = null, Rot4 rot = default) =>
            Thing(def, new IntVec3(x, 0, z), stuff, rot);

        public static Part Floor(string def, IntVec3 at) =>
            new Part { Def = def, At = at, IsTerrain = true };

        public static Part Floor(string def, int x, int z) =>
            Floor(def, new IntVec3(x, 0, z));
    }

    static class Bp
    {
        public const string CategoryName = "Blueprints";

        public static DesignationCategoryDef Category => DefDatabase<DesignationCategoryDef>.GetNamed(CategoryName);

        public static IEnumerable<Designator> Tab => Category.ResolvedAllowedDesignators;

        public static Designator CreateTool => Tab.First(d => d.GetType().Name == "Designator_CreateBlueprint");

        public static List<Designator> Saved => Tab.Where(d => d.GetType().Name == "Designator_Blueprint").ToList();

        public static List<string> SavedNames => Saved.Select(d => d.Label).ToList();

        public static Designator SavedNamed(string name) => Saved.FirstOrDefault(d => d.Label == name);

        static void RequireMod(string packageId, string name, string workshopId) {
            if (!LoadedModManager.RunningModsListForReading.Any(m => string.Equals(m.PackageIdPlayerFacing, packageId, StringComparison.OrdinalIgnoreCase)))
                throw new ScenarioFailure(name + " (Workshop " + workshopId + ") is not loaded; run with --mod " + workshopId);
        }

        public static void RequireMint() => RequireMod("Dubwise.DubsMintMenus", "Dubs Mint Menus", "1446523594");

        public static void RequireBadHygiene() => RequireMod("Dubwise.DubsBadHygiene", "Dubs Bad Hygiene", "836308268");

        public static void ClearWindows() {
            foreach (var w in Find.WindowStack.Windows.ToList())
                Find.WindowStack.TryRemove(w, doCloseSound: false);
        }

        public static void Begin(ScenarioContext s) {
            Current.Game.CurrentMap = s.Map;
            Event.current = new Event { type = EventType.Repaint };
            ClearWindows();
            Find.DesignatorManager.Deselect();
            var remove = "Fluffy.Blueprints.Remove".Translate().Resolve();
            foreach (var leftover in Saved)
                leftover.RightClickFloatMenuOptions.First(o => o.Label == remove).action();
            ClearMessages();
        }

        public static void End() {
            ClearWindows();
            Find.DesignatorManager.Deselect();
        }

        public static Window NameDialog => Find.WindowStack.Windows.FirstOrDefault(w => w.GetType().Name == "Dialog_NameBlueprint");

        public static bool TypeNameAndAccept(string name) {
            var dialog = NameDialog ?? throw new ScenarioFailure("no blueprint naming dialog is open");
            AccessTools.Field(dialog.GetType().BaseType, "curName").SetValue(dialog, name);
            Event.current = new Event { type = EventType.KeyDown, keyCode = KeyCode.Return };
            dialog.DoWindowContents(new Rect(0f, 0f, 280f, 175f));
            return !Find.WindowStack.Windows.Contains(dialog);
        }

        public static Designator Create(ScenarioContext s, IEnumerable<IntVec3> cells, string name) {
            Current.Game.CurrentMap = s.Map;
            Event.current = new Event { type = EventType.MouseUp };
            CreateTool.DesignateMultiCell(cells.ToList());
            var created = Find.DesignatorManager.SelectedDesignator;
            if (created == null || created.GetType().Name != "Designator_Blueprint")
                throw new ScenarioFailure("dragging over the layout did not select a new blueprint");
            if (name != null && !TypeNameAndAccept(name))
                throw new ScenarioFailure("the naming dialog did not accept " + name);
            return created;
        }

        public static void Place(Designator blueprint, IntVec3 cell, bool shift = false, bool select = true) {
            if (select || Find.DesignatorManager.SelectedDesignator != blueprint) Find.DesignatorManager.Select(blueprint);
            Event.current = new Event { type = EventType.MouseUp, modifiers = shift ? EventModifiers.Shift : EventModifiers.None };
            var accepted = blueprint.CanDesignateCell(cell);
            if (!accepted.Accepted) throw new ScenarioFailure("the blueprint tool refused cell " + cell);
            blueprint.DesignateSingleCell(cell);
        }

        public static void PressKey(Designator blueprint, KeyBindingDef key) {
            Event.current = new Event { type = EventType.KeyDown, keyCode = key.MainKey };
            blueprint.DoExtraGuiControls(0f, 300f);
            ClearWindows();
        }

        public static void RotateRight(Designator blueprint) => PressKey(blueprint, KeyBindingDefOf.Designator_RotateRight);

        public static void RotateLeft(Designator blueprint) => PressKey(blueprint, KeyBindingDefOf.Designator_RotateLeft);

        public static void Flip(Designator blueprint) => PressKey(blueprint, KeyBindingDef.Named("Blueprint_Flip"));

        public static List<string> LiveMessages() {
            var field = AccessTools.Field(typeof(Messages), "liveMessages");
            var list = (System.Collections.IList)field.GetValue(null);
            return list.Cast<object>().Select(m => (string)AccessTools.Field(m.GetType(), "text").GetValue(m)).ToList();
        }

        public static void ClearMessages() {
            var field = AccessTools.Field(typeof(Messages), "liveMessages");
            ((System.Collections.IList)field.GetValue(null)).Clear();
        }

        public static IntVec3 RotateOffset(IntVec3 offset, int clockwiseSteps) {
            var steps = ((clockwiseSteps % 4) + 4) % 4;
            var o = offset;
            for (var i = 0; i < steps; i++) o = new IntVec3(o.z, 0, -o.x);
            return o;
        }

        static bool DoorRotationFollowsNeighbors(string defName) {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            return def?.blueprintDef != null && typeof(Blueprint_Door).IsAssignableFrom(def.blueprintDef.thingClass) && def.Size == IntVec2.One;
        }

        public static string Describe(string def, string stuff, Rot4 rot, IEnumerable<IntVec3> cells) =>
            def + "|" + (stuff ?? "-") + "|" + (DoorRotationFollowsNeighbors(def) ? 0 : rot.AsInt) + "|" + string.Join(" ", cells.OrderBy(c => c.x).ThenBy(c => c.z).Select(c => c.x + "," + c.z));

        public static Building Spawn(ScenarioContext s, Part p, IntVec3 center) {
            var cell = center + p.At;
            if (p.IsTerrain) {
                s.Map.terrainGrid.SetTerrain(cell, DefDatabase<TerrainDef>.GetNamed(p.Def));
                return null;
            }
            return s.SpawnBuilding(p.Def, cell, p.Stuff, p.Rot);
        }

        public static HashSet<IntVec3> FloorCovered(IEnumerable<Part> parts, IntVec3 dest, int clockwiseSteps) =>
            parts.Where(p => !p.IsTerrain && !ScenarioContext.Def(p.Def).CoexistsWithFloors)
                 .SelectMany(p => GenAdj.OccupiedRect(p.At, p.Rot, ScenarioContext.Def(p.Def).Size).Cells)
                 .Select(c => dest + RotateOffset(c, clockwiseSteps))
                 .ToHashSet();

        public static int FloorsLeftOut(IEnumerable<Part> parts) {
            var covered = FloorCovered(parts, IntVec3.Zero, 0);
            return parts.Count(p => p.IsTerrain && covered.Contains(p.At));
        }

        public static List<string> ExpectedAfter(IEnumerable<Part> parts, IntVec3 dest, int clockwiseSteps, Func<Part, string> stuffOf = null) {
            var result = new List<string>();
            var covered = FloorCovered(parts, dest, clockwiseSteps);
            foreach (var p in parts) {
                var at = dest + RotateOffset(p.At, clockwiseSteps);
                if (p.IsTerrain && covered.Contains(at)) continue;
                if (p.IsTerrain) {
                    result.Add(Describe(p.Def, null, Rot4.North, new[] { at }));
                    continue;
                }
                var def = ScenarioContext.Def(p.Def);
                var rot = def.rotatable ? new Rot4((p.Rot.AsInt + clockwiseSteps + 400) % 4) : p.Rot;
                var occupiedBefore = GenAdj.OccupiedRect(p.At, p.Rot, def.Size).Cells;
                var cells = occupiedBefore.Select(c => dest + RotateOffset(c, clockwiseSteps));
                result.Add(Describe(p.Def, stuffOf == null ? p.Stuff : stuffOf(p), rot, cells));
            }
            return result.OrderBy(x => x, StringComparer.Ordinal).ToList();
        }

        public static List<string> PlacedConstruction(Map map) {
            var result = new List<string>();
            foreach (var thing in map.listerThings.AllThings.ToList()) {
                if (thing is Blueprint_Build b) {
                    var entity = b.def.entityDefToBuild;
                    var isTerrain = entity is TerrainDef;
                    result.Add(Describe(entity.defName, isTerrain ? null : b.stuffToUse?.defName, isTerrain ? Rot4.North : b.Rotation, b.OccupiedRect().Cells));
                }
            }
            return result.OrderBy(x => x, StringComparer.Ordinal).ToList();
        }

        public static List<string> Diff(List<string> expected, List<string> actual) {
            var missing = expected.Except(actual).Select(x => "missing " + x);
            var extra = actual.Except(expected).Select(x => "unexpected " + x);
            return missing.Concat(extra).ToList();
        }

        public static void CheckLayout(ScenarioContext s, List<string> expected, string when) {
            var actual = PlacedConstruction(s.Map);
            var diff = Diff(expected, actual);
            s.Check(diff.Count == 0, when + ": the placed construction orders differ from the saved layout (" + diff.Count + "): " + string.Join(" ;; ", diff.Take(12)));
            s.AreEqual(expected.Count, actual.Count, when + ": number of construction orders");
        }

        public static void ClearConstruction(Map map) {
            foreach (var t in map.listerThings.AllThings.ToList())
                if (t is Blueprint_Build || t is Frame) t.Destroy();
        }

        public static readonly IntVec3 Source = new IntVec3(-18, 0, 0);
        public static readonly IntVec3 Dest = new IntVec3(18, 0, 0);

        public static List<Part> Room() {
            var parts = new List<Part>();
            for (var x = -3; x <= 3; x++)
            for (var z = -2; z <= 2; z++) {
                parts.Add(Part.Floor("WoodPlankFloor", x, z));
                var edge = x == -3 || x == 3 || z == -2 || z == 2;
                if (!edge) continue;
                if (x == 0 && z == -2)
                    parts.Add(Part.Thing("Door", x, z, "WoodLog"));
                else
                    parts.Add(Part.Thing("Wall", x, z, "BlocksGranite"));
            }
            parts.Add(Part.Thing("Bed", 1, 0, "WoodLog", Rot4.East));
            parts.Add(Part.Thing("TorchLamp", -2, 0));
            parts.Add(Part.Thing("PlantPot", -2, -1, "WoodLog"));
            parts.Add(Part.Thing("TorchWallLamp", -1, 1));
            parts.Add(Part.Thing("TorchWallLamp", 1, -1, null, Rot4.South));
            return parts;
        }

        public static Part Table() => Part.Thing("Table2x2c", 0, 3, "WoodLog");

        public static List<Part> BuildRoom(ScenarioContext s, List<Part> parts, IntVec3 center, int halfZ = 2) {
            foreach (var p in parts.Where(p => p.IsTerrain)) Spawn(s, p, center);
            foreach (var p in parts.Where(p => !p.IsTerrain)) Spawn(s, p, center);
            return Harvest(s.Map, center, 3, halfZ);
        }

        public static List<Part> Harvest(Map map, IntVec3 center, int halfX, int halfZ) {
            var result = new List<Part>();
            var rect = CellRect.FromLimits(center.x - halfX, center.z - halfZ, center.x + halfX, center.z + halfZ);
            foreach (var cell in rect) {
                var terrain = map.terrainGrid.TerrainAt(cell);
                if (terrain.designationCategory != null)
                    result.Add(Part.Floor(terrain.defName, cell - center));
            }
            foreach (var thing in rect.SelectMany(c => c.GetThingList(map)).OfType<Building>().Where(b => b.Faction == Faction.OfPlayer && b.def.designationCategory != null).Distinct())
                result.Add(Part.Thing(thing.def.defName, thing.Position - center, thing.Stuff?.defName, thing.Rotation));
            return result;
        }

        public static IEnumerable<IntVec3> DragRect(IntVec3 center, int halfX, int halfZ) =>
            CellRect.FromLimits(center.x - halfX, center.z - halfZ, center.x + halfX, center.z + halfZ).Cells;

        public static void DrawArchitectTab() {
            Event.current = new Event { type = EventType.Repaint };
            var tab = new ArchitectCategoryTab(Category, new QuickSearchFilter());
            tab.DesignationTabOnGUI(null);
            DrawWindows();
        }

        public static void DrawWindows() {
            Event.current = new Event { type = EventType.Repaint };
            Find.WindowStack.WindowStackOnGUI();
            foreach (var w in Find.WindowStack.Windows.ToList()) {
                try { w.DoWindowContents(w.windowRect.AtZero()); }
                catch (Exception ex) { Log.Error("Exception filling window for " + w.GetType() + ": " + ex); }
            }
        }

        public static List<FloatMenuOption> OpenRightClickMenu(Designator d) {
            var options = d.RightClickFloatMenuOptions.ToList();
            Find.WindowStack.Add(new FloatMenu(options));
            DrawWindows();
            return options;
        }

        public static FloatMenuOption Option(List<FloatMenuOption> options, string label) {
            var found = options.FirstOrDefault(o => o.Label == label);
            if (found == null) throw new ScenarioFailure("no menu option '" + label + "' among: " + string.Join(" | ", options.Select(o => o.Label)));
            return found;
        }

        public static void Choose(FloatMenuOption option) {
            ClearWindows();
            option.action();
        }

#if RW_1_6
        // Unity's GUI.Button needs the same control id on the press and the release, and a synthesized press and release inside one OnGUI never share one,
        // so on 1.6 the click is replaced by the call the button makes: Blueprint.DrawStuffMenu for each stuff-based line of the info panel.
        static void ChooseStuffThroughTheMenuCall(Designator blueprint, IEnumerable<Part> parts, string stuffLabelStart) {
            Find.DesignatorManager.Select(blueprint);
            var model = AccessTools.Property(blueprint.GetType(), "Blueprint").GetValue(blueprint);
            var groups = (System.Collections.IDictionary)AccessTools.Property(model.GetType(), "GroupedBuildables").GetValue(model);
            var draw = AccessTools.Method(model.GetType(), "DrawStuffMenu");
            var hits = 0;
            foreach (BuildableDef buildable in groups.Keys.Cast<BuildableDef>().ToList()) {
                var first = ((System.Collections.IEnumerable)groups[buildable]).Cast<object>().First();
                if (AccessTools.Property(first.GetType(), "Stuff").GetValue(first) == null) continue;
                ClearWindows();
                draw.Invoke(model, new object[] { buildable });
                var menu = Find.WindowStack.WindowOfType<FloatMenu>() ?? throw new ScenarioFailure("choosing the stuff of " + buildable.defName + " opened no menu");
                var options = (List<FloatMenuOption>)AccessTools.Field(typeof(FloatMenu), "options").GetValue(menu);
                var option = options.FirstOrDefault(o => o.Label.StartsWith(stuffLabelStart)) ?? throw new ScenarioFailure("no '" + stuffLabelStart + "' option in the stuff menu of " + buildable.defName + ": " + string.Join(" | ", options.Select(o => o.Label)));
                option.action();
                ClearWindows();
                hits++;
            }
            var stuffBased = parts.Where(p => !p.IsTerrain).Select(p => ScenarioContext.Def(p.Def)).Where(d => d.MadeFromStuff && d.costStuffCount > 0).Distinct().Count();
            if (hits < stuffBased) throw new ScenarioFailure("only " + hits + " lines of the info panel offered a stuff menu but the layout holds " + stuffBased + " stuff-based buildings");
        }
#endif

        public static void ChooseStuffForEveryBuildable(Designator blueprint, IEnumerable<Part> parts, string stuffLabelStart) {
#if RW_1_6
            ChooseStuffThroughTheMenuCall(blueprint, parts, stuffLabelStart);
#else
            Find.DesignatorManager.Select(blueprint);
            var width = ArchitectCategoryTab.InfoRect.width;
            var hits = 0;
            for (var y = 1f; y < 700f; y += 2f) {
                ClearWindows();
                var at = new Vector2(width * 0.5f, y);
                var curY = 0f;
                Event.current = new Event { type = EventType.MouseDown, button = 0, mousePosition = at };
                blueprint.DrawPanelReadout(ref curY, width);
                curY = 0f;
                Event.current = new Event { type = EventType.MouseUp, button = 0, mousePosition = at };
                blueprint.DrawPanelReadout(ref curY, width);
                var menu = Find.WindowStack.WindowOfType<FloatMenu>();
                if (menu == null) continue;
                var options = (List<FloatMenuOption>)AccessTools.Field(typeof(FloatMenu), "options").GetValue(menu);
                var option = options.FirstOrDefault(o => o.Label.StartsWith(stuffLabelStart));
                if (option == null) throw new ScenarioFailure("no '" + stuffLabelStart + "' option in the stuff menu opened at y=" + y + ": " + string.Join(" | ", options.Select(o => o.Label)));
                option.action();
                ClearWindows();
                hits++;
            }
            var stuffBased = parts.Where(p => !p.IsTerrain).Select(p => ScenarioContext.Def(p.Def)).Where(d => d.MadeFromStuff && d.costStuffCount > 0).Distinct().Count();
            if (hits < stuffBased) throw new ScenarioFailure("only " + hits + " places in the info panel opened a stuff menu but the layout holds " + stuffBased + " stuff-based buildings");
#endif
        }
    }
}