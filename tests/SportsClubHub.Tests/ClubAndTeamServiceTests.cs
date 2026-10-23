using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Services;

namespace SportsClubHub.Tests;

public class ClubAndTeamServiceTests
{
    [Fact]
    public async Task Register_CreatesPendingClubThatIsHiddenFromThePublicDirectory()
    {
        using var database = new TestDatabase();
        var chess = await database.AddSportAsync("Chess", SportCategory.Indoor);
        var service = new ClubService(database);
        var club = new Club
        {
            Name = "Test Chess Circle",
            Location = "Test Town",
            ContactEmail = "Chess@Example.test",
            VerificationStatus = VerificationStatus.Verified // the caller must not be able to self-verify
        };

        var result = await service.RegisterAsync(club, new[] { chess.SportId });

        Assert.True(result.Succeeded);
        Assert.Empty(await service.SearchAsync(null, null, publicOnly: true));
        var admin = Assert.Single(await service.SearchAsync(null, null, publicOnly: false));
        Assert.Equal(VerificationStatus.Pending, admin.VerificationStatus);
        Assert.Equal("chess@example.test", admin.ContactEmail);
    }

    [Fact]
    public async Task Register_DuplicateNameAndLocation_IsRejected()
    {
        using var database = new TestDatabase();
        var chess = await database.AddSportAsync("Chess", SportCategory.Indoor);
        await database.AddClubAsync("Test Chess Circle", "Test Town", VerificationStatus.Verified, chess);
        var service = new ClubService(database);

        var result = await service.RegisterAsync(
            new Club { Name = "test chess circle", Location = "TEST TOWN", ContactEmail = "a@example.test" },
            new[] { chess.SportId });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Search_FiltersBySportAndOnlyReturnsVerifiedClubs()
    {
        using var database = new TestDatabase();
        var chess = await database.AddSportAsync("Chess", SportCategory.Indoor);
        var netball = await database.AddSportAsync("Netball", SportCategory.CourtSport);
        await database.AddClubAsync("Chess Club", "Town A", VerificationStatus.Verified, chess);
        await database.AddClubAsync("Netball Club", "Town A", VerificationStatus.Verified, netball);
        await database.AddClubAsync("Unverified Chess Club", "Town B", VerificationStatus.Pending, chess);
        var service = new ClubService(database);

        var result = await service.SearchAsync(null, chess.SportId);

        var club = Assert.Single(result);
        Assert.Equal("Chess Club", club.Name);
    }

    [Fact]
    public async Task Team_CannotPlayASportItsClubDoesNotOffer()
    {
        using var database = new TestDatabase();
        var chess = await database.AddSportAsync("Chess", SportCategory.Indoor);
        var netball = await database.AddSportAsync("Netball", SportCategory.CourtSport);
        var club = await database.AddClubAsync("Chess Club", "Town A", VerificationStatus.Verified, chess);
        var service = new TeamService(database);

        var rejected = await service.SaveAsync(new Team { Name = "Netball A", ClubId = club.ClubId, SportId = netball.SportId });
        var accepted = await service.SaveAsync(new Team { Name = "Chess A", ClubId = club.ClubId, SportId = chess.SportId });

        Assert.False(rejected.Succeeded);
        Assert.True(accepted.Succeeded);
    }

    [Fact]
    public async Task Club_CannotDropASportWhileItHasAnActiveTeamForIt()
    {
        using var database = new TestDatabase();
        var chess = await database.AddSportAsync("Chess", SportCategory.Indoor);
        var draughts = await database.AddSportAsync("Draughts", SportCategory.Indoor);
        var club = await database.AddClubAsync("Board Games Club", "Town A", VerificationStatus.Verified, chess, draughts);
        await new TeamService(database).SaveAsync(new Team { Name = "Chess A", ClubId = club.ClubId, SportId = chess.SportId });
        var clubService = new ClubService(database);

        var edited = new Club { ClubId = club.ClubId, Name = club.Name, Location = club.Location, ContactEmail = club.ContactEmail };
        var result = await clubService.SaveAsync(edited, new[] { draughts.SportId });

        Assert.False(result.Succeeded);
    }
}
