namespace Rava.Core.Configuration;

public class HateSpeechOptions
{
    public const string SectionName = "HateSpeech";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// CSV spreadsheet filename in the API content root (first column = term). Opens in Excel.
    /// </summary>
    public string TermsFile { get; set; } = "hate-speech-terms.csv";

    /// <summary>
    /// CSV spreadsheet for profanity and other bad language (same format as TermsFile).
    /// </summary>
    public string BadLanguageFile { get; set; } = "bad-language-terms.csv";

    /// <summary>
    /// CSV spreadsheet for political terms (same format as TermsFile).
    /// </summary>
    public string PoliticalTermsFile { get; set; } = "political-terms.csv";

    /// <summary>
    /// CSV spreadsheet for pornographic or sexual terms (same format as TermsFile).
    /// </summary>
    public string SexualTermsFile { get; set; } = "sexual-terms.csv";

    /// <summary>
    /// Legacy fallback when the CSV files are missing or empty.
    /// </summary>
    public string[] Terms { get; set; } = [];
}
