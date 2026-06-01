using Rava.Core.Validation;

namespace Rava.Api.Controllers;

internal static class ProfileBackgroundUploadLimits
{
    public const long MaxBytes = ProfileBackgroundValidator.MaxBytes;
}
