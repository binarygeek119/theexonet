using Rava.Core.Validation;

namespace Rava.Api.Controllers;

internal static class CompanyLogoUploadLimits
{
    public const long MaxBytes = CompanyLogoValidator.MaxBytes;
}
