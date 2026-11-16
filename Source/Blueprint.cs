// Copyright Karel Kroeze, 2020-2021.
// Blueprints/Blueprints/Blueprint.cs

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using RimWorld;
using Verse;

namespace Blueprints {
    public class Blueprint: IExposable, IRenameable {
        // regex for valid file names. Should allow all 'normal' characters, where normal (i.e. \w) differs per localization.
        private static readonly Regex ValidNameRegex = new Regex(@"^[\w]+$");

        private List<BuildableInfo> _availableContents;
        private Dictionary<IntVec3, BuildableInfo> _foundationParts;
        private List<BuildableDef>  _buildables;
        private List<ThingDefCount> _costlist;

        private readonly HashSet<FailReason>                           _failReasonsMentioned = new HashSet<FailReason>();
        private Dictionary<BuildableDef, List<BuildableInfo>> _groupedBuildables;
        private IntVec2                                       _size;

        public List<BuildableInfo> contents;
        public bool                exported;
        public string              name;

        public Blueprint() {
            // empty for Scribe;
        }

        public Blueprint(List<BuildableInfo> contents, IntVec2 size, string defaultName = null, bool temporary = false) {
            // input
            this.contents = contents;
            name = defaultName;
            _size = size;

            // provide reference to this blueprint in all contents
            foreach (BuildableInfo item in contents) {
                item.blueprint = this;
            }

            // just created, so not exported yet
            exported = false;

            // 'orrible default name
            if (name == null || !CouldBeValidBlueprintName(name)) {
                name = "Fluffy.Blueprints.DefaultBlueprintName".Translate();
            }

            // increment numeric suffix until we have a unique name
            int i = 1;
            while (BlueprintController.FindBlueprint(name + "_" + i) != null) {
                i++;
            }

            // set name
            name = name + "_" + i;

            // ask for name
            if (!temporary) {
                Find.WindowStack.Add(new Dialog_NameBlueprint(this));
            }
        }

        public List<BuildableInfo> AvailableContents {
            get {
                _availableContents ??= contents.Where(item => item.Designator.Visible)
                                                 .ToList();

                return _availableContents;
            }
        }

        public List<BuildableDef> Buildables {
            get {
                _buildables ??= AvailableContents.Select(item => item.BuildableDef)
                                                   .ToList();

                return _buildables;
            }
        }

        public List<ThingDefCount> CostListAdjusted {
            get {
                _costlist ??= CreateCostList();

                return _costlist;
            }
        }

        public Dictionary<BuildableDef, List<BuildableInfo>> GroupedBuildables {
            get {
                // return cached
                if (_groupedBuildables != null) {
                    return _groupedBuildables;
                }

                // create and cache list
                Dictionary<BuildableDef, List<BuildableInfo>> dict = new Dictionary<BuildableDef, List<BuildableInfo>>();

                foreach (BuildableDef buildable in Buildables.Distinct()) {
                    dict.Add(buildable, AvailableContents.Where(bi => bi.BuildableDef == buildable).ToList());
                }

                _groupedBuildables = dict;
                return dict;
            }
        }

        public string RenamableLabel { get => name; set => name = value; }

        public string BaseLabel => name;

        public string InspectLabel => name;

        public void ExposeData() {
            if (Scribe.mode == LoadSaveMode.Saving) {
                List<BuildableInfo> parts = contents.Concat(_unresolvedParts).ToList();
                Scribe_Collections.Look(ref parts, "BuildableThings", LookMode.Deep, this);
            } else {
                Scribe_Collections.Look(ref contents, "BuildableThings", LookMode.Deep, this);
            }

            Scribe_Values.Look(ref name, "Name");
            Scribe_Values.Look(ref _size, "Size");
            Scribe_Values.Look(ref exported, "Exported");
            if (Scribe.mode == LoadSaveMode.LoadingVars) {
                RemovePartsThatCannotBeUsed();
            }
        }

