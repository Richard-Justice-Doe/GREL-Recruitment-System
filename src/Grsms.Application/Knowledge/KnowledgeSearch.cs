using System.Text;
using System.Text.RegularExpressions;

namespace Grsms.Application.Knowledge;

/// <summary>
/// docs/36 AI-7: turning a policy document's text into sections, and finding the sections that answer a question.
///
/// The search runs inside GRSMS (BM25 ranking over words, headings counting double) rather than with MySQL full-text search,
/// as first planned: GREL's library is small (tens of documents), the ranking is the same on MySQL and in the tests, and MySQL's
/// full-text search ignores short words (e.g. "sick", "pay" are fine but "HR" and "ID" are not) and any word found in more than
/// half the rows. The index is rebuilt only when a document changes (see <see cref="KnowledgeIndexCache"/>).
/// </summary>
public static partial class KnowledgeText
{
    public const int TargetSectionLength = 1500, MaxSectionLength = 2500;

    [GeneratedRegex(@"^\s*((\d+(\.\d+)*\.?)|([A-Z]\.)|(Section|Article|Chapter|Part|Clause)\s+\w+[.:]?)\s+\S")]
    private static partial Regex Numbered();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Word();

    /// <summary>A heading: a short line that doesn't end like a sentence and is numbered, in capitals, or in title case.</summary>
    public static bool LooksLikeHeading(string line)
    {
        var t = line.Trim();
        if (t.Length is < 3 or > 90 || t.EndsWith('.') && !Numbered().IsMatch(t) || t.EndsWith(',') || t.EndsWith(';')) return false;
        if (Numbered().IsMatch(t) && t.Length <= 80 && !t.TrimEnd().EndsWith('.')) return true;
        var letters = t.Where(char.IsLetter).ToList();
        if (letters.Count < 3) return false;
        if (letters.All(char.IsUpper)) return true;
        var words = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var capitalised = words.Count(w => char.IsUpper(w[0]) || !char.IsLetter(w[0]));
        return words.Length <= 8 && capitalised >= Math.Ceiling(words.Length * 0.7) && !t.EndsWith(':') || t.EndsWith(':') && words.Length <= 6;
    }

    /// <summary>Splits extracted text into sections of about <see cref="TargetSectionLength"/> characters, starting a new one at each heading.</summary>
    public static List<(string Heading, string Text)> Split(string text, string documentTitle)
    {
        var sections = new List<(string Heading, string Text)>();
        var heading = documentTitle;
        var body = new StringBuilder();

        void Flush()
        {
            var t = body.ToString().Trim();
            if (t.Length > 0) sections.Add((heading, t));
            body.Clear();
        }

        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = Regex.Replace(raw, @"\s+", " ").Trim();
            if (line.Length == 0)
            {
                if (body.Length > 0 && body[^1] != '\n') body.Append('\n');
                continue;
            }
            if (LooksLikeHeading(line))
            {
                Flush();
                heading = line.TrimEnd(':').Trim();
                if (heading.Length > 280) heading = heading[..280];
                continue;
            }
            // A long section is cut at a paragraph end once it reaches the target size, and a very long paragraph at sentence
            // ends, so no section passes the maximum.
            foreach (var piece in Pieces(line))
            {
                if (body.Length + piece.Length > MaxSectionLength || body.Length >= TargetSectionLength && body[^1] == '\n') Flush();
                body.Append(piece).Append(piece.Length == line.Length ? '\n' : ' ');
            }
        }
        Flush();

