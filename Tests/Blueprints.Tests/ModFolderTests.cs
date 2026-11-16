using System.Xml.Linq;
using Xunit;

namespace Blueprints.Tests;

public class ModFolderTests
{
    static readonly string Root = FindRoot();

    static string FindRoot() {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "rimhunter-runner.json")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("rimhunter-runner.json not found above the test binary");
    }

    static XElement Load(string relative) => XDocument.Load(Path.Combine(Root, relative)).Root;

    static XElement KeyBinding(string defName) =>
        Load("Defs/KeyBindingDefs/KeyBinding_Blueprints.xml").Elements("KeyBindingDef")
            .Single(d => d.Element("defName")?.Value == defName);

    [Fact]
    public void About_declares_harmony_as_a_dependency_loaded_before_the_mod() {
        var about = Load("About/About.xml");
        Assert.Equal(new[] { "brrainz.harmony" }, about.Element("modDependencies").Elements("li").Select(l => l.Element("packageId").Value));
        Assert.Equal(new[] { "brrainz.harmony" }, about.Element("loadAfter").Elements("li").Select(l => l.Value));
    }

    [Fact]
    public void About_supports_exactly_1_6_and_1_7() {
        var about = Load("About/About.xml");
        Assert.Equal(new[] { "1.6", "1.7" }, about.Element("supportedVersions").Elements("li").Select(l => l.Value));
    }

    [Fact]
    public void Load_folders_pick_the_matching_build_for_each_game_version() {
        var folders = Load("LoadFolders.xml");
        Assert.Equal(new[] { "/", "1.6" }, folders.Element("v1.6").Elements("li").Select(l => l.Value));
        Assert.Equal(new[] { "/", "1.7" }, folders.Element("v1.7").Elements("li").Select(l => l.Value));
    }

    [Fact]
    public void Copy_key_is_a_vanilla_key_binding_that_defaults_to_C() {
        var def = KeyBinding("Blueprint_Copy");
        Assert.Equal("C", def.Element("defaultKeyCodeA").Value);
        Assert.Equal("SelectionMisc", def.Element("category").Value);
    }

    [Fact]
    public void Flip_key_is_a_vanilla_key_binding_that_defaults_to_F() {
        Assert.Equal("F", KeyBinding("Blueprint_Flip").Element("defaultKeyCodeA").Value);
    }

    [Fact]
    public void No_prebuilt_binary_is_committed() {
        Assert.Empty(Directory.GetFiles(Root, "*.dll", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}Assemblies{Path.DirectorySeparatorChar}")));
        Assert.False(Directory.Exists(Path.Combine(Root, "Assemblies")));
    }

    [Fact]
    public void Nothing_references_BetterKeybinding() {
        var offenders = Directory.GetFiles(Path.Combine(Root, "Source"), "*", SearchOption.AllDirectories)
            .Where(p => p.EndsWith(".cs") || p.EndsWith(".csproj"))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(p => File.ReadAllText(p).Contains("BetterKeybinding"))
            .ToList();
        Assert.Empty(offenders);
    }

    static string LanguageValue(string dir, string element) =>
        Directory.GetFiles(dir, "*.xml", SearchOption.AllDirectories)
            .Select(f => XDocument.Load(f).Root.Element(element)?.Value).SingleOrDefault(v => v != null);

    [Fact]
    public void Every_language_that_translated_the_copy_key_labels_the_copy_key_binding() {
        var languages = Path.Combine(Root, "Languages");
        var english = LanguageValue(Path.Combine(languages, "English"), "Fluffy.Blueprints.CopyKey");
        var translated = new List<string>();
        foreach (var dir in Directory.GetDirectories(languages)) {
            var copyKey = LanguageValue(dir, "Fluffy.Blueprints.CopyKey");
            if (copyKey == null || copyKey == english) continue;
            var label = LanguageValue(dir, "Blueprint_Copy.label");
            Assert.True(label == copyKey, Path.GetFileName(dir) + " translated the copy key as \"" + copyKey + "\" but its Blueprint_Copy.label is \"" + label + "\"");
            translated.Add(Path.GetFileName(dir));
        }
        Assert.Contains("Japanese", translated);
    }

    [Fact]
    public void Fluffys_export_fixture_names_his_blueprints_mod_by_its_steam_id() {
        var meta = Load("TestRunner/Fixtures/exports/Fluffy_Room.xml").Element("meta");
        var ids = meta.Element("modIds").Elements("li").Select(l => l.Value).ToList();
        var steam = meta.Element("modSteamIds").Elements("li").Select(l => l.Value).ToList();
        Assert.Equal("708455313", steam[ids.IndexOf("fluffy.blueprints")]);
    }
}
