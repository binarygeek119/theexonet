namespace Theexonet.Core.Configuration;

public class AiImageQueueOptions
{
    public const string SectionName = "AiImageQueue";

    public bool Enabled { get; set; } = true;

    /// <summary>Delay between image jobs to reduce OpenAI rate pressure.</summary>
    public int SecondsBetweenJobs { get; set; } = 3;
}
