using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;

namespace Theexonet.Infrastructure.Services;

public class HateSpeechScanner(
    IOptionsMonitor<HateSpeechOptions> options,
    HateSpeechTermsProvider termsProvider)
{
    public (bool IsMatch, string MatchedTerms) Scan(string body)
    {
        if (!options.CurrentValue.Enabled || string.IsNullOrWhiteSpace(body))
        {
            return (false, string.Empty);
        }

        var matches = new List<string>();
        foreach (var term in termsProvider.GetTerms())
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                continue;
            }

            if (ContainsTerm(body, term.Trim()))
            {
                matches.Add(term.Trim());
            }
        }

        if (matches.Count == 0)
        {
            return (false, string.Empty);
        }

        return (true, string.Join(", ", matches.Distinct(StringComparer.OrdinalIgnoreCase)));
    }

    private static bool ContainsTerm(string body, string term)
    {
        var pattern = $@"\b{Regex.Escape(term)}\b";
        return Regex.IsMatch(body, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
