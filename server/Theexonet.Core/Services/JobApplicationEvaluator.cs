namespace Theexonet.Core.Services;

/// <summary>
/// Detects whether a player must complete the Universal Employment Exchange job application once.
/// </summary>
public static class JobApplicationEvaluator
{
    public static bool IsRequired(DateTime? jobApplicationCompletedAt) =>
        jobApplicationCompletedAt is null;
}
