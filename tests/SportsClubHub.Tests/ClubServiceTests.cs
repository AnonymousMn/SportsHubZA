using SportsClubHub.Domain.Common;
using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Services;

namespace SportsClubHub.Tests;

public class ClubServiceTests
{
    [Fact]
    public async Task FindNearest_ReturnsMappedVerifiedClubsInDistanceOrder()
    {
        using var database = new TestDatabase();
        var sport = await database.AddSportAsync("Football");
        var nearbyClub = await database.AddClubAsync("Nearby", "Cape Town", VerificationStatus.Verified, sport);
        var distantClub = await database.AddClubAsync("Distant", "Johannesburg", VerificationStatus.Verified, sport);

        await using (var db = database.CreateDbContext())
        {
            var nearby = await db.Clubs.FindAsync(nearbyClub.ClubId);
            nearby!.Latitude = -33.9249;
            nearby.Longitude = 18.4241;
            var distant = await db.Clubs.FindAsync(distantClub.ClubId);
            distant!.Latitude = -26.2041;
            distant.Longitude = 28.0473;
            await db.SaveChangesAsync();
        }

        var service = new ClubService(database);
        var results = await service.FindNearestAsync(-33.9250, 18.4242);

        Assert.Equal(2, results.Count);
        var item = results[0];
        Assert.Equal("Nearby", item.Club.Name);
        Assert.True(item.DistanceKm < 1);
    }
}
