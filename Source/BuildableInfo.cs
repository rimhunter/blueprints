// BuildableInfo.cs
// Copyright Karel Kroeze, 2018-2019

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using RimWorld;
using UnityEngine;
using Verse;

namespace Blueprints {
    public enum PlacementReport {
        CanNotPlace,
        CanPlace,
        AlreadyPlaced
    }

    public class BuildableInfo: IEquatable<BuildableInfo>, IExposable {
        private readonly Dictionary<int, Material> _cachedMaterials = new Dictionary<int, Material>();

        private BuildableDef     _buildableDef;
        private Designator_Build _designator;
        private IntVec3          _position;
        private Rot4             _rotation;
        private ThingDef         _stuff;
        private TerrainDef       _terrainDef;
        private ThingDef         _thingDef;

        public Blueprint blueprint;

        public BuildableInfo(Blueprint blueprint) {
            this.blueprint = blueprint;
            // scribe
        }

        public BuildableInfo(Thing thing, IntVec3 origin) {
            if (thing is RimWorld.Blueprint blueprint) {
                Init(blueprint, origin);
            } else if (thing is Frame frame) {
                Init(frame, origin);
            } else {
                Init(thing, origin);
            }
        }

        public BuildableInfo(TerrainDef terrain, IntVec3 position, IntVec3 origin) {
            Init(terrain, position, origin);
        }

        public BuildableDef BuildableDef {
            get {
                if (_buildableDef == null) {
                    if (_thingDef != null) {
                        _buildableDef = _thingDef;
                    } else if (_terrainDef != null) {
                        _buildableDef = _terrainDef;
                    } else {
                        Log.ErrorOnce("Blueprints :: No thingDef or terrainDef set!", GetHashCode() * 123);
                    }
                }

                return _buildableDef;
            }
        }

        public Designator_Build Designator {
            get {
                _designator ??= CreateDesignatorCopy();

                return _designator;
            }
        }

        public IntVec3 Position {
            get {
                if (BuildableDef is TerrainDef) {
                    return _position;
                }

                if (Rotatable) {
                    return _position;
                }

                if (Centered || !Square) {
                    return _position;
                }

                // offset position
                return _position - Resources.Offset(_thingDef.Size, Rot4.North, _rotation);
            }
        }

        public ThingDef Stuff {
            get => _stuff;
            set {
                _stuff = value;
                Designator.SetStuffDef(value);
            }
        }

        public bool Rotatable {
            get {
                if (BuildableDef is ThingDef thingDef) {
                    return thingDef.rotatable;
                }

                return false;
            }
        }

        private Rot4 BuildRotation => Rotatable ? _rotation : Rot4.North;

        public bool Square {
            get {
                if (BuildableDef is ThingDef thingDef) {
                    return thingDef.Size.x == thingDef.Size.z;
                }

                return true;
            }
        }

        public bool Centered {
            get {
                if (BuildableDef == null) {
                    throw new InvalidOperationException("cannot get centered status without a buildable set");
                }

                if (BuildableDef is TerrainDef) {
                    return true;
                }

                // if width or height are even, the pivot cannot possibly be centered.
                if (_thingDef.Size.x % 2 == 0 || _thingDef.Size.z % 2 == 0) {
                    return false;
                }

                return true;
            }
        }

        // should only be used to compare info's in the same blueprint
        public bool Equals(BuildableInfo other) {
            return _thingDef == other._thingDef && _position == other._position;
        }

        public void ExposeData() {
            if (Scribe.mode == LoadSaveMode.LoadingVars) {
                ReadFromNode();
                return;
            }

            if (IsUnresolved) {
                Scribe_Values.Look(ref _raw.ThingDef, "ThingDef");
                Scribe_Values.Look(ref _raw.Stuff, "Stuff");
                Scribe_Values.Look(ref _raw.TerrainDef, "TerrainDef");
                Scribe_Values.Look(ref _raw.Position, "Position");
                Scribe_Values.Look(ref _raw.Rotation, "Rotation");
                return;
            }

            Scribe_Defs.Look(ref _thingDef, "ThingDef");
            Scribe_Defs.Look(ref _stuff, "Stuff");
            Scribe_Defs.Look(ref _terrainDef, "TerrainDef");
            Scribe_Values.Look(ref _position, "Position");
            Scribe_Values.Look(ref _rotation, "Rotation");
        }

        public List<string> LeftOutReasons { get; } = new List<string>();

        public bool IsUnresolved => LeftOutReasons.Count > 0;

        public bool IsFoundation => BuildableDef is TerrainDef { isFoundation: true };

        private sealed class RawPart {
            public string ThingDef;
            public string TerrainDef;
            public string Stuff;
            public string Position;
            public string Rotation;
        }

        private RawPart _raw = new RawPart();
        private bool _positionReadable = true;
        private bool _rotationReadable = true;

