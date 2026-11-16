# Changelog

"Blueprints (Continued)" is a modified version of "Blueprints" v4.0.53 (fluffy-mods/Blueprints, commit dd90d34) by Fluffy, under the MIT license for the code and CC-BY-SA 4.0 for the content. This file lists every change from v4.0.53, with its date. Original copyright and license: see LICENSE and MODIFICATIONS.

The continuation was built only from Fluffy's own GitHub source. No code or files from any later Blueprints build were used.

Add each new change under "Unreleased" with its date as `- YYYY-MM-DD: what changed`. At release, rename the heading to the version and release date.

## 4.1.0 (2026-11-16)

### Identity and license

- 2026-11-16: renamed to "Blueprints (Continued)", packageId `rimhunter.blueprints`, version 4.1.0 (continuing v4.0.53), author "Fluffy, rimhunter", supported versions 1.6 and 1.7, Harmony as a dependency, and `<url>` pointing at the source repository (https://github.com/rimhunter/blueprints).
- 2026-11-16: the mod list now warns when Fluffy's Blueprints (`Fluffy.Blueprints`) or a later Blueprints build (`Fluffy.Blueprints.fork`, `defi.blueprints.fork`, `dw.defi.fluffy.blueprints.fork`) is also enabled.
- 2026-11-16: added MODIFICATIONS ("Modifications Copyright 2026 rimhunter, under the same MIT and CC-BY-SA 4.0 terms"), this CHANGELOG and WORKSHOP.md. LICENSE is unchanged from v4.0.53.
- 2026-11-16: the preview image (`About/Preview.png`, 1024x576) is rimhunter's own, a native 16:9 crop of a 3840x2160 in-game render of a built room beside its blueprint ghost (scenario `screenshot-preview-image-source`, then `scripts/make-preview`). Fluffy's preview image is not shipped, because it was licensed BY-NC by its artist (see Readme.md); its source `Source/preview.svg` was removed with it. `Source/Blueprint.psd`, the source of the shipped icons, is Fluffy's CC-BY-SA work and stays.
- 2026-11-16: removed `modinfo.json`, Fluffy's tool configuration, which holds a personal email address and is not part of the mod.
- 2026-11-16: `Source/` is included in the packaged mod folder, together with LICENSE, MODIFICATIONS and this CHANGELOG. The test runner and tests are not. Readme.md stays in the repository with Fluffy's text unchanged.
- 2026-11-16: added a note at the top of Readme.md, marked as a rimhunter modification: this is the continuation, the BY-NC credit refers to Fluffy's original preview, which is not included, and issues go to https://github.com/rimhunter/blueprints. Fluffy's text below it is unchanged.
- 2026-11-16: added `scripts/test-identity`, which checks the identity, license, Fluffy's copyright lines in `Source/`, translations, preview image (16:9, under 1 MB), package contents and Workshop page text.
- 2026-11-16: added scenarios that prove the mod list warns when Fluffy's Blueprints or any later build is enabled together with this mod (test only, with stand-in mods that carry only a package id), and the scenario that renders the preview image.

### Translations

- 2026-11-16: every language folder from v4.0.53 is kept with every key. The only new text is the label of the new copy key (`Blueprint_Copy`), which is English in its def and has a Japanese translation (`Languages/Japanese/DefInjected/KeyBindingDef/KeyBinding_Copy.xml`); the other languages show the English label.
- 2026-11-16: the Brazilian Portuguese translation (`strings.xml`, outside `Keyed/`) never loaded; it is now `Languages/PortugueseBrazilian/Keyed/Translations.xml` with its keys unchanged.
- 2026-11-16: removed the Japanese `DefInjected/HugsLib.UpdateFeatureDef` file, which names a type that exists only with HugsLib and made the game record a language load error without it.

### Settings and keys

- 2026-11-16: removed the mod settings page (`Source/Settings.cs` deleted). A copy key saved in the settings of v4.0.53 or earlier is ignored. The copy key is now set in the game's own Keyboard settings, as the binding "copy selection as blueprint (hold Ctrl)" (`Blueprint_Copy`) (category SelectionMisc). "flip blueprint" (`Blueprint_Flip`) is unchanged.
- 2026-11-16: the keys `Fluffy.Blueprints` (the settings page title) and `Fluffy.Blueprints.CopyKey` (the settings label) are no longer used. They stay in the language files unchanged.
- 2026-11-16: renamed the Harmony id from `fluffy.blueprints` to `rimhunter.blueprints`.
- 2026-11-16: renamed `Defs/KeyBindingDefs/KeyBinding_Flip.xml` to `KeyBinding_Blueprints.xml`; it holds `Blueprint_Flip` unchanged and the new `Blueprint_Copy`.
- 2026-11-16: the blueprint designator no longer overrides `DraggableDimensions` (it was 0, the default); the create designator uses `DrawStyleCategory` instead of `DraggableDimensions`, as 1.6 and 1.7 require.

### Placing blueprints

- 2026-11-16: a blueprint's button now has a description (the blueprint's name, which needs no new text), so a blueprint with a long item list no longer throws an error in the info box, and right-clicking it shows its menu again.
- 2026-11-16: things that cannot rotate (lamps, plant pots, tables) are built facing north when a blueprint is rotated or flipped, and even-sized ones of them (a 2x2 table) stay in the cell they were saved in. Blueprints that v4.0.53 saved with turned lamps now place them facing north.
- 2026-11-16: placing a blueprint leaves out a floor only where a wall, or another thing that cannot share its cell with a floor, covers it. Other floors that cannot be built are reported in the message that tells how many parts could not be placed (new key `Fluffy.Blueprints.CouldNotPlaceParts`, English only, in `Languages/English/Keyed/Blueprints_Placement.xml`).
- 2026-11-16: a shift-click makes plans in the 1.7 plan system, so vanilla plan removal removes them, and it skips parts that are already placed or built.
- 2026-11-16: `DesignationCategoryDef.ResolveDesignators` is patched (`Patch_DesignationCategoryDef_ResolveDesignators`): when the game rebuilds the Blueprints category, which mods can trigger at any time, the tab is filled with the create tool and the player's blueprints again instead of being left empty while the blueprints still exist.
- 2026-11-16: `ResearchManager.FinishProject` is patched (`Patch_ResearchManager_FinishProject`): when research finishes, every blueprint refreshes which of its parts are available, so a part that the research unlocks is placed and counted without opening the blueprint again.

