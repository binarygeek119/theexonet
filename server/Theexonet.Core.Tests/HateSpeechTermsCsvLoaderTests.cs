using Theexonet.Core.Configuration;

namespace Theexonet.Core.Tests;

public class HateSpeechTermsCsvLoaderTests
{
    [Fact]
    public void Parse_skips_header_and_comments()
    {
        const string csv = """
            Term,Notes
            # ignored comment
            badword,example
            another,
            """;

        var terms = HateSpeechTermsCsvLoader.Parse(csv);

        Assert.Equal(["badword", "another"], terms);
    }

    [Fact]
    public void Parse_deduplicates_case_insensitively()
    {
        const string csv = """
            Term
            Alpha
            alpha
            """;

        var terms = HateSpeechTermsCsvLoader.Parse(csv);

        Assert.Single(terms);
        Assert.Equal("Alpha", terms[0]);
    }

    [Fact]
    public void Parse_merges_multiple_lists_without_duplicates()
    {
        var hateSpeech = HateSpeechTermsCsvLoader.Parse("""
            Term
            slur
            shared
            """);

        var badLanguage = HateSpeechTermsCsvLoader.Parse("""
            Term
            profanity
            shared
            """);

        var merged = hateSpeech
            .Concat(badLanguage)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(["slur", "shared", "profanity"], merged);
    }
}