        private static readonly Regex PositionFormat = new Regex(@"^\(\s*-?\d+\s*,\s*-?\d+\s*,\s*-?\d+\s*\)$");

        private static string TextOf(string label) {
            XmlNode node = Scribe.loader.curXmlParent[label];
            string text = node?.InnerText?.Trim();
            return string.IsNullOrEmpty(text) || text == "null" ? null : text;
        }

        private static T FindDef<T>(string label, string name, List<string> leftOut, string missingKey) where T : Def, new() {
            if (name == null) {
                return null;
            }

            string current = BackCompatibility.BackCompatibleDefName(typeof(T), name, false, Scribe.loader.curXmlParent[label]);
            T def = DefDatabase<T>.GetNamedSilentFail(current);
            if (def == null) {
                leftOut.Add(missingKey.Translate(name).Resolve());
            }

            return def;
        }

        // reads the stored names as text and looks them up without logging, so a def this game lacks is reported to the player
        // by the blueprint's message and not as an error. The stored format is the one Scribe_Defs and Scribe_Values write.
        private void ReadFromNode() {
            string thingName = TextOf("ThingDef");
            string terrainName = TextOf("TerrainDef");
            string stuffName = TextOf("Stuff");
            string position = TextOf("Position");
            string rotation = TextOf("Rotation");
            _raw = new RawPart { ThingDef = thingName, TerrainDef = terrainName, Stuff = stuffName, Position = position, Rotation = rotation };

            _thingDef = FindDef<ThingDef>("ThingDef", thingName, LeftOutReasons, "Fluffy.Blueprints.MissingBuilding");
            _terrainDef = FindDef<TerrainDef>("TerrainDef", terrainName, LeftOutReasons, "Fluffy.Blueprints.MissingFloor");
            _stuff = FindDef<ThingDef>("Stuff", stuffName, LeftOutReasons, "Fluffy.Blueprints.MissingStuff");

            if (thingName == null && terrainName == null) {
                LeftOutReasons.Add("Fluffy.Blueprints.UnreadableNoName".Translate().Resolve());
            }

            string label = thingName ?? terrainName ?? "?";
            if (position == null) {
                _position = IntVec3.Zero;
            } else if (PositionFormat.IsMatch(position)) {
                _position = IntVec3.FromString(position);
            } else {
                _positionReadable = false;
                LeftOutReasons.Add("Fluffy.Blueprints.UnreadablePosition".Translate(label, position).Resolve());
            }

            if (rotation == null) {
                _rotation = Rot4.North;
            } else if (int.TryParse(rotation, out _) || rotation == "North" || rotation == "East" || rotation == "South" || rotation == "West") {
                _rotation = Rot4.FromString(rotation);
            } else {
                _rotationReadable = false;
                LeftOutReasons.Add("Fluffy.Blueprints.UnreadableRotation".Translate(label, rotation).Resolve());
            }
        }

        public PlacementReport CanPlace(IntVec3 origin) {
            // get rotated cell position
            IntVec3 cell = origin + Position;

            // if out of bounds, we clearly can't place it
            if (!cell.InBounds(Find.CurrentMap)) {
                return PlacementReport.CanNotPlace;
            }

            // a part that stands on a foundation with an order waits until it is built, as the game would not accept it before
            if (blueprint.WaitsForFoundation(this, origin)) {
                return PlacementReport.CanNotPlace;
            }

            // if the designator's check passes, we can safely assume it's OK to build here
            if (Designator.CanDesignateCell(cell).Accepted) {
                return PlacementReport.CanPlace;
            }

            // otherwise, check if the same thing (or it's blueprint/frame stages) already exists here
            // terrain and thing both have bluePrint and frame in thinglist, as are things. Terrains are not a thing, and retrieved with GetTerrain().
            List<ThingDef> cellDefs = cell.GetThingList( Find.CurrentMap ).Select( thing => thing.def ).ToList();
            if (cellDefs.Contains(BuildableDef as ThingDef) ||
                 cell.GetTerrain(Find.CurrentMap) == BuildableDef as TerrainDef ||
                 Find.CurrentMap.terrainGrid.FoundationAt(cell) == BuildableDef as TerrainDef && BuildableDef is TerrainDef ||
                 cellDefs.Contains(BuildableDef.blueprintDef) ||
                 cellDefs.Contains(BuildableDef.frameDef)) {
                return PlacementReport.AlreadyPlaced;
            }

            // finally, default to returning false.
            return PlacementReport.CanNotPlace;
        }

        public Designator_Build CreateDesignatorCopy() {
            // create a new copy
            Designator_Build designator = new Designator_Build( BuildableDef );

            // apply stuffdef & rotation
            if (_thingDef != null) {
                // set stuff def
                if (_stuff == null) {
                    designator.SetStuffDef(GenStuff.DefaultStuffFor(_thingDef));
                } else {
                    designator.SetStuffDef(_stuff);
                }

                // set rotation through reflection
                Resources.SetDesignatorRotation(designator, BuildRotation);
            }

            return designator;
        }