        public int PartsLeftOut { get; private set; }

        public bool HasNoPartsList { get; private set; }

        public List<string> LeftOutReasons { get; private set; } = new List<string>();

        private List<BuildableInfo> _unresolvedParts = new List<BuildableInfo>();

        private void RemovePartsThatCannotBeUsed() {
            if (contents == null) {
                HasNoPartsList = true;
                contents = new List<BuildableInfo>();
                return;
            }

            _unresolvedParts = contents.Where(item => item.IsUnresolved).ToList();
            PartsLeftOut = _unresolvedParts.Count;
            LeftOutReasons = _unresolvedParts.SelectMany(item => item.LeftOutReasons).Distinct().ToList();
            contents.RemoveAll(item => item.IsUnresolved);
        }

        public void ReportAndClearLeftOut() {
            if (HasNoPartsList) {
                Messages.Message("Fluffy.Blueprints.NoPartsList".Translate(name), MessageTypeDefOf.CautionInput);
                HasNoPartsList = false;
            }

            if (PartsLeftOut == 0) {
                return;
            }

            string key = Resources.CountKey(PartsLeftOut, "Fluffy.Blueprints.PartLeftOut", "Fluffy.Blueprints.PartsLeftOut");
            Messages.Message(key.Translate(name, PartsLeftOut, string.Join(", ", LeftOutReasons)),
                             MessageTypeDefOf.CautionInput);
            PartsLeftOut = 0;
        }

        private Dictionary<IntVec3, BuildableInfo> FoundationParts {
            get {
                _foundationParts ??= AvailableContents.Where(item => item.IsFoundation)
                                                      .GroupBy(item => item.Position)
                                                      .ToDictionary(group => group.Key, group => group.First());

                return _foundationParts;
            }
        }

        private static bool HasFoundationOrder(Map map, IntVec3 cell) {
            foreach (Thing thing in cell.GetThingList(map)) {
                if (thing.def.entityDefToBuild is TerrainDef { isFoundation: true }) {
                    return true;
                }
            }

            return false;
        }

        public bool WaitsForFoundation(BuildableInfo item, IntVec3 origin) {
            if (item.IsFoundation) {
                return false;
            }

            Dictionary<IntVec3, BuildableInfo> foundations = FoundationParts;
            if (foundations.Count == 0) {
                return false;
            }

            Map map = Find.CurrentMap;
            foreach (IntVec3 cell in item.OccupiedCells(origin)) {
                if (!cell.InBounds(map) || map.terrainGrid.FoundationAt(cell) != null) {
                    continue;
                }

                if (foundations.TryGetValue(cell - origin, out BuildableInfo foundation) &&
                    (HasFoundationOrder(map, cell) || foundation.CanPlace(origin) == PlacementReport.CanPlace)) {
                    return true;
                }
            }

            return false;
        }

        public readonly struct PlacementResult {
            public readonly int Placed;
            public readonly int Total;
            public readonly int Waiting;
            public readonly int NotPlaced;

            public PlacementResult(int placed, int total, int waiting, int notPlaced) {
                Placed = placed;
                Total = total;
                Waiting = waiting;
                NotPlaced = notPlaced;
            }
        }

