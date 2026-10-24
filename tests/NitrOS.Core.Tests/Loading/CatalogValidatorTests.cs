using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Core.Tests.TestSupport;

namespace NitrOS.Core.Tests.Loading;

public class CatalogValidatorTests
{
    private static IReadOnlyList<CatalogError> One(SwitchDef s, string file = "test.json", string group = "test") =>
        CatalogValidator.Validate([(file, new GroupFile(group, "T", [s]))]);

    private static IReadOnlyList<CatalogError> Many(params SwitchDef[] s) =>
        CatalogValidator.Validate([("test.json", new GroupFile("test", "T", s))]);

    private static void AssertOne(IReadOnlyList<CatalogError> errors, string contains)
    {
        Assert.Single(errors);
        Assert.Contains(contains, errors[0].Message);
        Assert.Null(errors[0].Line);
    }

    [Fact]
    public void GroupName_MustMatchFileName()
    {
        var errors = One(Switches.Make("a-one"), "test.json", "other");
        AssertOne(errors, "does not match file name");
        Assert.Equal("test.json", errors[0].File);
    }

    [Fact]
    public void Id_MustBeKebabCase()
    {
        var errors = One(Switches.Make("Bad_Id"));
        AssertOne(errors, "kebab-case");
        Assert.Equal("Bad_Id", errors[0].SwitchId);
    }

    [Fact]
    public void DuplicateId_NamesBothFiles()
    {
        var errors = CatalogValidator.Validate([
            ("a.json", new GroupFile("a", "A", [Switches.Make("dup")])),
            ("b.json", new GroupFile("b", "B", [Switches.Make("dup")])),
        ]);
        Assert.Single(errors);
        Assert.Contains("a.json", errors[0].Message);
        Assert.Contains("b.json", errors[0].Message);
        Assert.Equal("dup", errors[0].SwitchId);
    }

    [Fact]
    public void Description_OneLineMax120()
    {
        AssertOne(One(Switches.Make("x") with { Description = new string('a', 121) }), "description");
        AssertOne(One(Switches.Make("x") with { Description = "two\nlines" }), "description");
        AssertOne(One(Switches.Make("x") with { Description = "" }), "description");
        Assert.Empty(One(Switches.Make("x") with { Description = new string('a', 120) }));
    }

    [Fact]
    public void Requires_MustExistAndNotSelf()
    {
        AssertOne(One(Switches.Make("x", b => b.Requires = ["ghost"])), "ghost");
        AssertOne(One(Switches.Make("x", b => b.Requires = ["x"])), "itself");
    }

    [Fact]
    public void Breaks_RequiredWhenRiskIsBreaks()
    {
        AssertOne(One(Switches.Make("x", b => b.Risk = Risk.Breaks)), "breaks");
        Assert.Empty(One(Switches.Make("x", b => { b.Risk = Risk.Breaks; b.Breaks = ["Store"]; })));
    }

    [Fact]
    public void Modes_MatchActionLists()
    {
        // listed live, no live actions
        AssertOne(One(Switches.Make("x", b => b.Live = [])), "live");
        // image actions present but image not listed
        AssertOne(One(Switches.Make("x", b => b.Image = [new FileRemove(@"C:\x")])), "image");
    }

    [Fact]
    public void PackageRemove_ImageOnly()
    {
        var s = Switches.Make("x", b =>
        {
            b.Modes = [Mode.Live, Mode.Image];
            b.Reversible = false;
            b.Live = [new PackageRemove("Foo*")];
            b.Image = [new PackageRemove("Foo*")];
        });
        var errors = One(s);
        Assert.Contains(errors, e => e.Message.Contains("package.remove"));
    }

    [Fact]
    public void Reversible_HasNoDestructiveLiveAction()
    {
        var destructive = One(Switches.Make("x", b => b.Live = [new AppxRemove("Foo")]));
        AssertOne(destructive, "reversible");

        var script = One(Switches.Make("x", b => b.Live = [new Script("echo hi")]));
        AssertOne(script, "reversible");

        Assert.Empty(One(Switches.Make("x", b => b.Live = [new Script("echo hi", "echo undo")])));
    }

    [Fact]
    public void RegistryData_ParsesForKind()
    {
        static SwitchDef Reg(string hive, RegistryKind kind, string data) =>
            Switches.Make("x", b => b.Live = [new RegistrySet(hive, @"SOFTWARE\T", "V", kind, data)]);

        Assert.Empty(One(Reg("HKLM", RegistryKind.Dword, "0x1")));
        Assert.Empty(One(Reg("HKCU", RegistryKind.Dword, "42")));
        AssertOne(One(Reg("HKLM", RegistryKind.Dword, "abc")), "data");
        AssertOne(One(Reg("HKLM", RegistryKind.Dword, "4294967296")), "data");
        AssertOne(One(Reg("HKCR", RegistryKind.Dword, "1")), "hive");
        Assert.Empty(One(Reg("HKLM", RegistryKind.Qword, "0xFFFFFFFFFFFFFFFF")));
        AssertOne(One(Reg("HKLM", RegistryKind.Qword, "18446744073709551616")), "data");
        Assert.Empty(One(Reg("HKLM", RegistryKind.Binary, "0aFF")));
        AssertOne(One(Reg("HKLM", RegistryKind.Binary, "abc")), "data");
    }

