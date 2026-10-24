using Microsoft.EntityFrameworkCore;
using System.Globalization;
using SportsClubHub.Domain.Common;
using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Data;

namespace SportsClubHub.Web.Services;

public record VenueImportRow(
    int RowNumber,
    string SourceFile,
    Venue? Venue,
    IReadOnlyList<string> SportNames,
    string? Error);

public class VenueService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public VenueService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<List<Venue>> SearchAsync(string? location, int? sportId, bool includeInactive = false)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var query = db.Venues.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(v => v.IsActive);
        }

        var term = InputNormaliser.Text(location);
        if (term.Length > 0)
        {
            query = query.Where(v => EF.Functions.Like(v.Location, $"%{term}%"));
        }

        if (sportId is not null)
        {
            query = query.Where(v => v.VenueSports.Any(vs => vs.SportId == sportId));
        }

        return await query
            .Include(v => v.VenueSports).ThenInclude(vs => vs.Sport)
            .OrderBy(v => v.Name)
            .ToListAsync();
    }

    public async Task<Venue?> GetAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Venues.AsNoTracking()
            .Include(v => v.VenueSports).ThenInclude(vs => vs.Sport)
            .FirstOrDefaultAsync(v => v.VenueId == id);
    }

    /// <summary>Locations that already appear on a venue or club, used to suggest consistent spelling.</summary>
    public async Task<List<string>> GetKnownLocationsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var venueLocations = await db.Venues.Select(v => v.Location).ToListAsync();
        var clubLocations = await db.Clubs.Select(c => c.Location).ToListAsync();
        return venueLocations.Concat(clubLocations)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(l => l)
            .ToList();
    }

    /// <summary>Adds a venue (VenueId = 0) or updates one, together with the sports it supports.</summary>
    public async Task<OperationResult> SaveAsync(Venue edited, IReadOnlyCollection<int> sportIds)
    {
        if (sportIds.Count == 0)
        {
            return OperationResult.Fail("Choose at least one sport the venue supports.");
        }

        edited.Name = InputNormaliser.Text(edited.Name);
        edited.Location = InputNormaliser.Text(edited.Location);
        edited.Address = InputNormaliser.NullIfBlank(edited.Address);
        edited.SurfaceType = InputNormaliser.NullIfBlank(edited.SurfaceType);

        await using var db = await _factory.CreateDbContextAsync();

        var nameTaken = await db.Venues.AnyAsync(v =>
            v.VenueId != edited.VenueId &&
            v.Name.ToLower() == edited.Name.ToLower() &&
            v.Location.ToLower() == edited.Location.ToLower());
        if (nameTaken)
        {
            return OperationResult.Fail("A venue with that name already exists in that location.");
        }

        Venue venue;
        if (edited.VenueId == 0)
        {
            venue = edited;
            db.Venues.Add(venue);
        }
        else
        {
            var existing = await db.Venues.Include(v => v.VenueSports).FirstOrDefaultAsync(v => v.VenueId == edited.VenueId);
            if (existing is null)
            {
                return OperationResult.Fail("Venue not found.");
            }

            existing.Name = edited.Name;
            existing.Location = edited.Location;
            existing.Address = edited.Address;
            existing.VenueType = edited.VenueType;
            existing.SurfaceType = edited.SurfaceType;
            existing.Capacity = edited.Capacity;
            venue = existing;
        }

        var toRemove = venue.VenueSports.Where(vs => !sportIds.Contains(vs.SportId)).ToList();
        foreach (var row in toRemove)
        {
            venue.VenueSports.Remove(row);
        }

        var existingIds = venue.VenueSports.Select(vs => vs.SportId).ToHashSet();
        foreach (var id in sportIds.Where(id => !existingIds.Contains(id)))
        {
            venue.VenueSports.Add(new VenueSport { SportId = id });
        }

        await db.SaveChangesAsync();
        return OperationResult.Success();
    }

    public async Task<OperationResult> SetActiveAsync(int venueId, bool isActive)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var venue = await db.Venues.FirstOrDefaultAsync(v => v.VenueId == venueId);
        if (venue is null)
        {
            return OperationResult.Fail("Venue not found.");
        }

        venue.IsActive = isActive;
        await db.SaveChangesAsync();
        return OperationResult.Success();
    }

    public async Task<List<VenueImportRow>> PreviewImportAsync(IReadOnlyCollection<(string FileName, string Content)> files)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var sports = await db.Sports.AsNoTracking().Where(s => s.IsActive).ToListAsync();
        var knownSports = sports.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
        var existingKeys = (await db.Venues.AsNoTracking()
                .Select(v => new { v.Name, v.Location })
                .ToListAsync())
            .Select(v => $"{v.Name.Trim().ToLowerInvariant()}|{v.Location.Trim().ToLowerInvariant()}")
            .ToHashSet();
        var rows = new List<VenueImportRow>();

        foreach (var file in files)
        {
            var lines = file.Content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            if (lines.Length == 0 || string.IsNullOrWhiteSpace(lines[0]))
            {
                continue;
            }

            var header = ParseDelimitedLine(lines[0])
                .Select(value => value.Trim().Trim('\uFEFF'))
                .ToList();
            var missingHeaders = new[] { "Name", "Location", "VenueType", "Sports" }
                .Where(required => !header.Any(actual => string.Equals(actual, required, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (missingHeaders.Count > 0)
            {
                rows.Add(new VenueImportRow(1, file.FileName, null, Array.Empty<string>(),
                    $"Missing required header(s): {string.Join(", ", missingHeaders)}."));
                continue;
            }

            var columnIndexes = header
                .Select((name, index) => (Name: name, Index: index))
                .ToDictionary(column => column.Name, column => column.Index, StringComparer.OrdinalIgnoreCase);
            var start = 1;
            for (var index = start; index < lines.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(lines[index]))
                {
                    continue;
                }

                var parsedFields = ParseDelimitedLine(lines[index]);
                var fields = new[]
                {
                    GetField(parsedFields, columnIndexes, "Name"),
                    GetField(parsedFields, columnIndexes, "Location"),
                    GetField(parsedFields, columnIndexes, "Address"),
                    GetField(parsedFields, columnIndexes, "VenueType"),
                    GetField(parsedFields, columnIndexes, "SurfaceType"),
                    GetField(parsedFields, columnIndexes, "Capacity"),
                    GetField(parsedFields, columnIndexes, "Sports")
                };
                var rowNumber = index + 1;
                if (parsedFields.Count < header.Count)
                {
                    rows.Add(new VenueImportRow(rowNumber, file.FileName, null, Array.Empty<string>(),
                        $"Expected {header.Count} columns but found {parsedFields.Count}."));
                    continue;
                }

                var venueTypeText = InputNormaliser.Text(fields[3]);
                var sportNames = fields[6]
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var unknownSports = sportNames.Where(name => !knownSports.ContainsKey(name)).ToList();
                var error = ValidateImportRow(fields, venueTypeText, sportNames, unknownSports);
                if (error is not null)
                {
                    rows.Add(new VenueImportRow(rowNumber, file.FileName, null, sportNames, error));
                    continue;
                }

                _ = Enum.TryParse<VenueType>(venueTypeText, true, out var venueType);
                _ = int.TryParse(fields[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var capacity);
                var venue = new Venue
                {
                    Name = fields[0],
                    Location = fields[1],
                    Address = fields[2],
                    VenueType = venueType,
                    SurfaceType = fields[4],
                    Capacity = string.IsNullOrWhiteSpace(fields[5]) ? null : capacity
                };
                var key = $"{venue.Name.Trim().ToLowerInvariant()}|{venue.Location.Trim().ToLowerInvariant()}";
                var duplicateError = existingKeys.Add(key)
                    ? null
                    : "Duplicate venue in this import or the existing database.";
                rows.Add(new VenueImportRow(rowNumber, file.FileName, venue, sportNames, duplicateError));
            }
        }

        return rows;
    }

    public async Task<OperationResult> ImportAsync(IReadOnlyCollection<VenueImportRow> rows)
    {
        var validRows = rows.Where(row => row.Venue is not null && row.Error is null).ToList();
        if (validRows.Count == 0)
        {
            return OperationResult.Fail("There are no valid rows to import.");
        }

        await using var db = await _factory.CreateDbContextAsync();
        var sports = await db.Sports.Where(s => s.IsActive).ToListAsync();
        var knownSports = sports.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
        var existingKeys = (await db.Venues
                .Select(v => new { v.Name, v.Location })
                .ToListAsync())
            .Select(v => $"{v.Name.Trim().ToLowerInvariant()}|{v.Location.Trim().ToLowerInvariant()}")
            .ToHashSet();
        var importKeys = new HashSet<string>();

        foreach (var row in validRows)
        {
            var venue = row.Venue!;
            venue.Name = InputNormaliser.Text(venue.Name);
            venue.Location = InputNormaliser.Text(venue.Location);
            venue.Address = InputNormaliser.NullIfBlank(venue.Address);
            venue.SurfaceType = InputNormaliser.NullIfBlank(venue.SurfaceType);
            var key = $"{venue.Name.ToLowerInvariant()}|{venue.Location.ToLowerInvariant()}";
            if (!existingKeys.Add(key) || !importKeys.Add(key))
            {
                return OperationResult.Fail($"Duplicate venue in import or database: {venue.Name} ({venue.Location}).");
            }

            foreach (var sportName in row.SportNames)
            {
                venue.VenueSports.Add(new VenueSport { SportId = knownSports[sportName].SportId });
            }

            db.Venues.Add(venue);
        }

        await db.SaveChangesAsync();
        return OperationResult.Success();
    }

    private static string GetField(
        IReadOnlyList<string> fields,
        IReadOnlyDictionary<string, int> columnIndexes,
        string columnName)
    {
        return columnIndexes.TryGetValue(columnName, out var index) && index < fields.Count
            ? fields[index].Trim()
            : string.Empty;
    }

    private static string? ValidateImportRow(
        IReadOnlyList<string> fields,
        string venueTypeText,
        IReadOnlyList<string> sportNames,
        IReadOnlyList<string> unknownSports)
    {
        if (string.IsNullOrWhiteSpace(fields[0]) || string.IsNullOrWhiteSpace(fields[1]))
        {
            return "Name and Location are required.";
        }

        if (!Enum.TryParse<VenueType>(venueTypeText, true, out _))
        {
            return $"VenueType must be one of: {string.Join(", ", Enum.GetNames<VenueType>())}.";
        }

        if (!string.IsNullOrWhiteSpace(fields[5]) &&
            (!int.TryParse(fields[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var capacity) || capacity is < 1 or > 200000))
        {
            return "Capacity must be a whole number between 1 and 200000.";
        }

        if (sportNames.Count == 0)
        {
            return "At least one sport is required.";
        }

        return unknownSports.Count > 0 ? $"Unknown sport(s): {string.Join(", ", unknownSports)}." : null;
    }

    private static List<string> ParseDelimitedLine(string line)
    {
        var delimiter = line.Contains('\t') ? '\t' : line.Contains(';') && !line.Contains(',') ? ';' : ',';
        var fields = new List<string>();
        var field = new System.Text.StringBuilder();
        var quoted = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == delimiter && !quoted)
            {
                fields.Add(field.ToString().Trim());
                field.Clear();
            }
            else
            {
                field.Append(character);
            }
        }

        fields.Add(field.ToString().Trim());
        return fields;
    }
}
