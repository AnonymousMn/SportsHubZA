using Microsoft.EntityFrameworkCore;
using SportsClubHub.Domain.Common;
using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Data;
using SportsClubHub.Web.Models;

namespace SportsClubHub.Web.Services;

public record InterestRegistrationResult(bool Succeeded, string Message);

/// <summary>
/// Records "I am interested in this sport". This is not a club application and not a membership.
/// </summary>
public class ParticipantInterestService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly EmailService? _email;

    public ParticipantInterestService(IDbContextFactory<AppDbContext> factory, EmailService? email = null)
    {
        _factory = factory;
        _email = email;
    }

    public async Task<InterestRegistrationResult> RegisterAsync(ParticipantInterestForm form, IReadOnlyCollection<int> sportIds)
    {
        // The page already checks these, but the service must not trust the page.
        if (!form.ConfirmedAdult || !form.AcceptedPrivacyNotice)
        {
            return new(false, "Please confirm you are 18 or older and accept the privacy notice.");
        }

        if (sportIds.Count == 0)
        {
            return new(false, "Please choose at least one sport.");
        }

        var email = InputNormaliser.Email(form.Email);

        await using var db = await _factory.CreateDbContextAsync();

        var sports = await db.Sports
            .Where(s => s.IsActive && sportIds.Contains(s.SportId))
            .OrderBy(s => s.Name)
            .ToListAsync();
        if (sports.Count == 0)
        {
            return new(false, "Please choose at least one sport from the list.");
        }

        var participant = await db.Participants
            .Include(p => p.SportInterests)
            .FirstOrDefaultAsync(p => p.Email == email);

        if (participant is null)
        {
            var now = DateTime.UtcNow;
            participant = new Participant
            {
                Name = InputNormaliser.Text(form.Name),
                Email = email,
                Phone = form.Phone,
                Location = InputNormaliser.Text(form.Location),
                CreatedAt = now,
                ConsentGivenAt = now
            };
            db.Participants.Add(participant);
        }

        // An existing participant keeps the details they gave the first time; we only add
        // sports they were not already interested in. This stops duplicate interest rows.
        var alreadyInterested = participant.SportInterests.Select(si => si.SportId).ToHashSet();
        foreach (var sport in sports.Where(s => !alreadyInterested.Contains(s.SportId)))
        {
            participant.SportInterests.Add(new SportInterest { SportId = sport.SportId, CreatedAt = DateTime.UtcNow });
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Two submissions for the same e-mail at the same moment can hit the unique index.
            return new(false, "We could not save your registration. Please try again.");
        }

        // The wording is the same for new and returning people so the form cannot be used
        // to discover whether an e-mail address is already registered.
        var names = string.Join(", ", sports.Select(s => s.Name));
        if (_email is not null)
        {
            await _email.TrySendAsync(
                email,
                "Sports Club Hub SA interest acknowledgement",
                $"Hello {participant.Name},\n\nThank you for expressing interest in {names}. Your registration has been received.\n\nSports Club Hub SA",
                "interest acknowledgement");
        }
        return new(true, $"Thank you. Your interest in {names} has been recorded.");
    }
}