        // keeps going until nothing more can be placed: wall attachments need their wall's order to exist first,
        // and they can be stored before it.
        public PlacementResult PlaceAt(IntVec3 origin) {
            List<BuildableInfo> pending = AvailableContents.OrderBy(item => item.IsFoundation ? 0 : 1).ToList();
            HashSet<IntVec3> coveredByThings = new HashSet<IntVec3>();
            int total = pending.Count;
            int placed = 0;
            bool progress;
            do {
                progress = false;
                for (int i = 0; i < pending.Count; i++) {
                    PlacementReport report = pending[i].CanPlace(origin);
                    if (report == PlacementReport.CanNotPlace) {
                        continue;
                    }

                    if (report == PlacementReport.CanPlace) {
                        pending[i].Designate(origin);
                        placed++;
                    }

                    if (pending[i].BuildableDef is ThingDef thingDef && !thingDef.CoexistsWithFloors) {
                        coveredByThings.UnionWith(pending[i].OccupiedCells(origin));
                    }

                    pending.RemoveAt(i--);
                    progress = true;
                }
            } while (progress && pending.Count > 0);

            // the game does not allow a floor order under a wall order, so a floor that a thing which cannot share its cell
            // with a floor covers is left out, and is not a failure.
            total -= pending.RemoveAll(item => item.BuildableDef is TerrainDef && coveredByThings.Contains(origin + item.Position));

            HashSet<IntVec3> refusedFoundations = new HashSet<IntVec3>(pending.Where(item => item.IsFoundation)
                                                                               .SelectMany(item => item.OccupiedCells(origin)));
            int waiting = pending.RemoveAll(item => WaitsForFoundation(item, origin) &&
                                                    !item.OccupiedCells(origin).Any(refusedFoundations.Contains));

            return new PlacementResult(placed, total, waiting, pending.Count);
        }

        public bool CanPlace(IntVec3 origin) {
            return CanPlace(origin, Rot4.North);
        }

        public bool CanPlace(IntVec3 origin, Rot4 rotation) {
            // check each individual element for placement issues
            foreach (BuildableInfo item in AvailableContents) {
                if (item.CanPlace(origin) == PlacementReport.CanNotPlace) {
                    return false;
                }
            }

            return true;
        }

        public static bool CouldBeValidBlueprintName(string name) {
            return ValidNameRegex.IsMatch(name);
        }

        public static void Create(IEnumerable<IntVec3> cells, Map map) {
            // bail out if empty
            if (cells == null || !cells.Any()) {
                Messages.Message("Fluffy.Blueprints.CannotCreateBluePrint_NothingSelected".Translate(),
                                 MessageTypeDefOf.RejectInput);
                return;
            }

            // get list of buildings in the cells, note that this includes frames and blueprints, and so _may include floors!_
            List<Thing> things = new List<Thing>(cells.SelectMany(cell => cell.GetThingList(map)
                                                                      .Where(thing => thing.IsValidBlueprintThing()))
                                              .Distinct());

            // get list of creatable terrains
            List<Pair<TerrainDef, IntVec3>> terrains = new List<Pair<TerrainDef, IntVec3>>();
            // a foundation (gravship substructure, a bridge) and the floor laid on it are two layers of one cell.
            foreach (IntVec3 cell in cells) {
                TerrainDef foundation = map.terrainGrid.FoundationAt(cell);
                TerrainDef surface = cell.GetTerrain(map);
                if (foundation != null && foundation.IsValidBlueprintTerrain()) {
                    terrains.Add(new Pair<TerrainDef, IntVec3>(foundation, cell));
                }

                if (surface != foundation && surface.IsValidBlueprintTerrain()) {
                    terrains.Add(new Pair<TerrainDef, IntVec3>(surface, cell));
                }
            }

            // get edges of blueprint area
            // (might be bigger than selected region, but never smaller).
            IEnumerable<IntVec3> allCells = cells.Concat(things.SelectMany(thing => thing.OccupiedRect().Cells));

            int left   = allCells.Min(cell => cell.x);
            int top    = allCells.Max(cell => cell.z);
            int right  = allCells.Max(cell => cell.x);
            int bottom = allCells.Min(cell => cell.z);

            // total size ( +1 because x = 2 ... x = 4 => 4 - 2 + 1 cells )
            IntVec2 size = new IntVec2(right - left + 1, top - bottom + 1);

            // fetch origin for default (North) orientation
            IntVec3 origin = Resources.CenterPosition(new IntVec3(left, 0, bottom), size, Rot4.North);

            // create list of buildables
            List<BuildableInfo> buildables = new List<BuildableInfo>();
            foreach (Thing thing in things) {
                buildables.Add(new BuildableInfo(thing, origin));
            }

            foreach (Pair<TerrainDef, IntVec3> terrain in terrains) {
                buildables.Add(new BuildableInfo(terrain.First, terrain.Second, origin));
            }

            // try to get a decent default name: get rooms for occupied cells, then see if there is only one type.
            IEnumerable<IGrouping<TaggedString, Room>> rooms = allCells.Select(c => c.GetRoom(map))
                                .Where(r => r != null && r.Role != RoomRoleDefOf.None)
                                .Distinct()
                                .GroupBy(r => r.Role.LabelCap);

#if DEBUG
            foreach (IGrouping<TaggedString, Room> room in rooms) {
                Blueprints.Debug.Message($"{room.Count()}x {room.Key}");
            }
#endif

            // only one type of room
            string defaultName = null;
            if (rooms.Count() == 1) {
                IGrouping<TaggedString, Room> room = rooms.First();
                defaultName = room.Count() > 1
                    ? "Fluffy.Blueprints.Plural".Translate(room.Key)
                    : room.Key;
            }

            // add to controller - controller handles adding to designations
            Blueprint blueprint = new Blueprint(buildables, size, defaultName);
            BlueprintController.Add(blueprint);

            blueprint.Debug();
        }