        public IEnumerable<IntVec3> OccupiedCells(IntVec3 origin) {
            if (_thingDef == null) {
                return new[] { origin + Position };
            }

            return GenAdj.OccupiedRect(origin + Position, BuildRotation, _thingDef.Size).Cells;
        }

        public void Designate(IntVec3 origin) {
            Designator.DesignateSingleCell(origin + Position);
        }

        public void DrawGhost(IntVec3 origin) {
            IntVec3 cell = origin + Position;
            if (_thingDef != null) {
                // normal thingdef graphic
                if (_thingDef.graphicData.linkFlags == LinkFlags.None || !(_thingDef.graphic is Graphic_Linked)) {
                    GhostDrawer.DrawGhostThing(cell, BuildRotation, _thingDef, null,
                                                Resources.GhostColor(CanPlace(origin)),
                                                AltitudeLayer.Blueprint);
                }

                // linked thingdef graphic
                else {
                    Color      color = Resources.GhostColor( CanPlace( origin ) );
                    int      hash  = color.GetHashCode() * _rotation.GetHashCode();
                    if (!_cachedMaterials.TryGetValue(hash, out Material material)) {
                        // get a colored version (ripped from GhostDrawer.DrawGhostThing)
                        Graphic_Linked graphic = (Graphic_Linked) _thingDef.graphic.GetColoredVersion( ShaderDatabase.Transparent,
                                                                                            color,
                                                                                            Color.white );

                        // atlas contains all possible link graphics
                        Material atlas = graphic.MatSingle;

                        // loop over cardinal directions, and set the correct bits (e.g. 1, 2, 4, 8).
                        int linkInt = 0;
                        int dirInt  = 1;
                        for (int i = 0; i < 4; i++) {
                            if (blueprint.ShouldLinkWith(Position + GenAdj.CardinalDirections[i], _thingDef)) {
                                linkInt += dirInt;
                            }

                            dirInt *= 2;
                        }

                        // translate int to bitmask (flags)
                        LinkDirections linkSet = (LinkDirections) linkInt;

                        // get and cache the final material
                        material = MaterialAtlasPool.SubMaterialFromAtlas(atlas, linkSet);
                        _cachedMaterials.Add(hash, material);
                    }

                    // draw the thing.
                    Vector3 position = cell.ToVector3ShiftedWithAltitude( AltitudeLayer.MetaOverlays );
                    Graphics.DrawMesh(MeshPool.plane10, position, Quaternion.identity, material, 0);
                }
            } else {
                Vector3 position = cell.ToVector3ShiftedWithAltitude( AltitudeLayer.MetaOverlays );
                Graphics.DrawMesh(MeshPool.plane10, position, Quaternion.identity,
                                   Resources.GhostFloorMaterial(CanPlace(origin)), 0);
            }
        }

        public bool TryGetPlanCell(IntVec3 origin, out IntVec3 cell) {
            cell = origin + Position;

            // only plan wall (or things that link with walls) designations
            if (_thingDef == null || (_thingDef.graphicData.linkFlags & LinkFlags.Wall) != LinkFlags.Wall) {
                return false;
            }

            Map map = Find.CurrentMap;
            if (!cell.InBounds(map) || cell.InNoBuildEdgeArea(map)) {
                return false;
            }

            return map.planManager.PlanAt(cell) == null;
        }

        private bool HoldsMissingThing => _thingDef == null && _terrainDef == null && _raw.ThingDef != null;

        private void WriteBackRaw() {
            if (_positionReadable && (_raw.Position != null || _position != IntVec3.Zero)) {
                _raw.Position = _position.ToString();
            }

            if (_rotationReadable && (_raw.Rotation != null || _rotation != Rot4.North)) {
                _raw.Rotation = _rotation.ToString();
            }
        }

        public FailReason Rotate(RotationDirection direction) {
            FailReason result = RotateParts(direction);
            if (IsUnresolved) {
                WriteBackRaw();
            }

            return result;
        }

