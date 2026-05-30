namespace Rava.Core.Configuration;

public class HateSpeechOptions
{
    public const string SectionName = "HateSpeech";

    public bool Enabled { get; set; } = true;

    public string[] Terms { get; set; } = [];
}