### Modded buildings

- 2026-11-16: the ghost of a blueprint draws every part when a mod gives a building link flags but a plain graphic (windows, over-wall coolers). It used to throw `InvalidCastException` in `BuildableInfo.DrawGhost` and stopped drawing there (Fluffy's issues #26, #27, #28 and #34).
- 2026-11-16: scenarios prove it with a test-only building, and prove that a room of Dubs Bad Hygiene buildings is captured and placed, also rotated, with no error (test only).

### Odyssey

- 2026-11-16: with Odyssey, a blueprint made over a gravship now holds the substructure under every cell, also under a floor laid on it (the game keeps a foundation and the floor on it as two layers, and v4.0.53 stored only the top one). The same holds for bridges.
- 2026-11-16: parts that stand on a foundation, such as gravship hulls, consoles and tanks on substructure, wait until the foundation is built: the first placement puts down the substructure orders and tells the player how many parts wait (new key `Fluffy.Blueprints.WaitsForFoundation`, English only); placing the blueprint again once the substructure is built places them, in their cells and rotation. A floor that belongs on a foundation is not placed on the bare ground beside its order. Parts the game's own rules refuse, such as substructure out of reach of a grav engine, are left out and reported with the existing message, never as an error.
- 2026-11-16: fixed an error when a blueprint was placed across the top or bottom edge of the map: the parts off the map are now counted as not placeable and the rest is handled as usual.
- 2026-11-16: a part now waits for a foundation only where a foundation order exists at its cell or is put down by the same placement (foundations are designated first). A floor laid on a bridge is placed on land when the bridge cannot be, and a floor no longer waits for substructure the player cannot build yet (grav engine not inspected).
- 2026-11-16: the ghost of a blueprint is drawn faster: the check for parts standing on a foundation no longer scans every part for every part.
- 2026-11-16: the message that parts wait for a foundation says "1 part" when it is one part.
- 2026-11-16: placing a blueprint again over a finished foundation with a floor on it no longer reports the foundation as left out.

### Export and import

- 2026-11-16: a blueprint that is imported from the Blueprints folder now counts as exported, so its menu offers "Remove blueprint and delete exported blueprint" (before, it offered "Export blueprint" again). Its name is the file's name, so deleting the export deletes the file that was imported.
- 2026-11-16: a blueprint that names a building, floor or material this game does not have loads with the rest of its parts, in an imported file and in a saved game alike. The game's own red "Could not load reference" errors and "No thingDef or terrainDef set" no longer appear; the player gets one message that lists what was left out (new keys `Fluffy.Blueprints.PartsLeftOut`, `PartLeftOut` for exactly one part, `MissingBuilding`, `MissingFloor`, `MissingStuff`; shown in English until translated). A part whose material is missing is left out, and is no longer built from a default material.
- 2026-11-16: a part that cannot be read (a position or rotation that is not in Fluffy's format) is left out and named in the same message (new keys `UnreadableNoName`, `UnreadablePosition`, `UnreadableRotation`) instead of being placed at a wrong cell. A file that is not readable XML, has no `Blueprint` node or has no list of parts shows a message (new keys `FileUnreadable`, `NoBlueprintNode`, `NoPartsListInFile`) and is not imported; before, a file of the wrong shape imported as an empty blueprint, and an unreadable file also logged errors. Only XML and file errors are caught, every other error is raised. A saved blueprint with no list of parts is listed empty with a message (new key `NoPartsList`).
- 2026-11-16: parts that name something this game does not have are no longer lost. They stay on the blueprint as the text the file held (name, material, position, rotation) and are written back as ordinary parts in every save, rename and export, so the file stays in Fluffy's format and the parts come back, and are placed, when the mod returns.
- 2026-11-16: rotating or flipping a blueprint now turns the parts kept for a mod that is gone with the rest, in the saved game and in every export, so they come back in the right cells and facing when the mod does. A kept part whose position or rotation could not be read stays as it is. The flip of a kept part of an even-sized building cannot correct its offset, because the building is not known while its mod is gone.
- 2026-11-16: the file format is Fluffy's v4.0.53 format, unchanged, and the world component keeps the class name `Blueprints.BlueprintController`, so exports and saves made with his build still load. Scenarios prove it with a hand-written file in his format, a save whose blueprint list is written in his format, and a file from a later build with a longer name and extra nodes (test only).

### Build

- 2026-11-16: builds for RimWorld 1.6 and 1.7 from Fluffy's v4.0.53 source, one DLL per game version in per-version load folders, compiled against Krafs.Rimworld.Ref and Lib.Harmony 2.4.3.
- 2026-11-16: removed `Source/Blueprints.sln` (the project builds through `Build.proj`) and replaced `.gitignore`; the test runner is a git submodule (`.gitmodules`) and `LoadFolders.xml` loads the per-version folders.
- 2026-11-16: removed the shipped binaries (`Assemblies/0Harmony.dll`, `Assemblies/BetterKeybinding.dll`, `Assemblies/Blueprints.dll`), the HugsLib news file and Fluffy's Workshop id and dependency files.
- 2026-11-16: the mod has three Harmony patches, all applied once from the mod constructor instead of on every world load. The copy gizmo patch now targets `GizmoGridDrawer.DrawGizmoGridFor` (the class it patched, `InspectGizmoGrid`, no longer exists) and fails loudly when the injection point is not found. The two new patches are listed under "Placing blueprints".
- 2026-11-16: replaced the unlicensed BetterKeybinding library with vanilla key binding defs: the copy key is `Blueprint_Copy` (Ctrl held plus the bound key, default C); `Blueprint_Flip` is unchanged. Ctrl+Shift+C and Ctrl+Alt+C no longer copy.
- 2026-11-16: adapted to `FinalizeInit(bool)` and `DrawStyleCategory` in 1.6 and 1.7.
- 2026-11-16: added the shared scenario runner (test only, never shipped), startup, key binding and patch scenarios, unit tests, a packaging check and CI.
