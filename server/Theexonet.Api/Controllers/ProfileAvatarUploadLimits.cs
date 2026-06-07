using Theexonet.Core.Validation;

namespace Theexonet.Api.Controllers;

internal static class ProfileAvatarUploadLimits
{
    public const long MaxBytes = ProfileAvatarValidator.MaxBytes;
}
