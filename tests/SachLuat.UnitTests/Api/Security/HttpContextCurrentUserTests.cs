using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using SachLuat.Api.Security;
using SachLuat.SharedKernel.Security;
using Xunit;

namespace SachLuat.UnitTests.Api.Security;

public sealed class HttpContextCurrentUserTests
{
    [Fact]
    public void SelfLogin_HasUserIdAndNoImpersonator()
    {
        ICurrentUser currentUser = Create(new Claim(AuthClaimTypes.Subject, "42"));

        currentUser.UserId.Should().Be(42);
        currentUser.ImpersonatorUserId.Should().BeNull();
        currentUser.IsImpersonated.Should().BeFalse();
    }

    [Fact]
    public void Impersonated_ExposesImpersonatorId()
    {
        ICurrentUser currentUser = Create(
            new Claim(AuthClaimTypes.Subject, "42"),
            new Claim(AuthClaimTypes.ImpersonatorId, "1"));

        currentUser.UserId.Should().Be(42);
        currentUser.ImpersonatorUserId.Should().Be(1);
        currentUser.IsImpersonated.Should().BeTrue();
    }

    private static HttpContextCurrentUser Create(params Claim[] claims)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        return new HttpContextCurrentUser(new HttpContextAccessor { HttpContext = context });
    }
}