        public static void Create(IEnumerable<Thing> things, bool temporary = false) {
            // get edges of blueprint area
            // (might be bigger than selected region, but never smaller).
            IEnumerable<IntVec3> cells = things.SelectMany(thing => thing.OccupiedRect().Cells);

            int left   = cells.Min(cell => cell.x);
            int top    = cells.Max(cell => cell.z);
            int right  = cells.Max(cell => cell.x);
            int bottom = cells.Min(cell => cell.z);

            // total size ( +1 because x = 2 ... x = 4 => 4 - 2 + 1 cells )
            IntVec2 size = new IntVec2(right - left + 1, top - bottom + 1);

            // fetch origin for default (North) orientation
            IntVec3 origin = Resources.CenterPosition(new IntVec3(left, 0, bottom), size, Rot4.North);

            // create list of buildables
            List<BuildableInfo> buildables = new List<BuildableInfo>();
            foreach (Thing thing in things) {
                buildables.Add(new BuildableInfo(thing, origin));
            }

            // add to controller - controller handles adding to designations
            Blueprint blueprint = new Blueprint(buildables, size, "Selection", temporary);
            if (temporary) {
                Find.DesignatorManager.Select(new Designator_Blueprint(blueprint));
            } else {
                BlueprintController.Add(blueprint);
            }

            blueprint.Debug();
        }

        private List<ThingDefCount> CreateCostList() {
            // set up a temporary dictionary to make adding costs easier
            Dictionary<ThingDef, int> costdict = new Dictionary<ThingDef, int>();

            // loop over all buildables
            foreach (BuildableInfo item in AvailableContents) {
                foreach (ThingDefCountClass cost in item.BuildableDef.CostListAdjusted(item.Stuff, false)) {
                    if (cost?.thingDef == null) {
                        continue;
                    }

                    // add up all construction costs
                    if (costdict.ContainsKey(cost.thingDef)) {
                        costdict[cost.thingDef] += cost.count;
                    } else {
                        costdict.Add(cost.thingDef, cost.count);
                    }
                }
            }

            // return a list of thingcounts, in descending cost order.
            return costdict.Select(pair => new ThingDefCount(pair.Key, pair.Value))
                           .OrderByDescending(tc => tc.Count).ToList();
        }

        [Conditional("DEBUG")]
        public void Debug() {
            Log.Message(_size.ToString());

            foreach (BuildableInfo thing in contents) {
                Log.Message(thing.ToString());
                Log.Message($"Available: {AvailableContents.Contains(thing)}");
            }
        }

