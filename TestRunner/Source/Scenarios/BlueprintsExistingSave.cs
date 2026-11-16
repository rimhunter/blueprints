using System.Linq;
using RimWorld;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class BlueprintsExistingSave
    {
        [Scenario("blueprints-work-in-a-save-made-without-the-mod", MapSize = 80, SaveWithMods = "")]
        static void WorksInASaveMadeWithoutTheMod(ScenarioContext s) {
            Bp.Begin(s);
            try {
                var savedWith = ScribeMetaHeaderUtility.loadedModIdsList ?? throw new ScenarioFailure("the loaded save has no list of the mods it was made with");
                s.Check(!savedWith.Contains("rimhunter.blueprints"), "the save should have been made without the mod; it lists: " + string.Join(", ", savedWith));
                s.Check(ModsConfig.IsActive("rimhunter.blueprints"), "the mod should be on in the launch that loads the save");
                s.AreEqual(0, Bp.Saved.Count, "blueprints in the menu of a save made without the mod");
                s.AreEqual(1, Bp.Tab.Count(d => d.GetType().Name == "Designator_CreateBlueprint"), "create tools in the Blueprints menu of that save");
                Bp.DrawArchitectTab();

                var parts = Bp.Room();
                var source = s.Center + Bp.Source;
                parts = Bp.BuildRoom(s, parts, source);
                var created = Bp.Create(s, Bp.DragRect(source, 3, 2), "Late");
                var dest = s.Center + Bp.Dest;
                Bp.Place(created, dest);
                Bp.CheckLayout(s, Bp.ExpectedAfter(parts, dest, 0), "placed in the save made without the mod");
                Bp.DrawArchitectTab();
                s.RequireNoLogErrors();
            }
            finally { Bp.End(); }
        }
    }
}
