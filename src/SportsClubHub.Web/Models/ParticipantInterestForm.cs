using System.ComponentModel.DataAnnotations;
using SportsClubHub.Domain.Common;

namespace SportsClubHub.Web.Models;

/// <summary>
/// What the "register your interest" page collects. It is separate from the Participant entity
/// because it also holds the two confirmations, which are not stored as columns.
/// </summary>
public class ParticipantInterestForm
{
    private string? _phone;

    [Required(ErrorMessage = "Please enter your name."), StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your e-mail address."), EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    public string? Phone
    {
        get => _phone;
        set => _phone = InputNormaliser.NullIfBlank(value);
    }

    [Required(ErrorMessage = "Please enter your town or area."), StringLength(120)]
    public string Location { get; set; } = string.Empty;

    // [Range(typeof(bool), "true", "true")] is the usual way to make a check box mandatory.
    [Range(typeof(bool), "true", "true", ErrorMessage = "Please confirm that you are 18 or older.")]
    public bool ConfirmedAdult { get; set; }

    [Range(typeof(bool), "true", "true", ErrorMessage = "Please accept the privacy notice to continue.")]
    public bool AcceptedPrivacyNotice { get; set; }
}
