using Umbraco.Cms.Core;

namespace Umbraco.Community.ArchivedUsers.Web.Controllers;

public enum ReasonError
{
    Required,
    TooLong,
}

/// <summary>
/// Trims the reason; empty after trimming becomes null. Over 500 characters is rejected, never truncated.
/// The text is stored as sent: escaping is the renderer's job (AU-04).
/// </summary>
public static class ReasonValidator
{
    public const int MaxLength = 500;

    public static Attempt<string?, ReasonError> Normalise(string? raw, bool required)
    {
        string? value = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        if (value is null)
        {
            return required ? Attempt.FailWithStatus<string?, ReasonError>(ReasonError.Required, null) : Attempt.SucceedWithStatus<string?, ReasonError>(default, null);
        }

        return value.Length > MaxLength
            ? Attempt.FailWithStatus<string?, ReasonError>(ReasonError.TooLong, null)
            : Attempt.SucceedWithStatus<string?, ReasonError>(default, value);
    }
}
