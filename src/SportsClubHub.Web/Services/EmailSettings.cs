using System.Net.Mail;

namespace SportsClubHub.Web.Services;

public enum EmailState { Disabled, NeedsSetup, Ready }

/// <summary>What the admin area shows about e-mail. Never contains a secret, only the names of missing settings.</summary>
public sealed record EmailStatus(EmailState State, IReadOnlyList<string> MissingSettings);

/// <summary>SMTP settings read from configuration (user-secrets or environment variables such as <c>Email__Host</c>).</summary>
public sealed record EmailSettings(
    bool Enabled,
    string? Host,
    int Port,
    string? Username,
    string? Password,
    string? FromAddress,
    string FromName,
    int TimeoutSeconds)
{
    public static EmailSettings From(IConfiguration configuration)
    {
        var username = Clean(configuration["Email:Username"]);

        // Google shows app passwords as four groups separated by spaces; the spaces are not part of the password.
        var password = configuration["Email:Password"];
        password = password is null ? null : new string(password.Where(c => !char.IsWhiteSpace(c)).ToArray());

        return new EmailSettings(
            configuration.GetValue<bool>("Email:Enabled"),
            Clean(configuration["Email:Host"]),
            configuration.GetValue<int?>("Email:Port") ?? 587,
            username,
            string.IsNullOrEmpty(password) ? null : password,
            Clean(configuration["Email:FromAddress"]) ?? username,
            Clean(configuration["Email:FromName"]) ?? "Sports Club Hub SA",
            Math.Clamp(configuration.GetValue<int?>("Email:TimeoutSeconds") ?? 15, 5, 60));
    }

    /// <summary>Names of required settings that are absent or unusable.</summary>
    public IReadOnlyList<string> Missing
    {
        get
        {
            var missing = new List<string>();
            if (Host is null) { missing.Add("Host"); }
            if (Username is null) { missing.Add("Username"); }
            if (Password is null) { missing.Add("Password"); }
            if (FromAddress is null) { missing.Add("FromAddress"); }
            else if (!MailAddress.TryCreate(FromAddress, out _)) { missing.Add("FromAddress (not a valid address)"); }
            return missing;
        }
    }

    public bool IsComplete => Missing.Count == 0;

    public EmailStatus ToStatus() =>
        !Enabled ? new EmailStatus(EmailState.Disabled, Array.Empty<string>())
        : IsComplete ? new EmailStatus(EmailState.Ready, Array.Empty<string>())
        : new EmailStatus(EmailState.NeedsSetup, Missing);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Small text helpers that keep e-mail safe and keep personal data out of logs.</summary>
public static class EmailText
{
    /// <summary>A recipient written for logs, such as "j***@example.com".</summary>
    public static string Mask(string? address)
    {
        if (string.IsNullOrWhiteSpace(address) || !MailAddress.TryCreate(address.Trim(), out var parsed))
        {
            return "(invalid address)";
        }

        return parsed.User.Length == 0 ? "***@" + parsed.Host : parsed.User[0] + "***@" + parsed.Host;
    }

    /// <summary>One line only: line breaks in a subject can be abused to inject extra mail headers.</summary>
    public static string CleanSubject(string? subject)
    {
        var line = string.Join(' ', (subject ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
        return line.Length > 200 ? line[..200] : line;
    }

    /// <summary>The bare address when the text is a single valid e-mail address.</summary>
    public static bool TryNormaliseAddress(string? text, out string address)
    {
        address = string.Empty;
        if (string.IsNullOrWhiteSpace(text) || !MailAddress.TryCreate(text.Trim(), out var parsed) ||
            parsed.Address != text.Trim() || text.Contains(','))
        {
            return false;
        }

        address = parsed.Address;
        return true;
    }
}
