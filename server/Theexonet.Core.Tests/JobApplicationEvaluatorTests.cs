using Theexonet.Core.Services;

namespace Theexonet.Core.Tests;

public class JobApplicationEvaluatorTests
{
    [Fact]
    public void IsRequired_WhenNotCompleted()
    {
        Assert.True(JobApplicationEvaluator.IsRequired(null));
    }

    [Fact]
    public void IsRequired_WhenCompleted()
    {
        Assert.False(JobApplicationEvaluator.IsRequired(DateTime.UtcNow));
    }
}
