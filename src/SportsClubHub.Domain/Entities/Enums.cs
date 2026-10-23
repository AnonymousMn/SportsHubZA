namespace SportsClubHub.Domain.Entities;

/// <summary>Broad grouping used to browse and filter sports.</summary>
public enum SportCategory
{
    FieldSport,
    CourtSport,
    Aquatic,
    Athletics,
    Combat,
    Indoor,
    Recreational
}

/// <summary>
/// Whether an administrator has checked a club. New clubs always start as Pending
/// and only Verified clubs appear in the public directory.
/// </summary>
public enum VerificationStatus
{
    Pending,
    Verified,
    Rejected
}

public enum CompetitionLevel
{
    Recreational,
    Development,
    Competitive,
    Elite
}

public enum VenueType
{
    Field,
    Court,
    Hall,
    Pool,
    Track,
    Stadium,
    Gym,
    Other
}
