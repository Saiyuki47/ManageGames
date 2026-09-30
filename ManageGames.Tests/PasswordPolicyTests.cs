using ManageGames.Auth;

namespace ManageGames.Tests;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("123456789012345")]          // digit sequence
    [InlineData("987654321098765")]          // backwards
    [InlineData("qwertzuiopasdfgh")]         // keyboard row
    [InlineData("1qaz2wsx3edc4rfv")]         // keyboard columns
    [InlineData("abcdefghijklmnop")]         // alphabet
    [InlineData("hallo123hallo123")]         // repetition
    [InlineData("password1234567")]          // common word padded with digits
    [InlineData("Passwort!!2026??")]         // common word with symbols
    [InlineData("sommer2026sommer")]         // repeated word
    [InlineData("my-managegames-login")]     // the app's name
    [InlineData("the-max-power-of-lions")]   // the username
    public void GuessablePasswords_AreRejected(string password)
    {
        Assert.True(CommonPasswordValidator.IsGuessable(password, "Max"));
    }

    [Theory]
    [InlineData("correct horse battery staple")]
    [InlineData("Blue-Ocean-Lantern-42")]
    [InlineData("8473926150384726")]
    [InlineData("Tr0ub4dor&3-grün")]
    public void StrongPasswords_AreAccepted(string password)
    {
        Assert.False(CommonPasswordValidator.IsGuessable(password, "Max"));
    }
}
