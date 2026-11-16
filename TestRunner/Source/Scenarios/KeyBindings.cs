using System.Collections;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class KeyBindings
    {
        static void SpawnAndSelectWalls(ScenarioContext s, int count) {
            Find.Selector.ClearSelection();
            for (var i = 0; i < count; i++) {
                var wall = s.SpawnBuilding("Wall", s.Cell(i, 0), "BlocksGranite");
                Find.Selector.Select(wall, playSound: false, forceDesignatorDeselect: false);
            }
        }

        static bool CopyGizmoIsOffered() {
            Event.current = new Event { type = EventType.Repaint };
            GizmoGridDrawer.DrawGizmoGridFor(Find.Selector.SelectedObjects, out _);
            var drawn = (System.Collections.Generic.List<Gizmo>)AccessTools.Field(typeof(GizmoGridDrawer), "gizmoList").GetValue(null);
            var label = "Fluffy.Blueprints.Copy".Translate().Resolve();
            return drawn.OfType<Command>().Any(c => c.Visible && c.defaultLabel == label);
        }

        static void PressCopyKey(ScenarioContext s, EventModifiers modifiers) {
            Find.DesignatorManager.Deselect();
            Event.current = new Event { type = EventType.KeyDown, keyCode = KeyCode.C, modifiers = modifiers };
            MapComponentUtility.MapComponentOnGUI(s.Map);
        }

        static bool CopiedBlueprintIsSelected() =>
            Find.DesignatorManager.SelectedDesignator is Designator d && d.Label.StartsWith("Selection_");

        [Scenario("copy-gizmo-shows-for-three-or-more-buildings", MapSize = 30, WindowedOnUnity = true)]
#if RW_1_6
        static IEnumerator CopyGizmoShows(ScenarioContext s) {
            CheckCopyGizmo(s);
            yield break;
        }
#else
        static void CopyGizmoShows(ScenarioContext s) => CheckCopyGizmo(s);
#endif

        static void CheckCopyGizmo(ScenarioContext s) {
            SpawnAndSelectWalls(s, 2);
            s.Check(!CopyGizmoIsOffered(), "the copy-as-blueprint gizmo should not show for two walls");
            SpawnAndSelectWalls(s, 3);
            s.Check(CopyGizmoIsOffered(), "the copy-as-blueprint gizmo should show among the gizmos of three selected walls");
            s.RequireNoPlayerLogErrors();
        }

        [Scenario("ctrl-c-copies-the-selection-as-a-blueprint", MapSize = 30, WindowedOnUnity = true)]
#if RW_1_6
        static IEnumerator CtrlCCopies(ScenarioContext s) {
            CheckCtrlC(s);
            yield break;
        }
#else
        static void CtrlCCopies(ScenarioContext s) => CheckCtrlC(s);
#endif

        static void CheckCtrlC(ScenarioContext s) {
            SpawnAndSelectWalls(s, 3);
            PressCopyKey(s, EventModifiers.None);
            s.Check(!CopiedBlueprintIsSelected(), "C without Ctrl should not copy the selection");
            PressCopyKey(s, EventModifiers.Control);
            s.Check(CopiedBlueprintIsSelected(), "Ctrl+C with three walls selected should create a blueprint and select it for placing");
            PressCopyKey(s, EventModifiers.Control | EventModifiers.Shift);
            s.Check(!CopiedBlueprintIsSelected(), "Ctrl+Shift+C should not copy the selection");
            PressCopyKey(s, EventModifiers.Control | EventModifiers.Alt);
            s.Check(!CopiedBlueprintIsSelected(), "Ctrl+Alt+C should not copy the selection");
            SpawnAndSelectWalls(s, 2);
            PressCopyKey(s, EventModifiers.Control);
            s.Check(!CopiedBlueprintIsSelected(), "Ctrl+C with two walls selected should not copy");
            s.RequireNoPlayerLogErrors();
        }
    }
}
