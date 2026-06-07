using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;

namespace Theexonet.Core.Services;

/// <summary>
/// Detects profile fields that existing players must fill after new DB columns ship.
/// Add new checks here when a field becomes required for everyone.
/// </summary>
public static class ProfileCompletionEvaluator
{
    public const string FieldGender = "gender";
    public const string FieldPreferredPronouns = "preferredPronouns";
    public const string FieldLocale = "locale";

    public static ProfileCompletionStatus Evaluate(string? gender, string? preferredPronouns, string? locale)
    {
        var missing = new List<ProfileCompletionFieldDto>();
        var normalizedGender = ProfileGender.Normalize(gender);

        if (string.IsNullOrEmpty(normalizedGender))
        {
            missing.Add(new ProfileCompletionFieldDto(FieldGender));
        }
        else if (ProfileGender.RequiresPreferredPronouns(normalizedGender) &&
                 string.IsNullOrEmpty(ProfilePreferredPronouns.Normalize(preferredPronouns)))
        {
            missing.Add(new ProfileCompletionFieldDto(FieldPreferredPronouns));
        }

        if (string.IsNullOrEmpty(ProfileLocale.Normalize(locale)))
        {
            missing.Add(new ProfileCompletionFieldDto(FieldLocale));
        }

        return new ProfileCompletionStatus(missing.Count > 0, missing);
    }
}

public sealed record ProfileCompletionStatus(
    bool Required,
    IReadOnlyList<ProfileCompletionFieldDto> MissingFields);
