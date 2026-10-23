using Microsoft.EntityFrameworkCore;
using SportsClubHub.Domain.Entities;

namespace SportsClubHub.Web.Data;

/// <summary>
/// Starter data. The sports list is reference data and is always added to an empty database.
/// The clubs, venues, teams and participants are FICTIONAL demonstration data: descriptions start
/// with "[Demo data]", e-mail addresses use the reserved example.test domain and participant names
/// are "Demo Participant NN". No real people or organisations are used.
/// </summary>
public static class SeedData
{
    public static List<Sport> CreateSports() => new()
    {
        Make("Football", SportCategory.FieldSport, "Grass or artificial pitch", "Eleven-a-side association football, plus five-a-side and futsal variants."),
        Make("Rugby Union", SportCategory.FieldSport, "Grass field", "Fifteen-a-side contact team sport."),
        Make("Cricket", SportCategory.FieldSport, "Cricket oval with nets", "Bat-and-ball team sport played in several formats."),
        Make("Hockey", SportCategory.FieldSport, "Astro or grass field", "Stick-and-ball team sport played on a field."),
        Make("Netball", SportCategory.CourtSport, "Outdoor or indoor court", "Seven-a-side court sport with no dribbling."),
        Make("Basketball", SportCategory.CourtSport, "Indoor or outdoor court", "Five-a-side court sport played to a raised hoop."),
        Make("Volleyball", SportCategory.CourtSport, "Indoor court or sand court", "Net sport for six-a-side indoor teams, or pairs on sand."),
        Make("Tennis", SportCategory.CourtSport, "Hard, clay or grass court", "Racket sport for singles or doubles."),
        Make("Badminton", SportCategory.CourtSport, "Indoor hall", "Racket-and-shuttlecock sport for singles or doubles."),
        Make("Swimming", SportCategory.Aquatic, "25 m or 50 m pool", "Competitive and recreational swimming."),
        Make("Water Polo", SportCategory.Aquatic, "Deep pool", "Seven-a-side team sport played in a pool."),
        Make("Surfing", SportCategory.Aquatic, "Coastline", "Wave riding, from beginner lessons to competition."),
        Make("Athletics (Track and Field)", SportCategory.Athletics, "Athletics track", "Running, jumping and throwing events."),
        Make("Road Running", SportCategory.Athletics, "Roads and parks", "Fun runs through to marathons."),
        Make("Boxing", SportCategory.Combat, "Gym with a ring", "Amateur boxing and fitness boxing."),
        Make("Judo", SportCategory.Combat, "Dojo with mats", "Grappling martial art and Olympic sport."),
        Make("Taekwondo", SportCategory.Combat, "Dojo with mats", "Striking martial art and Olympic sport."),
        Make("Chess", SportCategory.Indoor, "Any quiet hall or room", "Strategy board game played competitively and socially."),
        Make("Table Tennis", SportCategory.Indoor, "Indoor hall", "Fast racket sport played on a table."),
        Make("Squash", SportCategory.Indoor, "Squash court", "Racket sport played in an enclosed court."),
        Make("Cycling", SportCategory.Recreational, "Roads, trails or velodrome", "Road, mountain-bike and track cycling."),
        Make("Archery", SportCategory.Recreational, "Open range or hall", "Target shooting with a bow."),
        Make("Hiking and Trail Running", SportCategory.Recreational, "Trails and nature reserves", "Outdoor group walks and trail runs.")
    };

    private static Sport Make(string name, SportCategory category, string facility, string description) => new()
    {
        Name = name,
        Category = category,
        TypicalFacilityType = facility,
        Description = description
    };

