namespace SportsClubHub.Domain.Entities;

/// <summary>
/// "This participant would like to take part in this sport."
///
/// This is deliberately NOT a club application and NOT a membership. Someone who is only
/// interested has not applied to anything, and someone who has applied is not yet a member.
/// If membership management is added later it should be a separate ClubMembership entity
/// with its own status, so these three states are never mixed up.
/// </summary>
public class SportInterest
{
    public int ParticipantId { get; set; }
    public Participant Participant { get; set; } = null!;

    public int SportId { get; set; }
    public Sport Sport { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
