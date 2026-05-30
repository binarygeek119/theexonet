using Rava.Core.Validation;

namespace Rava.Api.Controllers;

internal static class ProfileAvatarUploadLimits
{
    public const long MaxBytes = ProfileAvatarValidator.MaxBytes;
}
