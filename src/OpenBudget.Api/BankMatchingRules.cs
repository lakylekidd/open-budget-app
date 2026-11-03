namespace OpenBudget.Api;

public static class BankMatchingRules
{
    public static decimal PayeeSimilarity(string? left, string? right)
    {
        var a = Normalize(left);
        var b = Normalize(right);
        if (a.Length == 0 || b.Length == 0) return 0;
        if (a == b) return 1;
        if (a.Length < 2 || b.Length < 2) return 0;
        var pairsA = Bigrams(a);
        var pairsB = Bigrams(b);
        var intersection = 0;
        foreach (var pair in pairsA)
        {
            var match = pairsB.IndexOf(pair);
            if (match < 0) continue;
            intersection++;
            pairsB.RemoveAt(match);
        }
        return decimal.Round(2m * intersection / (pairsA.Count + pairsB.Count + intersection), 4);
    }

    public static decimal CandidateScore(DateOnly importedDate, decimal importedAmount, string importedPayee,
        DateOnly existingDate, decimal existingAmount, string existingPayee)
    {
        if (decimal.Round(importedAmount, 2) != decimal.Round(existingAmount, 2)) return 0;
        var days = Math.Abs(importedDate.DayNumber - existingDate.DayNumber);
        if (days > 4) return 0;
        var payee = PayeeSimilarity(importedPayee, existingPayee);
        if (payee < 0.35m) return 0;
        var date = (4 - days) / 4m;
        return decimal.Round(0.5m + 0.18m * date + 0.32m * payee, 4);
    }

    private static List<string> Bigrams(string value)
    {
        var pairs = new List<string>(value.Length - 1);
        for (var index = 0; index < value.Length - 1; index++) pairs.Add(value.Substring(index, 2));
        return pairs;
    }

    private static string Normalize(string? value) => new((value ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
