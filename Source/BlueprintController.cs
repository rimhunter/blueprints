//using CommunityCoreLibrary;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Blueprints {
    public class BlueprintController: WorldComponent {
        public const   string           BlueprintSaveExtension = ".xml";
        private static string _blueprintSaveLocation;

        private List<Blueprint>  _blueprints = new List<Blueprint>();
        private List<Designator> _designators;
        private bool             _initialized;

        public BlueprintController(World world) : base(world) {
            Instance = this;
        }

        public static BlueprintController Instance { get; set; }

        public static string BlueprintSaveLocation {
            get {
                _blueprintSaveLocation ??= GetSaveLocation();

                return _blueprintSaveLocation;
            }
        }

        public override void FinalizeInit(bool fromLoad) {
            base.FinalizeInit(fromLoad);
            Initialize();
        }

        // messages posted while a game loads are cleared when the interface starts, so a loaded save's notices wait for
        // the first frame of a map.
        internal static void ShowPendingNotices() {
            if (Instance == null) {
                return;
            }

            foreach (Blueprint blueprint in Instance._blueprints) {
                blueprint.ReportAndClearLeftOut();
            }
        }

        public static void Add(Blueprint blueprint) {
            if (!Instance._initialized) {
                Initialize();
            }

            Instance._blueprints.Add(blueprint);

            Designator_Blueprint designator = new Designator_Blueprint( blueprint );
            Instance._designators.Add(designator);

            // select the new designator
            Find.DesignatorManager.Select(designator);
        }

        public static void Remove(Designator_Blueprint designator, bool removeFromDisk) {
            if (!Instance._initialized) {
                Initialize();
            }

            Instance._blueprints.Remove(designator.Blueprint);
            Instance._designators.Remove(designator);

            if (removeFromDisk) {
                DeleteXML(designator.Blueprint);
            }
        }

        public static Blueprint FindBlueprint(string name) {
            if (!Instance._initialized) {
                Initialize();
            }

            return Instance._blueprints.FirstOrDefault(blueprint => blueprint.name == name);
        }

        public static Designator_Blueprint FindDesignator(string name) {
            if (!Instance._initialized) {
                Initialize();
            }

            return Instance._designators.FirstOrDefault(designator =>
                                                    (designator as Designator_Blueprint)?.Blueprint.name == name) as
                Designator_Blueprint;
        }

        public static void Initialize() {
            if (Instance._initialized) {
                return;
            }

            // find our designation category.
            DesignationCategoryDef desCatDef = DesignationCategoryDefOf2.Blueprints;
            if (desCatDef != null) {
                Instance.FillCategory(desCatDef);

                // done!
                Instance._initialized = true;
            } else {
                throw new Exception("Blueprints designation category not found");
            }
        }

        // the game rebuilds a designation category's designators whenever it re-resolves the category (mods can trigger that
        // at any time), which would drop our blueprints from the tab while they still exist.
        internal static void Notify_CategoryResolved(DesignationCategoryDef category) {
            if (category != DesignationCategoryDefOf2.Blueprints || Instance == null || !Instance._initialized) {
                return;
            }

            Instance.FillCategory(category);
        }

        internal static void Notify_ResearchFinished() {
            if (Instance == null || !Instance._initialized) {
                return;
            }

            foreach (Blueprint blueprint in Instance._blueprints) {
                blueprint.RecacheBuildables();
            }
        }

        private void FillCategory(DesignationCategoryDef category) {
            _designators = category.AllResolvedDesignators;
            _designators.Clear();
            _designators.Add(new Designator_CreateBlueprint());

            foreach (Blueprint blueprint in _blueprints) {
                _designators.Add(new Designator_Blueprint(blueprint));
            }
        }

        public override void ExposeData() {
            base.ExposeData();
            Scribe_Collections.Look(ref _blueprints, "Blueprints");
        }

        private static void DeleteXML(Blueprint blueprint) {
            File.Delete(FullFilePath(blueprint.name));
        }

        internal static List<FileInfo> GetSavedFilesList() {
            DirectoryInfo directoryInfo = new DirectoryInfo( BlueprintSaveLocation );

            IOrderedEnumerable<FileInfo> files = from f in directoryInfo.GetFiles()
                                                 where f.Extension == BlueprintSaveExtension
                                                 orderby f.LastWriteTime descending
                                                 select f;

            return files.ToList();
        }

        private static string GetSaveLocation() {
            // Get method "FolderUnderSaveData" from GenFilePaths, which is private (NonPublic) and static.
            MethodInfo Folder = typeof( GenFilePaths ).GetMethod( "FolderUnderSaveData",
                                                           BindingFlags.NonPublic |
                                                           BindingFlags.Static ) ?? throw new Exception("Blueprints :: FolderUnderSaveData is null [reflection]");

            // Call "FolderUnderSaveData" from null parameter, since this is a static method.
            return (string) Folder.Invoke(null, new object[] { "Blueprints" });
        }

        private static string FullFilePath(string name) {
#if DEBUG
            Log.Message(Path.Combine(BlueprintSaveLocation, name + BlueprintSaveExtension));
#endif
            return Path.Combine(BlueprintSaveLocation, name + BlueprintSaveExtension);
        }

        internal static bool FileExists(string name) {
            return File.Exists(FullFilePath(name));
        }

        internal static bool TryRenameFile(Blueprint blueprint, string newName) {
            if (!FileExists(newName)) {
                RenameFile(blueprint, newName);
                return true;
            }

            return false;
        }

        private static void RenameFile(Blueprint blueprint, string newName) {
            DeleteXML(blueprint);
            blueprint.name = newName;
            SaveToXML(blueprint);
        }

        internal static Blueprint LoadFromXML(string fileName) {
            Blueprint blueprint = new Blueprint();
            string path = Path.Combine(BlueprintSaveLocation, fileName);

#if DEBUG
            Log.Message("Attempting to load from: " + fileName);
#endif

            try {
                new XmlDocument().Load(path);
            } catch (Exception e) when (e is XmlException || e is IOException) {
                ReportUnreadable(fileName, e.Message);
                return null;
            }

            try {
                Scribe.loader.InitLoading(path);
                ScribeMetaHeaderUtility.LoadGameDataHeader(ScribeMetaHeaderUtility.ScribeHeaderMode.Map, true);
                if (!Scribe.EnterNode("Blueprint")) {
                    ReportUnreadable(fileName, "Fluffy.Blueprints.NoBlueprintNode".Translate());
                    return null;
                }

                blueprint.ExposeData();
                Scribe.ExitNode();
            } finally {
                Scribe.loader.FinalizeLoading();
                Scribe.mode = LoadSaveMode.Inactive;
            }

            if (blueprint.HasNoPartsList) {
                ReportUnreadable(fileName, "Fluffy.Blueprints.NoPartsListInFile".Translate());
                return null;
            }

            blueprint.name = Path.GetFileNameWithoutExtension(fileName);
            blueprint.exported = true;
            return blueprint;
        }

        private static void ReportUnreadable(string fileName, string reason) {
            Messages.Message("Fluffy.Blueprints.FileUnreadable".Translate(fileName, reason), MessageTypeDefOf.CautionInput);
        }

        public static void SaveToXML(Blueprint blueprint) {
            try {
                try {
                    Scribe.saver.InitSaving(FullFilePath(blueprint.name), "Blueprint");
                } catch (Exception ex) {
                    GenUI.ErrorDialog("ProblemSavingFile".Translate(ex.ToString()));
                    throw;
                }

                ScribeMetaHeaderUtility.WriteMetaHeader();
                Scribe_Deep.Look(ref blueprint, "Blueprint");
            } catch (Exception ex2) {
                Log.Error("Exception while saving blueprint: " + ex2);
            } finally {
                Scribe.saver.FinalizeSaving();
            }

            // set exported flag.
            blueprint.exported = true;
        }
    }
}