        // The follow-on parts of a long section are named "(continued)" so citations stay clear.
        for (var i = 1; i < sections.Count; i++)
            if (sections[i].Heading == sections[i - 1].Heading.Replace(" (continued)", ""))
                sections[i] = ($"{sections[i].Heading} (continued)", sections[i].Text);
        return sections;
    }

    private static IEnumerable<string> Pieces(string line)
    {
        if (line.Length <= MaxSectionLength / 2) { yield return line; yield break; }
        var sentences = Regex.Split(line, @"(?<=[.!?])\s+");
        var sb = new StringBuilder();
        foreach (var s in sentences)
        {
            if (sb.Length + s.Length > MaxSectionLength / 2 && sb.Length > 0) { yield return sb.ToString().Trim(); sb.Clear(); }
            sb.Append(s).Append(' ');
        }
        if (sb.Length > 0) yield return sb.ToString().Trim();
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "can", "do", "does", "for", "from", "has", "have", "how", "i", "if", "in", "is",
        "it", "its", "me", "my", "of", "on", "or", "our", "should", "so", "that", "the", "their", "them", "there", "this", "to", "was",
        "we", "what", "when", "where", "which", "who", "will", "with", "would", "you", "your", "am", "any", "all", "about", "may", "must",
        "shall", "get", "into", "than", "then", "these", "those", "us", "were", "been", "being", "please", "tell", "know", "grel"
    };

    /// <summary>Lower-case words without common words, lightly stemmed ("days" → "day", "allowances" → "allowance").</summary>
    public static IEnumerable<string> Terms(string text)
    {
        foreach (Match m in Word().Matches(text.ToLowerInvariant()))
        {
            var w = m.Value;
            if (StopWords.Contains(w) || w.Length < 2) continue;
            yield return Stem(w);
        }
    }

    private static string Stem(string w)
    {
        if (w.Length > 4 && w.EndsWith("ies")) return w[..^3] + "y";
        if (w.Length > 5 && w.EndsWith("ing")) return w[..^3];
        if (w.Length > 4 && w.EndsWith("ed") && !w.EndsWith("eed")) return w[..^2];
        if (w.Length > 3 && w.EndsWith('s') && !w.EndsWith("ss") && !w.EndsWith("us")) return w[..^1];
        return w;
    }
}

/// <summary>A section in the index, with what's needed to cite it.</summary>
public sealed record IndexedSection(int SectionId, int DocumentId, string DocumentTitle, string Version, DateOnly EffectiveDate,
    Domain.Knowledge.KnowledgeAudience Audience, string Heading, string Text);

/// <summary>A search hit.</summary>
public sealed record KnowledgeHit(IndexedSection Section, double Score);

/// <summary>An in-memory BM25 index over the active documents' sections.</summary>
public sealed class KnowledgeIndex
{
    private const double K1 = 1.2, B = 0.75, HeadingWeight = 2.0;
    private readonly List<(IndexedSection Section, Dictionary<string, double> Tf, double Length)> _docs = [];
    private readonly Dictionary<string, int> _df = new(StringComparer.Ordinal);
    private readonly double _avgLength;

    public KnowledgeIndex(IEnumerable<IndexedSection> sections)
    {
        foreach (var s in sections)
        {
            var tf = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var t in KnowledgeText.Terms(s.Text)) tf[t] = tf.GetValueOrDefault(t) + 1;
            foreach (var t in KnowledgeText.Terms(s.Heading + " " + s.DocumentTitle)) tf[t] = tf.GetValueOrDefault(t) + HeadingWeight;
            foreach (var t in tf.Keys) _df[t] = _df.GetValueOrDefault(t) + 1;
            _docs.Add((s, tf, tf.Values.Sum()));
        }
        _avgLength = _docs.Count == 0 ? 1 : _docs.Average(d => d.Length);
    }

    public int Count => _docs.Count;

    /// <summary>The best sections for the question, best first; only those sharing at least one meaningful word.</summary>
    public IReadOnlyList<KnowledgeHit> Search(string question, Func<IndexedSection, bool> allowed, int take = 6)
    {
        var terms = KnowledgeText.Terms(question).Distinct().ToList();
        if (terms.Count == 0 || _docs.Count == 0) return [];
        var n = _docs.Count;
        var hits = new List<KnowledgeHit>();
        foreach (var (section, tf, length) in _docs)
        {
            if (!allowed(section)) continue;
            double score = 0;
            foreach (var t in terms)
            {
                if (!tf.TryGetValue(t, out var f)) continue;
                var df = _df[t];
                var idf = Math.Log(1 + (n - df + 0.5) / (df + 0.5));
                score += idf * f * (K1 + 1) / (f + K1 * (1 - B + B * length / _avgLength));
            }
            if (score > 0) hits.Add(new KnowledgeHit(section, score));
        }
        return hits.OrderByDescending(h => h.Score).Take(take).ToList();
    }
}

/// <summary>
/// Keeps the built index between requests (registered as a singleton). It is rebuilt when the list of active documents or any of
/// them changes; the caller passes a stamp made from the documents' ids and update times.
/// </summary>
public sealed class KnowledgeIndexCache
{
    private readonly Lock _lock = new();
    private (string Stamp, KnowledgeIndex Index)? _current;

    public async Task<KnowledgeIndex> GetAsync(string stamp, Func<Task<IEnumerable<IndexedSection>>> load)
    {
        lock (_lock)
            if (_current is { } c && c.Stamp == stamp) return c.Index;
        var index = new KnowledgeIndex(await load());
        lock (_lock) _current = (stamp, index);
        return index;
    }

    public void Clear()
    {
        lock (_lock) _current = null;
    }
}
