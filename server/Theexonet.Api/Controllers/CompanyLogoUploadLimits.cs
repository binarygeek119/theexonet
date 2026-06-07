using Theexonet.Core.Validation;

namespace Theexonet.Api.Controllers;

internal static class CompanyLogoUploadLimits
{
    public const long MaxBytes = CompanyLogoValidator.MaxBytes;
}