    /// <summary>Adds fictional clubs, teams, venues and participants. Call only on a database with no clubs.</summary>
    public static async Task AddDemoDataAsync(AppDbContext db)
    {
        var sports = await db.Sports.ToDictionaryAsync(s => s.Name);
        Sport S(string name) => sports[name];

        // --- Clubs (all marked as demo data, one left Pending to show the verification step) ---
        var clubs = new List<Club>
        {
            DemoClub("Baywest Football Club", "Nelson Mandela Bay", "Community football club with youth and senior sides.", VerificationStatus.Verified, S("Football")),
            DemoClub("Ocean View Aquatics", "Nelson Mandela Bay", "Learn-to-swim programmes and a competitive squad.", VerificationStatus.Verified, S("Swimming"), S("Water Polo")),
            DemoClub("Rivercrest Netball Club", "Buffalo City", "Social and league netball for all ages.", VerificationStatus.Verified, S("Netball")),
            DemoClub("Eastbank Multi-Sport Club", "Buffalo City", "Basketball and volleyball under one roof.", VerificationStatus.Verified, S("Basketball"), S("Volleyball")),
            DemoClub("Highveld Rugby Club", "Johannesburg", "Senior and junior rugby union.", VerificationStatus.Verified, S("Rugby Union")),
            DemoClub("Gold Reef Athletics Club", "Johannesburg", "Track and field and road running.", VerificationStatus.Verified, S("Athletics (Track and Field)"), S("Road Running")),
            DemoClub("Umhlanga Combat Academy", "eThekwini", "Judo and taekwondo classes in a friendly dojo.", VerificationStatus.Verified, S("Judo"), S("Taekwondo")),
            DemoClub("Southern Cross Chess Circle", "Mangaung", "Weekly casual and rated chess evenings.", VerificationStatus.Pending, S("Chess"))
        };
        db.Clubs.AddRange(clubs);

        // --- Teams (each team's sport is one the club offers) ---
        db.Teams.AddRange(
            DemoTeam(clubs[0], S("Football"), "Baywest First Team", "Open", CompetitionLevel.Competitive),
            DemoTeam(clubs[0], S("Football"), "Baywest Under 13", "Under 13", CompetitionLevel.Development),
            DemoTeam(clubs[1], S("Swimming"), "Ocean View Masters", "Masters 30+", CompetitionLevel.Recreational),
            DemoTeam(clubs[1], S("Water Polo"), "Ocean View Water Polo", "Open", CompetitionLevel.Competitive),
            DemoTeam(clubs[2], S("Netball"), "Rivercrest Social Netball", "Open", CompetitionLevel.Recreational),
            DemoTeam(clubs[2], S("Netball"), "Rivercrest League A", "Open", CompetitionLevel.Competitive),
            DemoTeam(clubs[3], S("Basketball"), "Eastbank Hoops", "Under 19", CompetitionLevel.Development),
            DemoTeam(clubs[4], S("Rugby Union"), "Highveld First XV", "Open", CompetitionLevel.Competitive),
            DemoTeam(clubs[6], S("Judo"), "Umhlanga Judo Squad", "Open", CompetitionLevel.Competitive));

        // --- Venues ---
        db.Venues.AddRange(
            DemoVenue("Baywest Community Sports Ground", "Nelson Mandela Bay", VenueType.Field, "Natural grass", 2500, S("Football"), S("Hockey"), S("Cricket")),
            DemoVenue("Ocean View Aquatic Centre", "Nelson Mandela Bay", VenueType.Pool, "Pool, 25 m", 400, S("Swimming"), S("Water Polo")),
            DemoVenue("Eastbank Indoor Hall", "Buffalo City", VenueType.Hall, "Sprung timber floor", 800, S("Basketball"), S("Volleyball"), S("Netball"), S("Badminton"), S("Table Tennis")),
            DemoVenue("Highveld Stadium", "Johannesburg", VenueType.Stadium, "Natural grass", 12000, S("Rugby Union"), S("Football")),
            DemoVenue("Gold Reef Athletics Track", "Johannesburg", VenueType.Track, "Synthetic track", 3000, S("Athletics (Track and Field)")),
            DemoVenue("Umhlanga Dojo", "eThekwini", VenueType.Hall, "Tatami mats", 120, S("Judo"), S("Taekwondo")));

        // --- Participants (fictional). Interest is skewed so the insights page has something to show. ---
        var participants = new List<Participant>();
        var plan = new (string Location, string[] Sports)[]
        {
            ("Nelson Mandela Bay", new[] { "Football", "Volleyball" }),
            ("Nelson Mandela Bay", new[] { "Volleyball", "Swimming" }),
            ("Nelson Mandela Bay", new[] { "Volleyball" }),
            ("Nelson Mandela Bay", new[] { "Chess", "Football" }),
            ("Buffalo City", new[] { "Netball" }),
            ("Buffalo City", new[] { "Netball", "Chess" }),
            ("Buffalo City", new[] { "Table Tennis" }),
            ("Johannesburg", new[] { "Chess", "Rugby Union" }),
            ("Johannesburg", new[] { "Chess" }),
            ("Johannesburg", new[] { "Surfing" }),
            ("eThekwini", new[] { "Surfing", "Judo" }),
            ("Mangaung", new[] { "Archery" })
        };

        for (var i = 0; i < plan.Length; i++)
        {
            var number = i + 1;
            var participant = new Participant
            {
                Name = $"Demo Participant {number:00}",
                Email = $"demo.participant{number:00}@example.test",
                Location = plan[i].Location,
                CreatedAt = DateTime.UtcNow,
                ConsentGivenAt = DateTime.UtcNow
            };

            foreach (var sportName in plan[i].Sports)
            {
                participant.SportInterests.Add(new SportInterest { Sport = S(sportName), CreatedAt = DateTime.UtcNow });
            }

            participants.Add(participant);
        }

        db.Participants.AddRange(participants);
        await db.SaveChangesAsync();
    }

    private static Club DemoClub(string name, string location, string description, VerificationStatus status, params Sport[] sports)
    {
        var slug = new string(name.ToLowerInvariant().Where(char.IsLetter).ToArray());
        var club = new Club
        {
            Name = name,
            Location = location,
            Description = "[Demo data] " + description,
            ContactEmail = $"info@{slug}.example.test",
            VerificationStatus = status
        };

        foreach (var sport in sports)
        {
            club.ClubSports.Add(new ClubSport { Sport = sport });
        }

        return club;
    }

    private static Team DemoTeam(Club club, Sport sport, string name, string ageGroup, CompetitionLevel level) => new()
    {
        Club = club,
        Sport = sport,
        Name = name,
        AgeGroup = ageGroup,
        CompetitionLevel = level
    };

    private static Venue DemoVenue(string name, string location, VenueType type, string surface, int capacity, params Sport[] sports)
    {
        var venue = new Venue
        {
            Name = name,
            Location = location,
            Address = "[Demo data] Fictional address",
            VenueType = type,
            SurfaceType = surface,
            Capacity = capacity
        };

        foreach (var sport in sports)
        {
            venue.VenueSports.Add(new VenueSport { Sport = sport });
        }

        return venue;
    }
}
