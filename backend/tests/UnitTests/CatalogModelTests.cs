using HawalekNow.Domain.Modules.Catalog;

namespace HawalekNow.UnitTests;

public sealed class CatalogModelTests
{
    [Fact]
    public void Names_respect_storage_limits()
    {
        Assert.Equal(100, Activity.Create(new string('ع', 100), new string('A', 100)).NameEn.Length);
        Assert.Throws<ArgumentException>(() => Activity.Create(new string('ع', 101), "Name"));
        Assert.Throws<ArgumentException>(() => Activity.Create("اسم", new string('A', 101)));
    }
    [Theory]
    [InlineData("", "Clothes")]
    [InlineData("ملابس", " ")]
    [InlineData("bad\0name", "Name")]
    public void Both_administrative_names_are_required(string ar, string en) =>
        Assert.Throws<ArgumentException>(() => Activity.Create(ar, en));

    [Fact]
    public void Comparison_is_compatibility_normalized_and_case_insensitive_but_display_is_preserved()
    {
        var activity = Activity.Create("ملابس", " Ｃlothes  and   accessories ");
        Assert.Equal("Ｃlothes  and   accessories", activity.NameEn);
        Assert.Equal("CLOTHES AND ACCESSORIES", activity.NameEnKey);
        Assert.NotEqual(CatalogNames.Key("على"), CatalogNames.Key("علي"));
    }

    [Fact]
    public void Invalid_rename_keeps_both_names_and_keys()
    {
        var activity = Activity.Create("ملابس", "Clothes");
        Assert.Throws<ArgumentException>(() => activity.Rename("تغيير", ""));
        Assert.Equal("ملابس", activity.NameAr); Assert.Equal("CLOTHES", activity.NameEnKey);
    }

    [Fact]
    public void Self_parent_and_empty_references_are_rejected_without_changing_state()
    {
        var category = Category.Create(Guid.CreateVersion7(), null, "رجالي", "Men");
        Assert.Throws<ArgumentException>(() => category.SetParent(category.Id));
        Assert.Throws<ArgumentException>(() => category.SetParent(Guid.Empty));
        Assert.Null(category.ParentId);
        Assert.Throws<ArgumentException>(() => Category.Create(Guid.Empty, null, "رجالي", "Men"));
    }

    [Fact]
    public void Model_has_no_business_depth_limit_and_activation_does_not_rewrite_children()
    {
        var activity = Activity.Create("ملابس", "Clothes");
        var root = Category.Create(activity.Id, null, "جذر", "Root");
        var previous = root;
        for (var i = 0; i < 1000; i++) previous = Category.Create(activity.Id, previous.Id, "فرع", "Child");
        root.SetActive(false);
        Assert.True(previous.IsActive); Assert.False(root.IsActive);
        Assert.NotNull(previous.ParentId);
    }
}
