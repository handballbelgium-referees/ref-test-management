using Handball.Belgium.RefTestManagement.Domain.Security;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class TokenServiceTests
{
    [Fact]
    public void GenerateBase64Url_ProducesUnpaddedUrlSafeOutput()
    {
        var token = TokenService.GenerateBase64Url(32);

        Assert.Equal(43, token.Length);
        Assert.All(token, character =>
            Assert.True(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'));
    }
}
