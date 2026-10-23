using Microsoft.EntityFrameworkCore;
using SportsClubHub.Web.Models;
using SportsClubHub.Web.Services;

namespace SportsClubHub.Tests;

public class ParticipantInterestServiceTests
{
    private static ParticipantInterestForm ValidForm(string email = "Sam@Example.test") => new()
    {
        Name = "Sam Example",
        Email = email,
        Location = "Test Town",
        ConfirmedAdult = true,
        AcceptedPrivacyNotice = true
    };

    [Fact]
    public async Task Register_StoresParticipantWithNormalisedEmailAndInterest()
    {
        using var database = new TestDatabase();
        var netball = await database.AddSportAsync("Netball");
        var service = new ParticipantInterestService(database);

        var result = await service.RegisterAsync(ValidForm(), new[] { netball.SportId });

        Assert.True(result.Succeeded);
        await using var db = database.CreateDbContext();
        var participant = await db.Participants.Include(p => p.SportInterests).SingleAsync();
        Assert.Equal("sam@example.test", participant.Email);
        Assert.Single(participant.SportInterests);
    }

    [Fact]
    public async Task Register_SameEmailAndSportTwice_DoesNotCreateDuplicates()
    {
        using var database = new TestDatabase();
        var netball = await database.AddSportAsync("Netball");
        var service = new ParticipantInterestService(database);

        await service.RegisterAsync(ValidForm("sam@example.test"), new[] { netball.SportId });
        var second = await service.RegisterAsync(ValidForm("SAM@example.test"), new[] { netball.SportId });

        Assert.True(second.Succeeded);
        await using var db = database.CreateDbContext();
        Assert.Equal(1, await db.Participants.CountAsync());
        Assert.Equal(1, await db.SportInterests.CountAsync());
    }

    [Fact]
    public async Task Register_ReturningParticipantWithNewSport_AddsOnlyTheNewInterest()
    {
        using var database = new TestDatabase();
        var netball = await database.AddSportAsync("Netball");
        var chess = await database.AddSportAsync("Chess");
        var service = new ParticipantInterestService(database);

        await service.RegisterAsync(ValidForm(), new[] { netball.SportId });
        await service.RegisterAsync(ValidForm(), new[] { netball.SportId, chess.SportId });

        await using var db = database.CreateDbContext();
        Assert.Equal(1, await db.Participants.CountAsync());
        Assert.Equal(2, await db.SportInterests.CountAsync());
    }

    [Fact]
    public async Task Register_WithoutConsent_IsRejectedAndNothingIsStored()
    {
        using var database = new TestDatabase();
        var netball = await database.AddSportAsync("Netball");
        var service = new ParticipantInterestService(database);
        var form = ValidForm();
        form.AcceptedPrivacyNotice = false;

        var result = await service.RegisterAsync(form, new[] { netball.SportId });

        Assert.False(result.Succeeded);
        await using var db = database.CreateDbContext();
        Assert.Equal(0, await db.Participants.CountAsync());
    }

    [Fact]
    public async Task Register_WithNoSports_IsRejected()
    {
        using var database = new TestDatabase();
        var service = new ParticipantInterestService(database);

        var result = await service.RegisterAsync(ValidForm(), Array.Empty<int>());

        Assert.False(result.Succeeded);
    }
}
