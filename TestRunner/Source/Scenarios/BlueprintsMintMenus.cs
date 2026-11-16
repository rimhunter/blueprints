using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class BlueprintsMintMenus
    {
        const string MintWindowType = "DubsMintMenus.MainTabWindow_MintArchitect";
        const string GizmoFailure = "GizmoOnGUI failed";
        const float ListWidth = 200f;

        static readonly List<string> warnings = new List<string>();

        static void OnLog(string condition, string stackTrace, LogType type) {
            if (type == LogType.Warning) lock (warnings) warnings.Add(condition);
        }

        static MainButtonDef MintButton => DefDatabase<MainButtonDef>.GetNamed("MintMenus");

        static Window MintWindow => Find.WindowStack.Windows.FirstOrDefault(w => w.GetType().FullName == MintWindowType);

        static void OpenMint() {
            if (MintWindow == null) MintButton.Worker.InterfaceTryActivate();
            if (MintWindow == null) throw new ScenarioFailure("opening the Mint architect menu the way its hotkey does did not open its window");
        }

        sealed class Screen
        {
            public List<DrawnText> Texts = new List<DrawnText>();

            public IEnumerator Look() {
                UiRecorder.Begin();
                yield return null;
                Texts = UiRecorder.End();
            }
        }

        static string Dump(IEnumerable<DrawnText> texts) => string.Join(" | ", texts.Select(t => t.ToString()));

        static bool InList(Window window, DrawnText t) => t.Ink.center.x / Prefs.UIScale < window.windowRect.x + ListWidth;

        static DrawnText Find2(IEnumerable<DrawnText> texts, string text, Func<DrawnText, bool> where = null) =>
            texts.FirstOrDefault(t => t.Text == text && (where == null || where(t)));

        // Command.GizmoOnGUIInt draws the label over the bottom of the 75 px button: its rect runs from butRect.yMax - height + 12 to butRect.yMax + 12.
        static DrawnText IconAbove(DrawnText label) {
            var scale = Prefs.UIScale;
            var icon = new Rect(label.Box.x, label.Box.yMax - 12f * scale - 75f * scale, label.Box.width, 75f * scale);
            return new DrawnText { Text = label.Text, Box = icon, Ink = icon };
        }

        static IEnumerator Press(ScenarioContext s, DrawnText target, Action direct) {
#if RW_1_6
            direct();
            yield break;
#else
            yield return s.Click(target);
#endif
        }

        static IEnumerator PressDesignator(ScenarioContext s, Window window, DrawnText target, Designator des) {
#if RW_1_6
            if (des.Disabled) throw new ScenarioFailure("the Mint menu would refuse a click on " + des.LabelCap + ": " + des.disabledReason);
            var clicked = AccessTools.Method(window.GetType(), "Clicked") ?? throw new ScenarioFailure("the Mint architect window has no Clicked method");
            clicked.Invoke(window, new object[] { des, Event.current });
            yield break;
#else
            yield return s.Click(target);
#endif
        }

        static void SelectCategory(Window window) {
            var panels = (List<ArchitectCategoryTab>)AccessTools.Property(window.GetType(), "CacheDesPanels").GetValue(window);
            var tab = panels.FirstOrDefault(p => p.def == Bp.Category) ?? throw new ScenarioFailure("the Mint architect menu has no " + Bp.CategoryName + " panel");
            AccessTools.Field(window.GetType(), "SelectedTab").SetValue(window, tab);
        }

        static IEnumerator OpenBlueprintsCategory(ScenarioContext s, Screen screen, string when) {
            OpenMint();
            yield return null;
            yield return screen.Look();
            var window = MintWindow;
            var name = Bp.Category.LabelCap.Resolve();
            var row = Find2(screen.Texts, name, t => InList(window, t));
            if (row == null) {
                s.Fail(when + ": the Mint architect menu should list the " + name + " category in its category list; it drew: " + Dump(screen.Texts));
                yield break;
            }
            yield return Press(s, row, () => SelectCategory(window));
            yield return screen.Look();
        }

        static void CheckGrid(ScenarioContext s, Window window, List<DrawnText> drawn, string when) {
            var name = Bp.Category.LabelCap.Resolve();
            s.Check(Find2(drawn, name, t => !InList(window, t)) != null, when + ": the Mint panel should be headed " + name + "; it drew: " + Dump(drawn.Where(t => !InList(window, t))));
            var tool = Bp.CreateTool.LabelCap;
            s.Check(Find2(drawn, tool, t => !InList(window, t)) != null, when + ": the Mint panel should show the create tool '" + tool + "'; it drew: " + Dump(drawn.Where(t => !InList(window, t))));
            var foreign = DefDatabase<DesignationCategoryDef>.AllDefs.Where(c => c != Bp.Category).SelectMany(c => c.ResolvedAllowedDesignators).Select(d => d.LabelCap).Where(l => l != tool && !Bp.Saved.Any(b => b.LabelCap == l)).Distinct().ToList();
            var leaked = drawn.Where(t => !InList(window, t) && foreign.Contains(t.Text)).Select(t => t.Text).Distinct().ToList();
            s.Check(leaked.Count == 0, when + ": the Mint panel should show only the " + name + " tools, but it also drew tools of other categories: " + string.Join(", ", leaked));
        }

        static void RequireNoToolFailure(ScenarioContext s, string when) {
            string[] failures;
            lock (warnings) failures = warnings.Where(w => w.Contains(GizmoFailure)).ToArray();
            var labels = Bp.Tab.Select(d => d.LabelCap).ToList();
            var ours = failures.Where(w => labels.Any(l => w.Contains(" " + l + " " + GizmoFailure))).ToList();
            s.Check(ours.Count == 0, when + ": Mint logged " + ours.Count + " failed Blueprints tool(s): " + string.Join(" || ", ours.Select(f => f.Length > 200 ? f.Substring(0, 200) : f)));
        }

        static void WatchWarnings() {
            lock (warnings) warnings.Clear();
            Application.logMessageReceivedThreaded += OnLog;
        }

        static List<Part> BuildSourceRoom(ScenarioContext s, bool withFloor) {
            var parts = new List<Part> {
                Part.Thing("Table2x2c", 0, 0, "WoodLog"),
                Part.Thing("TorchLamp", -2, 0),
                Part.Thing("PlantPot", 2, 1, "WoodLog"),
            };
            if (withFloor) parts.Add(Part.Floor("WoodPlankFloor", -2, 2));
            return Bp.BuildRoom(s, parts, s.Center + Bp.Source, 3);
        }

        [Scenario("blueprints-category-opens-inside-dubs-mint-menus-and-makes-and-places-a-blueprint", MapSize = 80, Windowed = true, MaxSeconds = 150, Mods = "1446523594")]
        static IEnumerator CategoryInsideMintMenus(ScenarioContext s) {
            Bp.RequireMint();
            Bp.Begin(s);
            WatchWarnings();
            var screen = new Screen();
            try {
                var source = s.Center + Bp.Source;
                var parts = BuildSourceRoom(s, true);
                yield return null;
                yield return OpenBlueprintsCategory(s, screen, "before any blueprint exists");
                var window = MintWindow;
                if (window == null) yield break;
                CheckGrid(s, window, screen.Texts, "before any blueprint exists");
                var create = Find2(screen.Texts, Bp.CreateTool.LabelCap, t => !InList(window, t));
                if (create == null) yield break;
                yield return PressDesignator(s, window, IconAbove(create), Bp.CreateTool);
                s.Check(Find.DesignatorManager.SelectedDesignator == Bp.CreateTool, "clicking the create tool drawn in the Mint menu should select it; selected: " + (Find.DesignatorManager.SelectedDesignator?.LabelCap ?? "nothing"));
                Bp.Create(s, Bp.DragRect(source, 3, 3), "FromMint");
                Find.DesignatorManager.Deselect();
                Bp.ClearWindows();
                yield return null;
                yield return OpenBlueprintsCategory(s, screen, "after FromMint exists");
                window = MintWindow;
                if (window == null) yield break;
                CheckGrid(s, window, screen.Texts, "after FromMint exists");
                var icon = Find2(screen.Texts, "FromMint", t => !InList(window, t));
                s.Check(icon != null, "the Mint panel should show the blueprint FromMint; it drew: " + Dump(screen.Texts.Where(t => !InList(window, t))));
                if (icon == null) yield break;
                yield return PressDesignator(s, window, IconAbove(icon), Bp.SavedNamed("FromMint"));
                var chosen = Find.DesignatorManager.SelectedDesignator;
                s.Check(chosen != null && chosen == Bp.SavedNamed("FromMint"), "clicking the FromMint icon drawn in the Mint menu should select that blueprint; selected: " + (chosen?.LabelCap ?? "nothing"));
                if (chosen == null) yield break;
                var dest = s.Center + Bp.Dest;
                Bp.Place(chosen, dest, select: false);
                Bp.CheckLayout(s, Bp.ExpectedAfter(parts, dest, 0), "placed from the Mint architect menu");
                yield return screen.Look();
                s.RequireNoLogErrors();
            }
            finally {
                Application.logMessageReceivedThreaded -= OnLog;
                RequireNoToolFailure(s, "drawing the Blueprints category in Mint");
                Bp.End();
            }
        }

        [Scenario("screenshot-blueprints-category-inside-dubs-mint-menus", MapSize = 80, Windowed = true, MaxSeconds = 150, Mods = "1446523594")]
        static IEnumerator CategoryScreenshot(ScenarioContext s) {
            Bp.RequireMint();
            Bp.Begin(s);
            WatchWarnings();
            try {
                BuildSourceRoom(s, false);
                Bp.Create(s, Bp.DragRect(s.Center + Bp.Source, 3, 3), "FromMint");
                Find.DesignatorManager.Deselect();
                yield return null;
                yield return OpenBlueprintsCategory(s, new Screen(), "before the screenshot");
                var window = MintWindow ?? throw new ScenarioFailure("the Mint architect menu closed before the screenshot");
                var image = s.Screenshot("01-blueprints-in-mint-architect-menu");
                yield return image;
                CheckGrid(s, window, image.Texts.ToList(), "in the screenshot");
                s.Check(image.ShowsText("FromMint"), "the Mint architect menu should show the blueprint FromMint; it showed: " + image.TextsMatching("From"));
                s.RequireNoLogErrors();
            }
            finally {
                Application.logMessageReceivedThreaded -= OnLog;
                RequireNoToolFailure(s, "drawing the Blueprints category in Mint");
                Bp.End();
            }
            Find.MainTabsRoot.EscapeCurrentTab(playSound: false);
            yield return null;
        }
    }
}
