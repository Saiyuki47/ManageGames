using ManageGames.Services.Covers;

namespace ManageGames.Tests;

public class TitleMatcherTests
{
    [Theory]
    [InlineData("Super Mario Odyssey", "Super Mario Odyssey")]
    [InlineData("Animal Crossing New Horizons", "Animal Crossing: New Horizons")]
    [InlineData("Pokemon Leaf Green", "Pokémon LeafGreen Version")]
    [InlineData("Zelda: Breath of the Wild", "The Legend of Zelda: Breath of the Wild")]
    [InlineData("Final Fantasy 7", "FINAL FANTASY VII")]
    [InlineData("Kirbys Dream Land", "Kirby's Dream Land")]
    [InlineData("Mario & Luigi", "Mario and Luigi")]
    [InlineData("Okami", "Ōkami")]
    [InlineData("Zelda: Link's Awakening", "The Legend of Zelda: Link's Awakening")]
    [InlineData("Zelda: Skyward Sword HD", "The Legend of Zelda: Skyward Sword HD")]
    [InlineData("Zelda – Majora's Mask 3D", "The Legend of Zelda: Majora's Mask 3D")]
    [InlineData("Wii Fit Plus", "Wii Fit +")]
    [InlineData("Pikmin 1+2", "Pikmin 1 + 2")]
    public void Similarity_AcceptsTheSameGameWrittenDifferently(string title, string other)
    {
        Assert.True(TitleMatcher.Similarity(title, other) >= TitleMatcher.AcceptThreshold, $"{title} / {other}");
    }

    [Theory]
    [InlineData("Mario Kart 8", "Mario Kart 7")]
    [InlineData("Splatoon", "Splatoon 2")]
    [InlineData("Final Fantasy VII", "Final Fantasy VIII")]
    [InlineData("Wii Sports", "Wii Sports Resort")]
    [InlineData("Wii Play", "Wii Party")]
    [InlineData("Zelda", "The Legend of Zelda: Tears of the Kingdom")]
    [InlineData("Doom", "Boom")]
    [InlineData("!!!", "Doom")]
    [InlineData("Pikmin", "Pikmin²")]
    [InlineData("Pikmin", "Pikmin^2")]
    [InlineData("Animal Crossing: New Horizons – NS2 Edition", "Animal Crossing: New Horizons")]
    [InlineData("Zelda: Four Swords", "The Legend of Zelda: Four Swords Adventures")]
    [InlineData("Metroid: Samus Returns", "Castlevania: Samus Returns")]
    [InlineData("Zelda: Spirit Tracks", "The Legend of Zelda: Phantom Hourglass")]
    [InlineData("Zelda:", "The Legend of Zelda: Spirit Tracks")]
    public void Similarity_RejectsOtherGames(string title, string other)
    {
        Assert.True(TitleMatcher.Similarity(title, other) < TitleMatcher.AcceptThreshold, $"{title} / {other}");
    }

    [Fact]
    public void Similarity_UsesTheBestAlternativeTitle()
    {
        var candidate = Candidate("Pokémon Sword", alternativeTitles: ["Pocket Monsters Sword", "Pokémon Schwert"]);

        Assert.Equal(1, TitleMatcher.Similarity("Pokemon Schwert", candidate));
    }

    [Theory]
    [InlineData("Nintendo Switch", "Nintendo Switch")]
    [InlineData("Switch", "Nintendo Switch")]
    [InlineData("Nintendo GameCube", "GameCube")]
    [InlineData("Sega Mega Drive", "Sega Mega Drive/Genesis")]
    [InlineData("Genesis", "Sega Mega Drive/Genesis")]
    [InlineData("GameBoy", "Game Boy")]
    [InlineData("Nintendo 3DS", "Nintendo 3DS")]
    [InlineData("Sega 32X", "32X")]
    public void PlatformMatches_IgnoresMakerNamesAndRegionalVariants(string console, string platform)
    {
        Assert.True(TitleMatcher.PlatformMatches(console, Candidate("Game", platforms: [platform])));
    }

    [Theory]
    [InlineData("Game Boy", "Game Boy Advance")]
    [InlineData("Nintendo Switch", "Nintendo Switch 2")]
    [InlineData("Nintendo DS", "Nintendo 3DS")]
    [InlineData("Wii", "Wii U")]
    public void PlatformMatches_RejectsOtherConsoles(string console, string platform)
    {
        Assert.False(TitleMatcher.PlatformMatches(console, Candidate("Game", platforms: [platform])));
    }

    [Fact]
    public void PlatformMatches_UsesAliases()
    {
        Assert.True(TitleMatcher.PlatformMatches("SNES", Candidate("Game", platforms: ["Super Nintendo Entertainment System"], aliases: ["SNES", "Super Famicom"])));
    }

    [Fact]
    public void PlatformMatches_IsUnknown_WithoutConsoleOrPlatforms()
    {
        Assert.Null(TitleMatcher.PlatformMatches(null, Candidate("Game", platforms: ["Wii"])));
        Assert.Null(TitleMatcher.PlatformMatches("  ", Candidate("Game", platforms: ["Wii"])));
        Assert.Null(TitleMatcher.PlatformMatches("Wii", Candidate("Game")));
    }

    [Fact]
    public void Tokens_DropAccentsPunctuationAndCase()
    {
        Assert.Equal(["pokemon", "lets", "go", "strasse"], TitleMatcher.Tokens("Pokémon: Let's Go – Straße!"));
        Assert.Equal(["pikmin", "2", "ns", "2", "edition", "3", "ds"], TitleMatcher.Tokens("Pikmin² NS2 Edition 3DS"));
        Assert.Equal(["wii", "fit", "plus", "mario", "and", "luigi"], TitleMatcher.Tokens("Wii Fit+ Mario & Luigi"));
    }

    private static CoverCandidate Candidate(string title, string[]? alternativeTitles = null, string[]? platforms = null, string[]? aliases = null)
    {
        var url = new Uri("https://images.example.test/cover.png");
        return new CoverCandidate("Test", title, alternativeTitles ?? [], platforms ?? [], aliases ?? [], null, url, url);
    }
}