    [Fact]
    public void PowerSet_GuidsAndAValue()
    {
        const string g = "54533251-82be-4824-96c1-47b60b740d00";
        AssertOne(One(Switches.Make("x", b => b.Live = [new PowerSet("nope", g, 1)])), "power.set");
        AssertOne(One(Switches.Make("x", b => b.Live = [new PowerSet(g, g)])), "power.set");
        Assert.Empty(One(Switches.Make("x", b => b.Live = [new PowerSet(g, g, Dc: 5)])));
    }

    [Fact]
    public void ValidCatalog_HasNoErrors()
    {
        Assert.Empty(Many(Switches.Make("one"), Switches.Make("two-b"), Switches.Make("three3")));
    }

    [Fact]
    public void UndefinedEnumValues_AreErrors()
    {
        AssertOne(One(Switches.Make("x", b => b.Risk = (Risk)7)), "Risk value");
        AssertOne(One(Switches.Make("x", b => b.Modes = [Mode.Live, (Mode)9])), "Mode value");
        AssertOne(One(Switches.Make("x", b => b.SkipOn = [(DeviceTrait)9])), "skipOn");
        AssertOne(One(Switches.Make("x", b => b.Cards = new CardDefaults
        {
            Recommended = (CardDefault)5,
            Gamer = CardDefault.Off,
            Streamer = CardDefault.Off,
            MaxLean = CardDefault.Off,
        })), "Card default");
        AssertOne(One(Switches.Make("x", b => b.Live = [new ServiceStart("svc", (ServiceStartType)42)])), "Service start type");
        AssertOne(One(Switches.Make("x", b =>
            b.Live = [new RegistrySet("HKLM", @"SOFTWARE\T", "V", (RegistryKind)99, "1")])), "Registry kind");
    }

    private static SwitchDef WithAction(SwitchAction a) =>
        a is PackageRemove
            ? Switches.Make("x", b => { b.Modes = [Mode.Image]; b.Live = []; b.Image = [a]; b.Reversible = false; })
            : Switches.Make("x", b => { b.Live = [a]; b.Reversible = false; });

    public static TheoryData<string, SwitchDef> EmptyFieldCases => new()
    {
        { "name", Switches.Make("x") with { Name = "" } },
        { "name", Switches.Make("x") with { Name = "  " } },
        { "feature", Switches.Make("x") with { Feature = "" } },
        { "feature", Switches.Make("x") with { Feature = " " } },
        { "breaks", Switches.Make("x", b => b.Breaks = [" "]) },
        { "breaks", Switches.Make("x", b => b.Breaks = [""]) },
        { "registry key", WithAction(new RegistrySet("HKLM", "", "V", RegistryKind.Dword, "1")) },
        { "registry key", WithAction(new RegistrySet("HKLM", " ", "V", RegistryKind.Dword, "1")) },
        { "service", WithAction(new ServiceStart("", ServiceStartType.Disabled)) },
        { "task.disable path", WithAction(new TaskDisable("")) },
        { "appx.remove name", WithAction(new AppxRemove("")) },
        { "capability.remove name", WithAction(new CapabilityRemove(" ")) },
        { "feature.disable name", WithAction(new FeatureDisable("")) },
        { "package.remove pattern", WithAction(new PackageRemove("")) },
        { "file.remove path", WithAction(new FileRemove("")) },
        { "script body", WithAction(new Script("", "undo")) },
        { "windowsBuild", Switches.Make("x", b => b.Measured = new Measurement(1, 1, "", new DateOnly(2026, 10, 24))) },
    };

    [Theory]
    [MemberData(nameof(EmptyFieldCases))]
    public void EmptyRequiredFields_AreErrors(string field, SwitchDef sw)
    {
        var errors = One(sw);
        Assert.Single(errors);
        Assert.Contains(field, errors[0].Message);
        Assert.Contains("must not be empty", errors[0].Message);
    }

    [Fact]
    public void Modes_MustNotBeEmpty()
    {
        var errors = One(Switches.Make("x", b => { b.Modes = []; b.Live = []; }));
        Assert.Single(errors);
        Assert.Contains("at least one mode", errors[0].Message);
    }

    [Fact]
    public void RegistryValueName_MayBeEmptyMeaningTheDefaultValue()
    {
        Assert.Empty(One(Switches.Make("x", b =>
            b.Live = [new RegistrySet("HKLM", @"SOFTWARE\T", "", RegistryKind.String, "v")])));
    }

    [Fact]
    public void Measured_NegativeMemoryIsAllowed()
    {
        Assert.Empty(One(Switches.Make("x", b => b.Measured = new Measurement(-20, 0, "26300", new DateOnly(2026, 10, 24)))));
    }
}
