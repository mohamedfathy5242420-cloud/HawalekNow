using HawalekNow.Application.Modules.Locations;

namespace HawalekNow.UnitTests;

public sealed class ReferenceSeedRulesTests
{
    private static ReferenceSeedDataset Valid() => new(ReferenceSeedRules.AcceptedCommit,
        [new("EG01", null, "محافظة", "Governorate")], [new("EG0101", "EG01", "مدينة", "City")], [new("EG010101", "EG0101", "منطقة", "Area")]);
    [Fact]
    public void Bilingual_parent_linked_source_keys_are_accepted() => ReferenceSeedRules.Validate(Valid());
    [Theory]
    [InlineData("version")]
    [InlineData("duplicate")]
    [InlineData("city-parent")]
    [InlineData("area-parent")]
    [InlineData("missing-English")]
    [InlineData("empty-key")]
    public void Bad_version_keys_names_or_parent_levels_fail_without_echoing_data(string failure)
    {
        var source = Valid();
        source = failure switch
        {
            "version" => source with { SourceCommit = new string('0', 40) },
            "duplicate" => source with { Cities = [source.Cities[0] with { SourceKey = "EG01" }] },
            "city-parent" => source with { Cities = [source.Cities[0] with { ParentSourceKey = "EG99" }] },
            "area-parent" => source with { Areas = [source.Areas[0] with { ParentSourceKey = "EG01" }] },
            "missing-English" => source with { Areas = [source.Areas[0] with { NameEn = "" }] },
            _ => source with { Governorates = [source.Governorates[0] with { SourceKey = "" }] }
        };
        var error = Assert.Throws<InvalidOperationException>(() => ReferenceSeedRules.Validate(source));
        Assert.Equal("The pinned reference dataset is invalid.", error.Message);
    }
}
