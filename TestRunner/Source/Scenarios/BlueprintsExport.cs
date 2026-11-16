using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using HarmonyLib;
using UnityEngine;
using RimWorld;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class BlueprintsExport
    {
        static readonly IntVec3 Source2 = new IntVec3(-18, 0, 14);
        static readonly IntVec3 Source3 = new IntVec3(-18, 0, -14);
        static readonly IntVec3 Dest2 = new IntVec3(18, 0, 14);
        static readonly IntVec3 Dest3 = new IntVec3(18, 0, -14);

        internal static string Folder => (string)AccessTools.Method(typeof(GenFilePaths), "FolderUnderSaveData").Invoke(null, new object[] { "Blueprints" });

        internal static string FileOf(string name) => Path.Combine(Folder, name + ".xml");

        static string FixtureDir {
            get {
                var mod = LoadedModManager.RunningModsListForReading.First(m => string.Equals(m.PackageIdPlayerFacing, "rimhunter.blueprints.testrunner", StringComparison.OrdinalIgnoreCase));
                return Path.Combine(mod.RootDir, "Fixtures", "exports");
            }
        }

        internal static void Start(ScenarioContext s, bool begin) {
            if (begin) { Bp.Begin(s); return; }
            Current.Game.CurrentMap = s.Map;
            Event.current = new Event { type = EventType.Repaint };
            Bp.ClearWindows();
            Find.DesignatorManager.Deselect();
        }

        static string ExportLabel => "Fluffy.Blueprints.SaveToXML".Translate().Resolve();
        static string RemoveLabel => "Fluffy.Blueprints.Remove".Translate().Resolve();
        static string RemoveAndDeleteLabel => "Fluffy.Blueprints.RemoveAndDeleteXML".Translate().Resolve();
        static string ImportLabel(string name) => "Fluffy.Blueprints.LoadFromXML".Translate(name).Resolve();

        static List<string> ImportLabels() {
            Bp.ClearMessages();
            var labels = Bp.CreateTool.RightClickFloatMenuOptions.Select(o => o.Label).ToList();
            Bp.ClearWindows();
            return labels;
        }

        static void Import(string name) {
            var options = Bp.CreateTool.RightClickFloatMenuOptions.ToList();
            Bp.Choose(Bp.Option(options, ImportLabel(name)));
        }

        static List<string> MenuLabels(Designator blueprint) {
            var labels = Bp.OpenRightClickMenu(blueprint).Select(o => o.Label).ToList();
            Bp.ClearWindows();
            return labels;
        }

        static List<string> Notices() =>
            Find.Archive.ArchivablesListForReading.OfType<Message>().Select(m => m.text).Concat(Bp.LiveMessages()).ToList();

        static List<string> Merge(params List<string>[] lists) => lists.SelectMany(l => l).OrderBy(x => x, StringComparer.Ordinal).ToList();

        static List<Part> SmallLayout() => new List<Part> {
            Part.Thing("Table2x2c", 0, 0, "WoodLog"),
            Part.Thing("TorchLamp", -2, 0),
            Part.Thing("PlantPot", 2, 1, "WoodLog"),
            Part.Floor("WoodPlankFloor", -2, 2),
        };

        static string Attr(XElement e, string child) => e.Element(child)?.Value;

        [Scenario("blueprints-export-writes-fluffys-file-format", MapSize = 80)]
        static void ExportWritesFluffysFormat(ScenarioContext s) {
            Start(s, true);
            try {
                var source = s.Center + Bp.Source;
                Bp.BuildRoom(s, SmallLayout(), source, 3);
                var created = Bp.Create(s, Bp.DragRect(source, 3, 3), "Format_Check");
                Bp.Choose(Bp.Option(Bp.OpenRightClickMenu(created), ExportLabel));

                var file = FileOf("Format_Check");
                s.Check(File.Exists(file), "exporting Format_Check should write " + file);
                s.AreEqual("Blueprints", Path.GetFileName(Path.GetDirectoryName(file)), "name of the export folder");
                s.AreEqual(Path.GetFullPath(GenFilePaths.SaveDataFolderPath), Path.GetFullPath(Path.GetDirectoryName(Path.GetDirectoryName(file))), "the export folder's parent should be the save-data folder");
                if (!File.Exists(file)) return;

                var doc = XDocument.Load(file);
                var root = doc.Root;
                s.AreEqual("Blueprint", root.Name.LocalName, "root node of an export");
                s.AreEqual("meta,Blueprint", string.Join(",", root.Elements().Select(e => e.Name.LocalName)), "children of the root node");
                var meta = root.Element("meta");
                foreach (var node in new[] { "gameVersion", "modIds", "modNames", "modSteamIds" })
                    s.Check(meta.Element(node) != null, "the meta header should hold " + node);
                var inner = root.Element("Blueprint");
                s.AreEqual("BuildableThings,Name,Size", string.Join(",", inner.Elements().Select(e => e.Name.LocalName)), "children of the inner Blueprint node of a first export");
                s.AreEqual("Format_Check", Attr(inner, "Name"), "Name node");
                s.Check(System.Text.RegularExpressions.Regex.IsMatch(Attr(inner, "Size") ?? "", @"^\(\d+, \d+\)$"), "Size node should look like (5, 3), it is " + Attr(inner, "Size"));

                var items = inner.Element("BuildableThings").Elements("li").ToList();
                var described = items.Select(li => {
                    var kind = li.Element("TerrainDef") != null ? "TerrainDef" : "ThingDef";
                    return kind + ":" + Attr(li, kind) + ":" + (Attr(li, "Stuff") ?? "-") + ":" + (Attr(li, "Rotation") ?? "-");
                }).OrderBy(x => x, StringComparer.Ordinal).ToList();
                var expected = new[] {
                    "TerrainDef:WoodPlankFloor:-:200",
                    "ThingDef:PlantPot:WoodLog:-",
                    "ThingDef:Table2x2c:WoodLog:-",
                    "ThingDef:TorchLamp:-:-",
                }.OrderBy(x => x, StringComparer.Ordinal).ToList();
                s.Check(described.SequenceEqual(expected), "the exported things should be " + string.Join(" | ", expected) + "; the file holds " + string.Join(" | ", described));
                foreach (var li in items) {
                    var allowed = new HashSet<string> { "ThingDef", "TerrainDef", "Stuff", "Position", "Rotation" };
                    var odd = li.Elements().Select(e => e.Name.LocalName).Where(n => !allowed.Contains(n)).ToList();
                    s.Check(odd.Count == 0, "an exported thing holds nodes Fluffy's build does not write: " + string.Join(", ", odd));
                    var position = Attr(li, "Position");
                    s.Check(position == null || System.Text.RegularExpressions.Regex.IsMatch(position, @"^\(-?\d+, 0, -?\d+\)$"), "Position should look like (-2, 0, 1): " + position);
                    s.Check(position != "(0, 0, 0)", "a position of (0, 0, 0) is left out of the file, as Fluffy's build does");
                }
                Bp.Choose(Bp.Option(Bp.OpenRightClickMenu(created), RemoveAndDeleteLabel));
                s.Check(!File.Exists(file), "removing and deleting the exported blueprint should delete its file");
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        [Scenario("blueprints-exports-appear-in-another-save-and-import-correctly", MapSize = 80, MaxSeconds = 150, SaveWithMods = "{mod}", SetupInSaveLaunch = true, ReloadInSaveLaunch = true)]
        static void ExportsAppearInAnotherSave(ScenarioContext s) {
            Start(s, s.Stage == SaveStage.Setup);
            try {
                var source = s.Center + Bp.Source;
                var source2 = s.Center + Source2;
                var source3 = s.Center + Source3;
                var names = new[] { "Alpha", "Beta", "Gamma" };
                if (s.Stage == SaveStage.Setup) {
                    Bp.BuildRoom(s, Bp.Room(), source, 2);
                    Bp.BuildRoom(s, SmallLayout(), source2, 3);
                    Bp.BuildRoom(s, SmallLayout(), source3, 3);
                    var alpha = Bp.Create(s, Bp.DragRect(source, 3, 2), "Alpha");
                    var beta = Bp.Create(s, Bp.DragRect(source2, 3, 3), "Beta");
                    var gamma = Bp.Create(s, Bp.DragRect(source3, 3, 3), "Gamma");
                    foreach (var d in new[] { alpha, beta, gamma }) Bp.Choose(Bp.Option(Bp.OpenRightClickMenu(d), ExportLabel));
                    foreach (var name in names) s.Check(File.Exists(FileOf(name)), "exporting " + name + " should write " + FileOf(name));
                    foreach (var d in new[] { alpha, beta, gamma }) Bp.Choose(Bp.Option(Bp.OpenRightClickMenu(d), RemoveLabel));
                    s.AreEqual(0, Bp.Saved.Count, "blueprints left in this save's menu after removing the three that were exported");
                    return;
                }
                var stage = " (" + s.Stage + ")";
                s.AreEqual(0, Bp.Saved.Count, "blueprints in the menu of the save that never had them" + stage);
                var listed = ImportLabels();
                var wanted = names.Select(ImportLabel).ToList();
                var absent = wanted.Where(w => !listed.Contains(w)).ToList();
                s.Check(absent.Count == 0, "the import menu should list every exported file" + stage + "; it does not list: " + string.Join(" | ", absent) + "; it lists: " + (listed.Count == 0 ? "nothing" : string.Join(" | ", listed)));
                if (absent.Count > 0) return;
                foreach (var name in names) Import(name);
                s.Check(Bp.SavedNames.OrderBy(n => n).SequenceEqual(names), "after importing, the menu should list Alpha, Beta and Gamma" + stage + "; it lists: " + string.Join(", ", Bp.SavedNames));
                foreach (var name in names) {
                    var d = Bp.SavedNamed(name);
                    if (d == null) continue;
                    var labels = MenuLabels(d);
                    s.Check(labels.Contains(RemoveAndDeleteLabel) && !labels.Contains(ExportLabel), "an imported " + name + " should offer '" + RemoveAndDeleteLabel + "' and not '" + ExportLabel + "'" + stage + "; the menu offers: " + string.Join(" | ", labels));
                }
                var partsA = Bp.Harvest(s.Map, source, 3, 2);
                var partsB = Bp.Harvest(s.Map, source2, 3, 3);
                var partsC = Bp.Harvest(s.Map, source3, 3, 3);
                s.Check(partsA.Count > 20 && partsB.Count == 4 && partsC.Count == 4, "the saved map should still hold the three source layouts" + stage + ": " + partsA.Count + ", " + partsB.Count + ", " + partsC.Count);
                var destA = s.Center + Bp.Dest;
                var destB = s.Center + Dest2;
                var destC = s.Center + Dest3;
                Bp.Place(Bp.SavedNamed("Alpha"), destA);
                Bp.Place(Bp.SavedNamed("Beta"), destB);
                Bp.Place(Bp.SavedNamed("Gamma"), destC);
                Bp.CheckLayout(s, Merge(Bp.ExpectedAfter(partsA, destA, 0), Bp.ExpectedAfter(partsB, destB, 0), Bp.ExpectedAfter(partsC, destC, 0)), "the three imported blueprints placed" + stage);
                if (s.Stage == SaveStage.ReloadedInSameLaunch) { s.RequireNoLogErrors(); return; }

                Bp.Choose(Bp.Option(Bp.OpenRightClickMenu(Bp.SavedNamed("Gamma")), RemoveAndDeleteLabel));
                s.Check(Bp.SavedNamed("Gamma") == null, "removing and deleting Gamma should take it out of the menu");
                s.Check(!File.Exists(FileOf("Gamma")), "removing and deleting Gamma should delete " + FileOf("Gamma"));
                s.Check(!ImportLabels().Contains(ImportLabel("Gamma")), "the import menu should not offer a deleted export again");
                Bp.Create(s, Bp.DragRect(source3, 3, 3), "Gamma");
                s.Check(Bp.SavedNamed("Gamma") != null, "a new blueprint should be able to take the name Gamma again");
                s.Check(!File.Exists(FileOf("Gamma")), "a new Gamma is not exported until the player exports it");
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        static XElement FileParts(string name) => XDocument.Load(FileOf(name)).Root.Element("Blueprint").Element("BuildableThings");

        static List<string> PartLines(XElement list) => list.Elements("li")
            .Select(li => string.Join("|", li.Elements().OrderBy(e => e.Name.LocalName, StringComparer.Ordinal).Select(e => e.Name.LocalName + "=" + e.Value)))
            .OrderBy(l => l, StringComparer.Ordinal).ToList();

        static List<string> FixtureParts(string file) => PartLines(XDocument.Load(Path.Combine(FixtureDir, file)).Root.Element("Blueprint").Element("BuildableThings"));

        static string LineOf(Part p) => "Position=(" + p.At.x + ", 0, " + p.At.z + ")" + (p.IsTerrain ? "|Rotation=200" : "") + (p.Stuff != null ? "|Stuff=" + p.Stuff : "") + "|" + (p.IsTerrain ? "TerrainDef=" : "ThingDef=") + p.Def;

        static void Rename(string from, string to) {
            Bp.Choose(Bp.Option(Bp.OpenRightClickMenu(Bp.SavedNamed(from) ?? throw new ScenarioFailure("no blueprint named " + from)), "Fluffy.Blueprints.Rename".Translate().Resolve()));
            if (!Bp.TypeNameAndAccept(to)) throw new ScenarioFailure("the rename dialog did not accept " + to);
        }

        static void CopyFixture(string file) => File.Copy(Path.Combine(FixtureDir, file), Path.Combine(Folder, file), true);

        [Scenario("blueprints-import-an-export-made-by-fluffys-v4-0-53", MapSize = 80)]
        static void ImportFluffysExport(ScenarioContext s) {
            Start(s, true);
            try {
                CopyFixture("Fluffy_Room.xml");
                var listed = ImportLabels();
                s.Check(listed.Contains(ImportLabel("Fluffy_Room")), "the import menu should list Fluffy_Room; it lists: " + string.Join(" | ", listed));
                Import("Fluffy_Room");
                var imported = Bp.SavedNamed("Fluffy_Room");
                s.Check(imported != null, "the menu should list Fluffy_Room after importing it; it lists: " + string.Join(", ", Bp.SavedNames));
                if (imported == null) return;
                s.Check(Bp.LiveMessages().Count == 0, "importing a complete export should show no message; it shows: " + string.Join(" | ", Bp.LiveMessages()));
                var dest = s.Center + Bp.Dest;
                Bp.Place(imported, dest);
                Bp.CheckLayout(s, Bp.ExpectedAfter(Bp.Room(), dest, 0), "Fluffy's room placed");
                Bp.ClearConstruction(s.Map);
                Bp.RotateRight(imported);
                Bp.Place(imported, dest);
                Bp.CheckLayout(s, Bp.ExpectedAfter(Bp.Room(), dest, 1), "Fluffy's room placed turned");
                Bp.DrawArchitectTab();
                var labels = MenuLabels(imported);
                s.Check(labels.Contains(RemoveAndDeleteLabel) && !labels.Contains(ExportLabel), "an imported blueprint should offer '" + RemoveAndDeleteLabel + "' and not '" + ExportLabel + "'; the menu offers: " + string.Join(" | ", labels));
                Bp.Choose(Bp.Option(Bp.OpenRightClickMenu(imported), RemoveAndDeleteLabel));
                s.Check(!File.Exists(FileOf("Fluffy_Room")), "removing and deleting should delete the file");
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        static List<Part> ForkedParts() => new List<Part> {
            Part.Thing("Wall", -2, -1, "BlocksGranite"),
            Part.Thing("Wall", 2, -1, "BlocksGranite"),
            Part.Thing("Bed", 0, 0, "WoodLog", Rot4.East),
            Part.Thing("TorchLamp", -2, 1),
            Part.Floor("WoodPlankFloor", 3, 2),
        };

        [Scenario("blueprints-import-a-later-builds-file-with-a-long-name-extra-nodes-and-an-unreadable-part", MapSize = 80)]
        static void ImportLaterBuildsFile(ScenarioContext s) {
            Start(s, true);
            const string name = "Forked_Library_Bedroom_With_A_Very_Long_Name";
            try {
                CopyFixture(name + ".xml");
                Import(name);
                var imported = Bp.SavedNamed(name);
                s.Check(imported != null, "the menu should list " + name + " after importing it; it lists: " + string.Join(", ", Bp.SavedNames));
                if (imported == null) return;
                var notices = Notices();
                s.Check(notices.Any(n => n.Contains(name) && n.Contains("Wall") && n.Contains("(oops)")), "the player should be told which part could not be read (a Wall with the position (oops)); the messages are: " + string.Join(" | ", notices));
                var fragment = "Fluffy.Blueprints.UnreadablePosition".Translate("Wall", "(oops)").Resolve();
                s.Check(fragment != "Fluffy.Blueprints.UnreadablePosition" && notices.Any(n => n.Contains(fragment)), "the message should say '" + fragment + "' from the translation key; the messages are: " + string.Join(" | ", notices));
                s.Check(notices.Any(n => n.Contains(name) && n.Contains("1 part ")) && !notices.Any(n => n.Contains("1 parts")), "one part left out should read '1 part', not '1 parts'; the messages are: " + string.Join(" | ", notices));
                var dest = s.Center + Bp.Dest;
                Bp.Place(imported, dest);
                Bp.CheckLayout(s, Bp.ExpectedAfter(ForkedParts(), dest, 0), "the readable parts of the later build's file placed");
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        static List<Part> MixedParts() {
            var parts = new List<Part> {
                Part.Thing("Wall", -3, 0, "BlocksGranite"),
                Part.Thing("Wall", -2, 0, "BlueprintsExtra_Ingot"),
                Part.Thing("BlueprintsExtra_Shrine", -1, 0),
                Part.Thing("TorchLamp", 0, 0),
                Part.Thing("PlantPot", 1, 0, "WoodLog"),
                Part.Floor("WoodPlankFloor", -3, -2),
                Part.Floor("WoodPlankFloor", -2, -2),
                Part.Floor("BlueprintsExtra_Tile", -1, -2),
                Part.Floor("BlueprintsExtra_Tile", 0, -2),
                Part.Floor("WoodPlankFloor", 1, -2),
            };
            parts.Add(Bp.Table());
            return parts;
        }

        static List<Part> PresentIn(IEnumerable<Part> parts) => parts.Where(p =>
            (p.IsTerrain ? (Def)DefDatabase<TerrainDef>.GetNamedSilentFail(p.Def) : DefDatabase<ThingDef>.GetNamedSilentFail(p.Def)) != null &&
            (p.Stuff == null || DefDatabase<ThingDef>.GetNamedSilentFail(p.Stuff) != null)).ToList();

        static void ClearSource(ScenarioContext s, IntVec3 center) {
            foreach (var cell in Bp.DragRect(center, 5, 6)) {
                foreach (var thing in cell.GetThingList(s.Map).OfType<Building>().ToList()) thing.Destroy(DestroyMode.Vanish);
                if (s.Map.terrainGrid.TerrainAt(cell) != TerrainDefOf.Soil) s.Map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
            }
            var left = Bp.DragRect(center, 5, 6).SelectMany(c => c.GetThingList(s.Map)).Where(t => t.def.modContentPack?.PackageIdPlayerFacing == "rimhunter.blueprints.testextradefs").ToList();
            if (left.Count > 0) throw new ScenarioFailure("things from the test-only mod are still on the map: " + string.Join(", ", left.Select(t => t.def.defName)));
        }

        static readonly string[] MissingNames = { "BlueprintsExtra_Shrine", "BlueprintsExtra_Tile", "BlueprintsExtra_Ingot" };

        static void CheckMissingMessage(ScenarioContext s, string blueprintName, string when) {
            var notices = Notices();
            var told = notices.Where(n => n.Contains(blueprintName) && MissingNames.All(n.Contains)).ToList();
            s.Check(told.Count > 0, when + ": the player should be told that " + string.Join(", ", MissingNames) + " were left out of " + blueprintName + "; the messages are: " + (notices.Count == 0 ? "none" : string.Join(" | ", notices)));
        }

        [Scenario("screenshot-blueprints-missing-defs-message", MapSize = 80, Windowed = true, MaxSeconds = 150)]
        static IEnumerator MissingDefsMessage(ScenarioContext s) {
            Start(s, true);
            try {
                CopyFixture("Returning_Mod.xml");
                Bp.ClearMessages();
                Import("Returning_Mod");
                s.Check(Bp.SavedNamed("Returning_Mod") != null, "the blueprint should be imported although it names defs this game lacks");
                CheckMissingMessage(s, "Returning_Mod", "importing the file");
                var dest = s.Center + Bp.Dest;
                Bp.Place(Bp.SavedNamed("Returning_Mod"), dest);
                Find.DesignatorManager.Deselect();
                s.LookAt(dest, 24);
                yield return null;
                var shot = s.Screenshot("05-missing-defs-message");
                yield return shot;
                s.Check(shot.ShowsText("BlueprintsExtra_Shrine"), "the message on screen should name the missing BlueprintsExtra_Shrine; the screenshot showed: " + shot.TextsMatching("Returning"));
                s.RequireNoLogErrors();
            }
            finally {
                File.Delete(FileOf("Returning_Mod"));
                Bp.End();
            }
            yield return null;
        }

        [Scenario("blueprints-import-a-file-whose-mods-are-gone-loads-the-rest-and-says-what-is-missing", MapSize = 80, MaxSeconds = 150, SaveWithMods = "{mod} TestRunner/Fixtures/blueprints-extra-defs", SetupInSaveLaunch = true)]
        static void ImportFileWithMissingParts(ScenarioContext s) {
            Start(s, s.Stage == SaveStage.Setup);
            try {
                var source = s.Center + Bp.Source;
                if (s.Stage == SaveStage.Setup) {
                    Bp.BuildRoom(s, MixedParts(), source, 4);
                    var mixed = Bp.Create(s, Bp.DragRect(source, 3, 4), "Mixed");
                    Bp.Choose(Bp.Option(Bp.OpenRightClickMenu(mixed), ExportLabel));
                    s.Check(File.Exists(FileOf("Mixed")), "exporting Mixed should write " + FileOf("Mixed"));
                    Bp.Choose(Bp.Option(Bp.OpenRightClickMenu(mixed), RemoveLabel));
                    ClearSource(s, source);
                    return;
                }
                s.Check(DefDatabase<ThingDef>.GetNamedSilentFail("BlueprintsExtra_Shrine") == null, "the launch that loads the save should not have the test-only mod");
                Bp.ClearMessages();
                Import("Mixed");
                var imported = Bp.SavedNamed("Mixed");
                s.Check(imported != null, "the blueprint should be imported although it names a building, a floor and a stuff this game lacks; the menu lists: " + string.Join(", ", Bp.SavedNames));
                if (imported == null) return;
                CheckMissingMessage(s, "Mixed", "importing the file");
                var dest = s.Center + Bp.Dest;
                Bp.Place(imported, dest);
                Bp.CheckLayout(s, Bp.ExpectedAfter(PresentIn(MixedParts()), dest, 0), "the parts that exist, placed");
                var labels = MenuLabels(imported);
                s.Check(labels.Contains(RemoveAndDeleteLabel), "the imported blueprint should offer '" + RemoveAndDeleteLabel + "'; the menu offers: " + string.Join(" | ", labels));
                Bp.DrawArchitectTab();
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        [Scenario("blueprints-load-a-save-whose-mods-are-gone-loads-the-rest-and-says-what-is-missing", MapSize = 80, MaxSeconds = 150, SaveWithMods = "{mod} TestRunner/Fixtures/blueprints-extra-defs", SetupInSaveLaunch = true)]
        static void LoadSaveWithMissingParts(ScenarioContext s) {
            Start(s, s.Stage == SaveStage.Setup);
            try {
                var source = s.Center + Bp.Source;
                if (s.Stage == SaveStage.Setup) {
                    Bp.BuildRoom(s, MixedParts(), source, 4);
                    Bp.Create(s, Bp.DragRect(source, 3, 4), "Kept");
                    ClearSource(s, source);
                    return;
                }
                s.Check(DefDatabase<ThingDef>.GetNamedSilentFail("BlueprintsExtra_Shrine") == null, "the launch that loads the save should not have the test-only mod");
                s.Check(Bp.SavedNames.SequenceEqual(new[] { "Kept" }), "the loaded save should list Kept; it lists: " + string.Join(", ", Bp.SavedNames));
                CheckMissingMessage(s, "Kept", "loading the save");
                var kept = Bp.SavedNamed("Kept");
                if (kept == null) return;
                var dest = s.Center + Bp.Dest;
                Bp.Place(kept, dest);
                Bp.CheckLayout(s, Bp.ExpectedAfter(PresentIn(MixedParts()), dest, 0), "the parts that exist, placed from the loaded save");
                Bp.DrawArchitectTab();
                Bp.Choose(Bp.Option(Bp.OpenRightClickMenu(kept), ExportLabel));
                s.Check(File.Exists(FileOf("Kept")), "exporting Kept should write " + FileOf("Kept"));
                if (File.Exists(FileOf("Kept"))) {
                    var written = PartLines(FileParts("Kept")).Where(l => MissingNames.Any(l.Contains)).OrderBy(l => l, StringComparer.Ordinal).ToList();
                    var wanted = MixedParts().Where(p => MissingNames.Contains(p.Def) || MissingNames.Contains(p.Stuff)).Select(LineOf).OrderBy(l => l, StringComparer.Ordinal).ToList();
                    s.Check(written.SequenceEqual(wanted), "an export of a blueprint with parts left out should still hold them; the file holds " + string.Join(" ; ", written) + " but should hold " + string.Join(" ; ", wanted));
                    File.Delete(FileOf("Kept"));
                }
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        static void RewriteBlueprintsOfSavedGame(string fileName) {
            if (fileName != "rimhunter-blueprints-load-a-save-written-by-fluffys-build") return;
            var path = GenFilePaths.FilePathForSavedGame(fileName);
            var doc = XDocument.Load(path);
            var component = doc.Descendants("li").FirstOrDefault(e => (string)e.Attribute("Class") == "Blueprints.BlueprintController")
                ?? throw new ScenarioFailure("the saved game has no world component of the class Blueprints.BlueprintController, so a save made with Fluffy's build would not match it");
            var fluffy = XDocument.Load(Path.Combine(FixtureDir, "Fluffy_Room.xml")).Root.Element("Blueprint");
            var entry = new XElement("li", fluffy.Elements());
            entry.Add(new XElement("Exported", "True"));
            component.Element("Blueprints")?.Remove();
            component.Add(new XElement("Blueprints", entry));
            doc.Save(path);
        }

        [Scenario("blueprints-load-a-save-written-by-fluffys-build", MapSize = 80, MaxSeconds = 150, SaveWithMods = "{mod}", SetupInSaveLaunch = true)]
        static void LoadSaveWrittenByFluffy(ScenarioContext s) {
            Start(s, s.Stage == SaveStage.Setup);
            try {
                if (s.Stage == SaveStage.Setup) {
                    var source = s.Center + Bp.Source;
                    Bp.BuildRoom(s, SmallLayout(), source, 3);
                    Bp.Create(s, Bp.DragRect(source, 3, 3), "Seed");
                    new Harmony("rimhunter.blueprints.testrunner.fluffysave").Patch(AccessTools.Method(typeof(GameDataSaveLoader), nameof(GameDataSaveLoader.SaveGame)),
                        postfix: new HarmonyMethod(typeof(BlueprintsExport), nameof(RewriteBlueprintsOfSavedGame)));
                    return;
                }
                s.Check(Bp.SavedNames.SequenceEqual(new[] { "Fluffy_Room" }), "the save written by Fluffy's build should list Fluffy_Room; it lists: " + string.Join(", ", Bp.SavedNames));
                var room = Bp.SavedNamed("Fluffy_Room");
                if (room == null) return;
                var labels = MenuLabels(room);
                s.Check(labels.Contains(RemoveAndDeleteLabel), "a blueprint that Fluffy's build saved as exported should offer '" + RemoveAndDeleteLabel + "'; the menu offers: " + string.Join(" | ", labels));
                var dest = s.Center + Bp.Dest;
                Bp.Place(room, dest);
                Bp.CheckLayout(s, Bp.ExpectedAfter(Bp.Room(), dest, 0), "Fluffy's room placed from the save his build wrote");
                Bp.DrawArchitectTab();
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        static List<Part> ReturningParts() => MixedParts().Where(p => p.Def != "Table2x2c").ToList();

        [Scenario("blueprints-parts-of-a-mod-that-is-gone-stay-in-the-file-and-come-back-with-the-mod", MapSize = 80, MaxSeconds = 150, SaveWithMods = "{mod}", Mods = "TestRunner/Fixtures/blueprints-extra-defs", SetupInSaveLaunch = true, ReloadInSaveLaunch = true)]
        static void PartsOfAGoneModStayInTheFile(ScenarioContext s) {
            Start(s, s.Stage == SaveStage.Setup);
            try {
                var wanted = FixtureParts("Returning_Mod.xml");
                var wantedLeftOut = wanted.Where(l => MissingNames.Any(l.Contains)).ToList();
                if (s.Stage == SaveStage.Setup) {
                    s.Check(DefDatabase<ThingDef>.GetNamedSilentFail("BlueprintsExtra_Shrine") == null, "the launch that saves should not have the test-only mod");
                    CopyFixture("Returning_Mod.xml");
                    Import("Returning_Mod");
                    var imported = Bp.SavedNamed("Returning_Mod");
                    s.Check(imported != null, "the file should import without the mod it names; the menu lists: " + string.Join(", ", Bp.SavedNames));
                    if (imported == null) return;
                    CheckMissingMessage(s, "Returning_Mod", "importing the file");
                    Rename("Returning_Mod", "Returning_Saved");
                    s.Check(File.Exists(FileOf("Returning_Saved")) && !File.Exists(FileOf("Returning_Mod")), "renaming the imported blueprint should move its file to " + FileOf("Returning_Saved"));
                    if (!File.Exists(FileOf("Returning_Saved"))) return;
                    CheckFileKeepsParts(s, "Returning_Saved", wanted, wantedLeftOut, "after the rename");
                    return;
                }
                if (s.Stage == SaveStage.ReloadedInSameLaunch) {
                    s.Check(Bp.SavedNames.SequenceEqual(new[] { "Returning_Saved" }), "the reloaded save should list Returning_Saved; it lists: " + string.Join(", ", Bp.SavedNames));
                    Rename("Returning_Saved", "Returning_Again");
                    s.Check(File.Exists(FileOf("Returning_Again")) && !File.Exists(FileOf("Returning_Saved")), "renaming after the reload should move the file to " + FileOf("Returning_Again"));
                    if (!File.Exists(FileOf("Returning_Again"))) return;
                    CheckFileKeepsParts(s, "Returning_Again", wanted, wantedLeftOut, "after a save, a reload and a rename");
                    File.Delete(FileOf("Returning_Again"));
                    s.RequireNoLogErrors();
                    return;
                }
                s.Check(DefDatabase<ThingDef>.GetNamedSilentFail("BlueprintsExtra_Shrine") != null, "the launch that loads the save should have the test-only mod again");
                s.Check(Bp.SavedNames.SequenceEqual(new[] { "Returning_Saved" }), "the loaded save should list Returning_Saved; it lists: " + string.Join(", ", Bp.SavedNames));
                var returned = Bp.SavedNamed("Returning_Saved");
                if (returned == null) return;
                s.Check(!Bp.LiveMessages().Any(m => m.Contains("left out")), "with the mod back nothing is left out, but the messages are: " + string.Join(" | ", Bp.LiveMessages()));
                var dest = s.Center + Bp.Dest;
                Bp.Place(returned, dest);
                Bp.CheckLayout(s, Bp.ExpectedAfter(ReturningParts(), dest, 0), "every part placed once the mod is back");
                Bp.DrawArchitectTab();
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        static List<Part> FlippedAcrossX(IEnumerable<Part> parts) => parts.Select(p => p.IsTerrain
            ? Part.Floor(p.Def, -p.At.x, p.At.z)
            : Part.Thing(p.Def, -p.At.x, p.At.z, p.Stuff, p.Rot.AsInt % 2 == 1 ? new Rot4(p.Rot.AsInt + 2) : p.Rot)).ToList();

        [Scenario("blueprints-parts-of-a-mod-that-is-gone-are-turned-and-flipped-with-the-rest", MapSize = 80, MaxSeconds = 150, SaveWithMods = "{mod}", Mods = "TestRunner/Fixtures/blueprints-extra-defs", SetupInSaveLaunch = true)]
        static void PartsOfAGoneModAreTurnedAndFlipped(ScenarioContext s) {
            Start(s, s.Stage == SaveStage.Setup);
            try {
                if (s.Stage == SaveStage.Setup) {
                    s.Check(DefDatabase<ThingDef>.GetNamedSilentFail("BlueprintsExtra_Shrine") == null, "the launch that saves should not have the test-only mod");
                    CopyFixture("Returning_Mod.xml");
                    Import("Returning_Mod");
                    var imported = Bp.SavedNamed("Returning_Mod");
                    s.Check(imported != null, "the file should import without the mod it names; the menu lists: " + string.Join(", ", Bp.SavedNames));
                    if (imported == null) return;
                    Find.DesignatorManager.Select(imported);
                    Bp.RotateRight(imported);
                    Bp.Flip(imported);
                    Rename("Returning_Mod", "Returning_Turned");
                    s.Check(File.Exists(FileOf("Returning_Turned")), "renaming the turned blueprint should write " + FileOf("Returning_Turned"));
                    return;
                }
                s.Check(DefDatabase<ThingDef>.GetNamedSilentFail("BlueprintsExtra_Shrine") != null, "the launch that loads the save should have the test-only mod again");
                s.Check(Bp.SavedNames.SequenceEqual(new[] { "Returning_Turned" }), "the loaded save should list Returning_Turned; it lists: " + string.Join(", ", Bp.SavedNames));
                var expected = Bp.ExpectedAfter(FlippedAcrossX(ReturningParts()), s.Center + Bp.Dest, 3);
                var fromSave = Bp.SavedNamed("Returning_Turned");
                if (fromSave == null) return;
                Bp.Place(fromSave, s.Center + Bp.Dest);
                Bp.CheckLayout(s, expected, "the turned and flipped blueprint from the saved game, placed");
                Bp.ClearConstruction(s.Map);
                Bp.Choose(Bp.Option(Bp.OpenRightClickMenu(fromSave), "Fluffy.Blueprints.Remove".Translate().Resolve()));
                Import("Returning_Turned");
                var fromFile = Bp.SavedNamed("Returning_Turned");
                s.Check(fromFile != null, "the turned file should import once the mod is back; the menu lists: " + string.Join(", ", Bp.SavedNames));
                if (fromFile == null) return;
                Bp.Place(fromFile, s.Center + Bp.Dest);
                Bp.CheckLayout(s, expected, "the turned and flipped blueprint from the exported file, placed");
                File.Delete(FileOf("Returning_Turned"));
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        static void CheckFileKeepsParts(ScenarioContext s, string name, List<string> wanted, List<string> wantedLeftOut, string when) {
            var written = PartLines(FileParts(name));
            var writtenLeftOut = written.Where(l => MissingNames.Any(l.Contains)).ToList();
            s.Check(written.Count == wanted.Count, when + " " + name + ".xml should hold all " + wanted.Count + " parts, including the ones left out of this game; it holds " + written.Count);
            s.Check(writtenLeftOut.SequenceEqual(wantedLeftOut), when + " " + name + ".xml should still hold " + string.Join(" ; ", wantedLeftOut) + "; it holds " + (writtenLeftOut.Count == 0 ? "none of them" : string.Join(" ; ", writtenLeftOut)));
        }

        static void WriteFile(string name, string text) => File.WriteAllText(FileOf(name), text);

        const string TinyMeta = "<meta><gameVersion>1.5.4104 rev435</gameVersion><modIds><li>ludeon.rimworld</li></modIds><modSteamIds><li>0</li></modSteamIds><modNames><li>Core</li></modNames></meta>";

        static void CheckUnreadable(ScenarioContext s, string name) {
            Bp.ClearMessages();
            Import(name);
            s.Check(Bp.SavedNamed(name) == null, name + ".xml should not be imported; the menu lists: " + string.Join(", ", Bp.SavedNames));
            var told = Bp.LiveMessages().Where(m => m.Contains(name + ".xml")).ToList();
            s.Check(told.Count == 1, "the player should be told once that " + name + ".xml could not be read; the messages are: " + (Bp.LiveMessages().Count == 0 ? "none" : string.Join(" | ", Bp.LiveMessages())));
        }

        [Scenario("blueprints-import-a-file-that-is-not-readable-xml-says-so-and-logs-no-error", MapSize = 80)]
        static void ImportMalformedFile(ScenarioContext s) {
            Start(s, true);
            try {
                WriteFile("Broken_File", "<Blueprint><meta><gameVersion>1.5");
                CheckUnreadable(s, "Broken_File");
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); File.Delete(FileOf("Broken_File")); }
        }

        [Scenario("blueprints-import-a-file-of-the-wrong-shape-says-so-and-does-not-make-an-empty-blueprint", MapSize = 80)]
        static void ImportWrongShapeFile(ScenarioContext s) {
            Start(s, true);
            try {
                WriteFile("Wrong_Shape", "<Blueprint>" + TinyMeta + "<Other>1</Other></Blueprint>");
                WriteFile("No_Part_List", "<Blueprint>" + TinyMeta + "<Blueprint><Name>No_Part_List</Name><Size>(1, 1)</Size></Blueprint></Blueprint>");
                CheckUnreadable(s, "Wrong_Shape");
                CheckUnreadable(s, "No_Part_List");
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); File.Delete(FileOf("Wrong_Shape")); File.Delete(FileOf("No_Part_List")); }
        }

        const string HollowScenario = "blueprints-load-a-save-whose-blueprint-has-no-list-of-parts-says-so";

        static void RemovePartsListOfSavedGame(string fileName) {
            if (fileName != "rimhunter-" + HollowScenario) return;
            var path = GenFilePaths.FilePathForSavedGame(fileName);
            var doc = XDocument.Load(path);
            var component = doc.Descendants("li").FirstOrDefault(e => (string)e.Attribute("Class") == "Blueprints.BlueprintController")
                ?? throw new ScenarioFailure("the saved game has no Blueprints.BlueprintController component");
            var lists = component.Descendants("BuildableThings").ToList();
            if (lists.Count != 1) throw new ScenarioFailure("expected one BuildableThings list in the saved game, found " + lists.Count);
            lists[0].Remove();
            doc.Save(path);
        }

        [Scenario("blueprints-load-a-save-whose-blueprint-has-no-list-of-parts-says-so", MapSize = 80, MaxSeconds = 150, SaveWithMods = "{mod}", SetupInSaveLaunch = true)]
        static void LoadSaveWithoutPartsList(ScenarioContext s) {
            Start(s, s.Stage == SaveStage.Setup);
            try {
                if (s.Stage == SaveStage.Setup) {
                    var source = s.Center + Bp.Source;
                    Bp.BuildRoom(s, SmallLayout(), source, 3);
                    Bp.Create(s, Bp.DragRect(source, 3, 3), "Hollow");
                    new Harmony("rimhunter.blueprints.testrunner.nolist").Patch(AccessTools.Method(typeof(GameDataSaveLoader), nameof(GameDataSaveLoader.SaveGame)),
                        postfix: new HarmonyMethod(typeof(BlueprintsExport), nameof(RemovePartsListOfSavedGame)));
                    return;
                }
                var notices = Notices();
                s.Check(notices.Any(n => n.Contains("Hollow") && n.Contains("no list of parts")), "the player should be told that Hollow has no list of parts; the messages are: " + (notices.Count == 0 ? "none" : string.Join(" | ", notices)));
                Bp.DrawArchitectTab();
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }

        const string GoneScenario = "blueprints-a-deleted-export-stays-gone-after-a-restart-and-its-name-can-be-reused";

        [Scenario("blueprints-a-deleted-export-stays-gone-after-a-restart-and-its-name-can-be-reused", MapSize = 80, MaxSeconds = 150, SaveWithMods = "{mod}", SetupInSaveLaunch = true, ReloadInSaveLaunch = true)]
        static void DeletedExportStaysGone(ScenarioContext s) {
            Start(s, s.Stage == SaveStage.Setup);
            try {
                var source = s.Center + Bp.Source;
                if (s.Stage == SaveStage.Setup) {
                    Bp.BuildRoom(s, SmallLayout(), source, 3);
                    var gone = Bp.Create(s, Bp.DragRect(source, 3, 3), "Gone");
                    Bp.Choose(Bp.Option(Bp.OpenRightClickMenu(gone), ExportLabel));
                    s.Check(File.Exists(FileOf("Gone")), "exporting Gone should write " + FileOf("Gone"));
                    return;
                }
                if (s.Stage == SaveStage.ReloadedInSameLaunch) {
                    var reloaded = Bp.SavedNamed("Gone");
                    s.Check(reloaded != null, "the reloaded save should list Gone");
                    if (reloaded == null) return;
                    Bp.Choose(Bp.Option(Bp.OpenRightClickMenu(reloaded), RemoveAndDeleteLabel));
                    s.Check(Bp.SavedNamed("Gone") == null && !File.Exists(FileOf("Gone")), "removing and deleting Gone should take it out of the menu and delete its file");
                    GameDataSaveLoader.SaveGame("rimhunter-" + GoneScenario);
                    return;
                }
                s.AreEqual(0, Bp.Saved.Count, "blueprints in the menu after a restart that follows 'remove and delete exported'");
                s.Check(!File.Exists(FileOf("Gone")), "the deleted export should still be gone after the restart");
                s.Check(!ImportLabels().Contains(ImportLabel("Gone")), "the import menu should not offer the deleted export after the restart");
                Bp.Create(s, Bp.DragRect(source, 3, 3), "Gone");
                s.Check(Bp.SavedNamed("Gone") != null, "a new blueprint should be able to take the name Gone after the restart");
                s.Check(!File.Exists(FileOf("Gone")), "a new Gone is not exported until the player exports it");
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }
    }
}