        public void DrawGhost(IntVec3 origin) {
            foreach (BuildableInfo item in AvailableContents) {
                item.DrawGhost(origin);
            }
        }

        public void DrawStuffMenu(BuildableDef buildable) {
            if (!(buildable is ThingDef thing) || thing.costStuffCount <= 0 || thing.stuffCategories.NullOrEmpty()) {
                return;
            }

            IEnumerable<ThingDef> stuffOptions = DefDatabase<ThingDef>.AllDefsListForReading.Where(def => def.IsStuff &&
                thing.stuffCategories.Any(
                    cat =>
                        def.stuffProps
                           .categories
                           .Contains(
                                cat)));

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (ThingDef stuff in stuffOptions) {
                options.Add(new FloatMenuOption(
                                stuff.LabelCap + " (" + Find.CurrentMap.resourceCounter.GetCount(stuff) + ")",
                                delegate { SetStuffFor(buildable, stuff); }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        public void Flip() {
            ClearCaches();
            foreach (BuildableInfo item in contents.Concat(_unresolvedParts)) {
                FailReason success = item.Flip();
                if (!success && !_failReasonsMentioned.Contains(success)) {
                    Messages.Message(success.reason, MessageTypeDefOf.RejectInput, false);
                    _failReasonsMentioned.Add(success);
                }
            }
        }

        public static AcceptanceReport IsValidBlueprintName(string name) {
            if (!CouldBeValidBlueprintName(name)) {
                return new AcceptanceReport("Fluffy.Blueprints.InvalidBlueprintName".Translate(name));
            }

            // TODO: figure out why this doesn't work
            if (BlueprintController.FindBlueprint(name) != null) {
                return new AcceptanceReport("Fluffy.Blueprints.NameAlreadyTaken".Translate(name));
            }

            return true;
        }

        // called when the parent designator is selected, this resets the buildables cache
        // so that newly unlocked research can be properly applied.
        public void RecacheBuildables() {
            ClearCaches();
        }

        private void ClearCaches() {
            _availableContents = null;
            _foundationParts = null;
            _buildables = null;
            _groupedBuildables = null;
            _costlist = null;
        }

        public void Rotate(RotationDirection direction) {
            _size = _size.Rotated();
            ClearCaches();
            foreach (BuildableInfo item in contents.Concat(_unresolvedParts)) {
                FailReason success = item.Rotate(direction);
                if (!success && !_failReasonsMentioned.Contains(success)) {
                    Messages.Message(success.reason, MessageTypeDefOf.RejectInput, false);
                    _failReasonsMentioned.Add(success);
                }
            }
        }

        private void SetStuffFor(BuildableDef buildableDef, ThingDef stuff) {
            // get all buildables of this type
            IEnumerable<BuildableInfo> buildables = contents.Where(bi => bi.BuildableDef == buildableDef);

            // set them to use the new stuff def
            foreach (BuildableInfo buildable in buildables) {
                buildable.Stuff = stuff;
            }

            // reset caches
            RecacheBuildables();
        }

        protected internal bool ShouldLinkWith(IntVec3 position, ThingDef thingDef) {
            // get things at neighbouring position
            IEnumerable<ThingDef> thingsAtPosition = AvailableContents
                                  .Where(item => item.Position == position && item.BuildableDef is ThingDef)
                                  .Select(item => item.BuildableDef as ThingDef);

            // if there's nothing there, there's nothing to link with
            if (!thingsAtPosition.Any()) {
                return false;
            }

            // loop over things to see if any of the things at the neighbouring location share a linkFlag with the thingDef we're looking at
            foreach (ThingDef thing in thingsAtPosition) {
                if ((thing.graphicData.linkFlags & thingDef.graphicData.linkFlags) != LinkFlags.None) {
                    return true;
                }
            }

            // nothing stuck, return false
            return false;
        }
    }
}
