using Umbraco.Community.ArchivedUsers.Web.Controllers;

namespace Umbraco.Community.ArchivedUsers.Tests;

public class ReasonValidatorTests
{
    [Fact]
    public void Null_is_success_null() => Assert.Null(ReasonValidator.Normalise(null, required: false).Result);

    [Fact]
    public void Value_is_trimmed() => Assert.Equal("x", ReasonValidator.Normalise("  x  ", required: false).Result);

    [Fact]
    public void Whitespace_with_required_is_Required()
    {
        var result = ReasonValidator.Normalise("   ", required: true);
        Assert.False(result.Success);
        Assert.Equal(ReasonError.Required, result.Status);
    }

    [Fact]
    public void Whitespace_not_required_is_success_null()
    {
        var result = ReasonValidator.Normalise("   ", required: false);
        Assert.True(result.Success);
        Assert.Null(result.Result);
    }

    [Fact]
    public void Five_hundred_chars_is_success() => Assert.True(ReasonValidator.Normalise(new string('a', 500), required: false).Success);

    [Fact]
    public void Five_hundred_and_one_chars_is_TooLong()
    {
        var result = ReasonValidator.Normalise(new string('a', 501), required: false);
        Assert.False(result.Success);
        Assert.Equal(ReasonError.TooLong, result.Status);
    }

    /// <summary>Escaping is the renderer's job; the server stores what was sent (Review Focus 2).</summary>
    [Theory]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("Left 24 Oct 🎉 Zoë 李")]
    public void Hostile_or_non_latin_text_is_returned_unchanged(string reason)
        => Assert.Equal(reason, ReasonValidator.Normalise(reason, required: true).Result);
}
