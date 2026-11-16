using System.Collections;
using RimWorld;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class IdentityPreview
    {
        static void Room(ScenarioContext s, int left, bool built) {
            for (var x = 0; x < 9; x++)
                for (var z = 0; z < 6; z++) {
                    var cell = s.Cell(left + x, z - 3);
                    var edge = x == 0 || x == 8 || z == 0 || z == 5;
                    if (edge && !(x == 4 && z == 0)) {
                        if (built) s.SpawnBuilding("Wall", cell, "BlocksGranite");
                        else s.PlaceBlueprint("Wall", cell, "BlocksGranite");
                    }
                    else if (built) s.Map.terrainGrid.SetTerrain(cell, TerrainDefOf.WoodPlankFloor);
                }
            void Furnish(string def, int x, int z, string stuff, Rot4 rot) {
                var cell = s.Cell(left + x, z - 3);
                if (built) s.SpawnBuilding(def, cell, stuff, rot);
                else s.PlaceBlueprint(def, cell, stuff, rot);
            }
            Furnish("DiningChair", 2, 2, "WoodLog", Rot4.East);
            Furnish("DiningChair", 6, 2, "WoodLog", Rot4.West);
            Furnish("Table2x2c", 4, 3, "WoodLog", Rot4.North);
            Furnish("EndTable", 1, 4, "WoodLog", Rot4.North);
            Furnish("EndTable", 7, 4, "WoodLog", Rot4.North);
        }

        [Scenario("screenshot-preview-image-source", MapSize = 40, Windowed = true, MaxSeconds = 150)]
        static IEnumerator PreviewImageSource(ScenarioContext s) {
            Room(s, -11, true);
            Room(s, 2, false);
            yield return null;
            s.LookAt(s.Cell(0, 0), 26);
            yield return null;
            var shot = s.Screenshot("preview-image-source");
            yield return shot;
        }
    }
}