        private FailReason RotateParts(RotationDirection direction) {
            // update position within blueprint
            // for a clock wise rotation
            if (direction == RotationDirection.Clockwise) {
                _position = _position.RotatedBy(Rot4.East);
            }

            // counter clock wise is the reverse
            else {
                _position = _position.RotatedBy(Rot4.West);
            }

            // a part kept for a building this game lacks turns with the rest, but its size is unknown
            if (HoldsMissingThing) {
                if (_rotationReadable && _rotation.IsValid) {
                    _rotation.Rotate(direction);
                }

                return true;
            }

            // update rotation of item
            // if there's no thingdef, there's no point.
            if (_thingDef == null) {
                return true;
            }

            // always keep track of rotation internally: the position offset of even-sized things depends on it.
            _rotation.Rotate(direction);

            // only things that can rotate are built facing the new way; lamps and plant pots keep the facing they have.
            if (!IsUnresolved) {
                Resources.SetDesignatorRotation(Designator, BuildRotation);
            }

            // if the pivot of a non-rotatable thing is not in it's center, try to offset it's position.
            if (!Rotatable && !Centered) {
                if (!Square) {
                    // throw message - don't deal.
                    return "Fluffy.Blueprints.UnRotatable.NonSquare".Translate(_thingDef.LabelCap).Resolve();
                }

                // we'll try to offset the location
                if (_thingDef.hasInteractionCell) {
                    // throw message - interaction cell might become inaccessible.
                    return "Fluffy.Blueprints.UnRotatable.HasInteractionCell".Translate(_thingDef.LabelCap).Resolve();
                }
            }

            return true;
        }

        public FailReason Flip() {
            FailReason result = FlipParts();
            if (IsUnresolved) {
                WriteBackRaw();
            }

            return result;
        }

        private FailReason FlipParts() {
            // invert x position
            _position.x = -_position.x;

            // a part kept for a building this game lacks is mirrored without the offset of an even-sized building, as its size is unknown
            if (HoldsMissingThing) {
                if (_rotationReadable && _rotation.AsInt % 2 == 1) {
                    _rotation.AsInt += 2;
                }

                return true;
            }

            if (_thingDef != null) {
                ThingDef thingDef = _thingDef;

                // fix relative offset
                if (!Centered) {
                    IntVec3 offset;
                    if (_rotation.AsInt % 2 == 0) {
                        offset = Resources.Offset(thingDef.Size, new Rot4(_rotation.AsInt + 2), _rotation);
                        _position.x += offset.x;
                    } else {
                        offset = Resources.Offset(thingDef.Size, new Rot4(_rotation.AsInt + 2), _rotation);
                        _position.z -= offset.z;
                    }

                    Debug.Message($"flip from: {_rotation}, to: {new Rot4(_rotation.AsInt + 2)}, {offset}");
                }

                // swap east/west facings, leave north/south the same.
                if (_rotation.AsInt % 2 == 1) // East/West
                {
                    _rotation.AsInt += 2;       // rotate twice.
                }

                if (!IsUnresolved) {
                    Resources.SetDesignatorRotation(Designator, BuildRotation);
                }

                if (!Rotatable && !Centered && thingDef.hasInteractionCell) {
                    return "Fluffy.Blueprints.UnFlippable.HasInteractionCell".Translate(thingDef.LabelCap).Resolve();
                }
            }

            return true;
        }

        public override string ToString() {
            return BuildableDef.LabelCap + " _pos: " + _position + ", rot: " + _rotation + ", rotPos: " + Position +
                   ", cat: " + BuildableDef.designationCategory;
        }

        private void Init(RimWorld.Blueprint blueprint, IntVec3 origin) {
            if (blueprint.def.entityDefToBuild is TerrainDef terrain) {
                Init(terrain, blueprint.Position, origin);
            } else if (blueprint is Blueprint_Build bp_build) {
                Init(blueprint.def.entityDefToBuild as ThingDef, bp_build.stuffToUse, blueprint.Position,
                      blueprint.Rotation, origin);
            } else if (blueprint is Blueprint_Install bp_install) {
                Init(blueprint.def.entityDefToBuild as ThingDef, bp_install.MiniToInstallOrBuildingToReinstall.Stuff,
                      blueprint.Position, blueprint.Rotation, origin);
            }
        }

        private void Init(Frame frame, IntVec3 origin) {
            if (frame.def.entityDefToBuild is TerrainDef terrain) {
                Init(terrain, frame.Position, origin);
            } else {
                Init(frame.def.entityDefToBuild as ThingDef, frame.Stuff, frame.Position, frame.Rotation, origin);
            }
        }

        private void Init(Thing thing, IntVec3 origin) {
            _thingDef = thing.def;
            _terrainDef = null;
            _position = thing.Position - origin;
            _rotation = thing.Rotation;
            _stuff = thing.Stuff;
        }

        private void Init(ThingDef thingdef, ThingDef stuffDef, IntVec3 position, Rot4 rotation, IntVec3 origin) {
            _thingDef = thingdef;
            _terrainDef = null;
            _position = position - origin;
            _rotation = rotation;
            _stuff = stuffDef;
        }

        private void Init(TerrainDef terrain, IntVec3 position, IntVec3 origin) {
            _thingDef = null;
            _terrainDef = terrain;
            _position = position - origin;
            _rotation = Rot4.Invalid;
            _stuff = null;
        }
    }
}
