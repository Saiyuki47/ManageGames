using ManageGames.Helpers;

namespace ManageGames.Tests;

public class SearchFilterTests
{
    private static readonly string[] Titles = ["Super Mario 64", "Mario Kart", "Zelda"];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyTerm_ReturnsEverything(string? term)
    {
        Assert.Equal(Titles, SearchFilter.Filter(Titles, term, t => t));
    }

    [Fact]
    public void Term_MatchesIgnoringCaseAndSpaces()
    {
        Assert.Equal(new[] { "Super Mario 64", "Mario Kart" }, SearchFilter.Filter(Titles, "MARIO", t => t));
        Assert.Equal(new[] { "Mario Kart" }, SearchFilter.Filter(Titles, "mariokart", t => t));
    }

    [Fact]
    public void NullValues_NeverMatch_AndDoNotThrow()
    {
        string?[] values = [null, "Metroid"];

        Assert.Equal(new string?[] { "Metroid" }, SearchFilter.Filter(values, "metroid", v => v!));
    }
}
