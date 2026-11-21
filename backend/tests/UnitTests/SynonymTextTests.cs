using HawalekNow.Domain.Modules.Search;

namespace HawalekNow.UnitTests;

public sealed class SynonymTextTests
{
    [Theory]
    [InlineData(" Ｔ－ｓｈｉｒｔ ", "T-SHIRT")]
    [InlineData(" تي\t شيرت\u00a0", "تي شيرت")]
    [InlineData("e\u0301", "É")]
    public void Comparison_normalizes_compatibility_spaces_case_and_composition_but_keeps_display(string original, string expected)
    { var term = SynonymTerm.Create(Guid.NewGuid(), original); Assert.Equal(original, term.Original); Assert.Equal(expected, term.NormalizedKey); }
    [Fact]
    public void Required_examples_are_explicit_terms_and_arabic_letters_or_diacritics_are_not_merged()
    { Assert.Equal(4, SynonymText.Keys(["تيشرت", "تي شيرت", "T-shirt", "Tshirt"]).Length); Assert.NotEqual(SynonymText.Key("أمل"), SynonymText.Key("امل")); Assert.NotEqual(SynonymText.Key("عَلَم"), SynonymText.Key("علم")); }
    [Fact]
    public void Duplicate_or_empty_or_oversized_input_is_rejected_before_mutation()
    { Assert.Throws<ArgumentException>(() => SynonymText.Keys(["Tshirt", " tshirt "])); Assert.Throws<ArgumentException>(() => SynonymText.Keys([])); Assert.Throws<ArgumentException>(() => SynonymText.Key(new string('x', 81))); Assert.Throws<ArgumentException>(() => SynonymGroup.Create("اسم", "", true)); }
}
