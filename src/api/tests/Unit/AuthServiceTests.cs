using System.Text.RegularExpressions;
using Api.Models;
using Api.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit;

[Trait("Category", "Unit")]
public class AuthServiceTests
{
    private static List<User> TestUsers() => new()
    {
        new User { Username = "alice", Password = "pass123", Role = "participant", Team = "team-a" },
        new User { Username = "bob", Password = "Secret!", Role = "coach", Team = "team-a" },
        new User { Username = "admin", Password = "AdminPass", Role = "techlead", Team = null }
    };

    private static AuthService CreateService(List<User>? users = null) => new(users ?? TestUsers());

    [Fact]
    public void ValidateCredentials_WithCorrectPassword_ReturnsUser()
    {
        var svc = CreateService();
        var user = svc.ValidateCredentials("alice", "pass123");
        user.Should().NotBeNull();
        user!.Username.Should().Be("alice");
    }

    [Fact]
    public void ValidateCredentials_WithWrongPassword_ReturnsNull()
    {
        var svc = CreateService();
        svc.ValidateCredentials("alice", "wrong").Should().BeNull();
    }

    [Fact]
    public void ValidateCredentials_WithNonExistentUser_ReturnsNull()
    {
        var svc = CreateService();
        svc.ValidateCredentials("nobody", "pass123").Should().BeNull();
    }

    [Theory]
    [InlineData("alice", "Alice")]
    [InlineData("ALICE", "alice")]
    [InlineData("Alice", "ALICE")]
    public void ValidateCredentials_IsCaseInsensitiveForUsername(string input, string stored)
    {
        var users = new List<User>
        {
            new User { Username = stored, Password = "pw", Role = "participant", Team = "t1" }
        };
        var svc = CreateService(users);
        svc.ValidateCredentials(input, "pw").Should().NotBeNull();
    }

    [Fact]
    public void ValidateCredentials_IsCaseSensitiveForPassword()
    {
        var svc = CreateService();
        svc.ValidateCredentials("alice", "Pass123").Should().BeNull();
        svc.ValidateCredentials("alice", "PASS123").Should().BeNull();
    }

    [Fact]
    public void CreateSession_GeneratesMinimum32HexCharSessionId()
    {
        var svc = CreateService();
        var user = svc.ValidateCredentials("alice", "pass123")!;
        var session = svc.CreateSession(user);

        session.SessionId.Should().HaveLength(32);
        Regex.IsMatch(session.SessionId, "^[0-9a-f]{32}$").Should().BeTrue();
    }

    [Fact]
    public void CreateSession_StoresUserIdentityInSession()
    {
        var svc = CreateService();
        var user = svc.ValidateCredentials("alice", "pass123")!;
        var session = svc.CreateSession(user);

        session.Username.Should().Be("alice");
        session.Role.Should().Be("participant");
        session.Team.Should().Be("team-a");
    }

    [Fact]
    public void CreateSession_InvalidatesPreviousSessionForSameUser()
    {
        var svc = CreateService();
        var user = svc.ValidateCredentials("alice", "pass123")!;

        var session1 = svc.CreateSession(user);
        var session2 = svc.CreateSession(user);

        svc.GetSession(session1.SessionId).Should().BeNull();
        svc.GetSession(session2.SessionId).Should().NotBeNull();
    }

    [Fact]
    public void ResolveSession_WithValidSessionId_ReturnsIdentity()
    {
        var svc = CreateService();
        var user = svc.ValidateCredentials("alice", "pass123")!;
        var session = svc.CreateSession(user);

        var resolved = svc.GetSession(session.SessionId);
        resolved.Should().NotBeNull();
        resolved!.Username.Should().Be("alice");
        resolved.Role.Should().Be("participant");
    }

    [Fact]
    public void ResolveSession_WithInvalidSessionId_ReturnsNull()
    {
        var svc = CreateService();
        svc.GetSession("nonexistent-session-id").Should().BeNull();
    }

    [Fact]
    public void DestroySession_RemovesSessionFromStore()
    {
        var svc = CreateService();
        var user = svc.ValidateCredentials("alice", "pass123")!;
        var session = svc.CreateSession(user);

        svc.RemoveSession(session.SessionId);
        svc.GetSession(session.SessionId).Should().BeNull();
    }

    [Theory]
    [InlineData("participant")]
    [InlineData("coach")]
    [InlineData("techlead")]
    public void RoleMapping_ValidRoles_AreAccepted(string role)
    {
        var users = new List<User>
        {
            new User
            {
                Username = "testuser", Password = "pw", Role = role,
                Team = role == "techlead" ? null : "team-x"
            }
        };
        var act = () => new AuthService(users);
        act.Should().NotThrow();
    }

    [Fact]
    public void RoleMapping_InvalidRole_IsRejected()
    {
        var users = new List<User>
        {
            new User { Username = "bad", Password = "pw", Role = "superadmin", Team = "t1" }
        };
        var act = () => new AuthService(users);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Invalid role*");
    }

    [Fact]
    public void LoadUsers_WithDuplicateUsernames_ThrowsOnStartup()
    {
        var users = new List<User>
        {
            new User { Username = "alice", Password = "pw1", Role = "participant", Team = "t1" },
            new User { Username = "alice", Password = "pw2", Role = "participant", Team = "t1" }
        };
        var act = () => new AuthService(users);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Duplicate username*");
    }

    [Fact]
    public void LoadUsers_TechleadWithTeamId_ThrowsOnStartup()
    {
        var users = new List<User>
        {
            new User { Username = "lead", Password = "pw", Role = "techlead", Team = "team-x" }
        };
        var act = () => new AuthService(users);
        act.Should().Throw<InvalidOperationException>().WithMessage("*must not have a teamId*");
    }

    [Fact]
    public void LoadUsers_ParticipantWithoutTeamId_ThrowsOnStartup()
    {
        var users = new List<User>
        {
            new User { Username = "noTeam", Password = "pw", Role = "participant", Team = null }
        };
        var act = () => new AuthService(users);
        act.Should().Throw<InvalidOperationException>().WithMessage("*must have a teamId*");
    }

    [Fact]
    public void ResolveSession_TechleadHasNullTeamId()
    {
        var svc = CreateService();
        var user = svc.ValidateCredentials("admin", "AdminPass")!;
        var session = svc.CreateSession(user);

        var resolved = svc.GetSession(session.SessionId);
        resolved.Should().NotBeNull();
        resolved!.Role.Should().Be("techlead");
        resolved.Team.Should().BeNull();
    }
}
