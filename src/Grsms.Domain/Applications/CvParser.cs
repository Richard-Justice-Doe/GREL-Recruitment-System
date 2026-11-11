using System.Globalization;
using System.Text.RegularExpressions;
using Grsms.Domain.Vacancies;

namespace Grsms.Domain.Applications;

/// <summary>One job found in a CV's employment history.</summary>
/// <param name="Description">The line it was found on (job title and/or employer), without the dates.</param>
/// <param name="From">First month (1st of the month).</param>
/// <param name="To">Last month; today for "present".</param>
/// <param name="IsCurrent">True for "present", "current", "to date", "now".</param>
public record CvJob(string Description, DateOnly From, DateOnly To, bool IsCurrent)
{
    public double Years => Math.Max(0, (To.DayNumber - From.DayNumber) / 365.25);
}

/// <summary>
/// Phase 15: what the ATS read from a CV file. Stored as JSON on the application (JobApplications.CvParsed).
/// Every value can be wrong (CVs have no fixed layout), so staff see it beside what the applicant typed in their profile.
/// </summary>
public record ParsedCv
{
    /// <summary>Format version, so older stored results can be told apart.</summary>
    public int Version { get; init; } = 1;

    /// <summary>"rules" (GRSMS's own reader) or "ai" (the optional Claude reader).</summary>
    public string Reader { get; init; } = "rules";

    public string? FullName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }

    /// <summary>The highest level the CV mentions (None if none found).</summary>
    public EducationLevel HighestEducation { get; init; }

    /// <summary>Lines of the education section (qualifications and schools).</summary>
    public IReadOnlyList<string> Education { get; init; } = [];

    /// <summary>Whole years of work experience worked out from the employment history (overlapping jobs counted once).</summary>
    public int? YearsExperience { get; init; }

    public IReadOnlyList<CvJob> EmploymentHistory { get; init; } = [];
    public IReadOnlyList<string> Skills { get; init; } = [];
    public IReadOnlyList<string> Certifications { get; init; } = [];

    /// <summary>Things staff should know, e.g. "No text could be read: the CV may be a scanned image."</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Phase 15: GRSMS's own CV reader. It works on the plain text of a CV (taken from the PDF or Word file) with
/// published rules, so nothing leaves GREL and every result can be explained:
///
///   * Email and phone: the first email address and the first phone number (Ghana or international format).
///   * Name: a "Name:" line, otherwise the first short line near the top made only of words (not "Curriculum Vitae").
///   * Sections: lines that are headings such as "Education", "Work experience", "Skills", "Certifications"
///     (with or without a colon, any capitals). A section runs until the next heading.
///   * Education level: the highest qualification word in the education section (e.g. "BSc", "HND", "WASSCE").
///   * Employment history: every date range in the experience section ("Jan 2019 – Present", "2015 - 2018",
///     "03/2017 to 12/2020"), with the text on that line or the line above as the job. Years of experience =
///     the total time covered, with overlapping jobs counted once.
///   * Skills and certifications: the items in those sections (split on bullets, commas and semicolons).
/// </summary>
public static partial class CvParser
{
    private const int MaxItems = 50;

    public static ParsedCv Parse(string? text, DateOnly today)
    {
        var lines = (text ?? "").Replace("\r", "").Split('\n')
            .Select(l => Spaces().Replace(l.Replace('\t', ' '), " ").Trim())
            .Where(l => l.Length > 0)
            .ToList();
        if (lines.Sum(l => l.Length) < 40)
            return new ParsedCv { Warnings = ["No text could be read from the CV. It may be a scanned image; check the file itself."] };

        var all = string.Join("\n", lines);
        var sections = SplitSections(lines);
        var warnings = new List<string>();

        var educationLines = sections.GetValueOrDefault(Section.Education) ?? [];
        var level = HighestLevel(educationLines.Count > 0 ? educationLines : lines, strict: educationLines.Count == 0);

        var experienceLines = sections.GetValueOrDefault(Section.Experience) ?? [];
        var jobs = FindJobs(experienceLines.Count > 0 ? experienceLines : [], today);
        if (experienceLines.Count == 0) warnings.Add("No work experience section was found.");
        else if (jobs.Count == 0) warnings.Add("The work experience section has no dates GRSMS could read, so years of experience were not worked out.");

        return new ParsedCv
        {
            FullName = FindName(lines),
            Email = EmailRegex().Match(all) is { Success: true } e ? e.Value.ToLowerInvariant() : null,
            Phone = PhoneRegex().Match(all) is { Success: true } p ? Spaces().Replace(p.Value.Trim(), " ") : null,
            HighestEducation = level,
            Education = educationLines.Take(MaxItems).ToList(),
            EmploymentHistory = jobs,
            YearsExperience = jobs.Count == 0 ? null : (int)Math.Floor(TotalYears(jobs)),
            Skills = Items(sections.GetValueOrDefault(Section.Skills)),
            Certifications = Items(sections.GetValueOrDefault(Section.Certifications)),
            Warnings = warnings
        };
    }

    // ===================================================================== Sections

    private enum Section { Other, Education, Experience, Skills, Certifications }

    /// <summary>Heading words → section. Anything else that looks like a heading ends the current section.</summary>
    private static readonly (Section Section, string[] Headings)[] Headings =
    [
        (Section.Education, ["education", "educational background", "educational qualifications", "academic qualifications",
            "academic background", "academic history", "education and qualifications", "education & qualifications", "academic record"]),
        (Section.Experience, ["experience", "work experience", "working experience", "professional experience", "employment history",
            "employment", "employment record", "career history", "work history", "relevant experience", "job experience"]),
        (Section.Skills, ["skills", "key skills", "core skills", "technical skills", "skills and abilities", "skills & abilities",
            "competencies", "core competencies", "key competencies", "areas of expertise", "skills summary", "computer skills"]),
        (Section.Certifications, ["certifications", "certification", "certificates", "professional certifications", "licences",
            "licenses", "licences and certifications", "licenses and certifications", "certifications and licences",
            "professional qualifications", "trainings", "training", "training and certifications", "courses and certifications"]),
        (Section.Other, ["references", "referees", "personal details", "personal information", "profile", "personal profile",
            "summary", "professional summary", "career objective", "objective", "hobbies", "interests", "hobbies and interests",
            "languages", "achievements", "awards", "publications", "projects", "volunteer experience", "declaration",
            "personal statement", "contact", "contact details", "memberships", "professional memberships"]),
    ];

    private static Dictionary<Section, List<string>> SplitSections(List<string> lines)
    {
        var result = new Dictionary<Section, List<string>>();
        Section? current = null;
        foreach (var line in lines)
        {
            if (IsHeading(line, out var section, out var rest))
            {
                current = section;
                if (!result.ContainsKey(section)) result[section] = [];
                if (rest is not null) result[section].Add(rest); // "Skills: Excel, Word"
                continue;
            }
            if (current is { } c) result[c].Add(line);
        }
        return result;
    }

    private static bool IsHeading(string line, out Section section, out string? rest)
    {
        section = Section.Other;
        rest = null;
        var head = line;
        var colon = line.IndexOf(':');
        if (colon > 0 && colon < 45)
        {
            head = line[..colon];
            rest = line[(colon + 1)..].Trim() is { Length: > 0 } r ? r : null;
        }
        var key = HeadingClean().Replace(head.ToLowerInvariant(), " ").Trim();
        if (key.Length == 0 || key.Split(' ').Length > 5) return false;
        foreach (var (s, words) in Headings)
            if (words.Contains(key))
            {
                section = s;
                return true;
            }
        rest = null;
        return false;
    }

    // ===================================================================== Name, education

    private static string? FindName(List<string> lines)
    {
        foreach (var line in lines.Take(40))
            if (NameLabel().Match(line) is { Success: true } m && LooksLikeName(m.Groups[1].Value.Trim()))
                return Title(m.Groups[1].Value.Trim());
        foreach (var line in lines.Take(6))
            if (LooksLikeName(line) && !line.Contains("curriculum", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("resume", StringComparison.OrdinalIgnoreCase) && !line.Contains("vitae", StringComparison.OrdinalIgnoreCase) &&
                !IsHeading(line, out _, out _))
                return Title(line);
        return null;
    }

    private static bool LooksLikeName(string s)
    {
        var words = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length is >= 2 and <= 5 && s.Length <= 60 && words.All(w => NameWord().IsMatch(w));
    }

    /// <summary>"KWAME  MENSAH" → "Kwame Mensah"; mixed case is kept as written.</summary>
    private static string Title(string s) =>
        s == s.ToUpperInvariant() ? CultureInfo.GetCultureInfo("en-GB").TextInfo.ToTitleCase(s.ToLowerInvariant()) : s;

    /// <summary>Qualification words for each level, highest first. Short ones (e.g. "ma") only count inside an education section.</summary>
    private static readonly (EducationLevel Level, string[] Words, string[] StrictOnly)[] Levels =
    [
        (EducationLevel.Doctorate, ["phd", "ph d", "doctorate", "doctor of philosophy", "dphil", "d phil"], []),
        (EducationLevel.Masters, ["msc", "m sc", "mba", "mphil", "m phil", "master", "masters", "master s", "meng", "m eng", "llm", "emba", "m ed", "med"], ["ma", "m a", "med"]),
        (EducationLevel.Bachelors, ["bsc", "b sc", "bachelor", "bachelors", "bachelor s", "beng", "b eng", "b ed", "bed", "llb", "bcom", "b com", "bba", "first degree", "degree"], ["ba", "b a", "bed", "degree"]),
        (EducationLevel.Hnd, ["hnd", "higher national diploma"], []),
        (EducationLevel.Diploma, ["diploma"], []),
        (EducationLevel.Certificate, ["certificate"], ["certificate"]),
        (EducationLevel.Secondary, ["wassce", "sssce", "waec", "senior high", "senior secondary", "shs", "gce", "o level", "a level"], ["shs"]),
        (EducationLevel.Basic, ["bece", "junior high", "jhs", "basic education"], ["jhs"]),
    ];

    /// <param name="strict">True when searching the whole CV (no education section): ambiguous short words are skipped.</param>
    private static EducationLevel HighestLevel(IEnumerable<string> lines, bool strict)
    {
        var text = " " + HeadingClean().Replace(string.Join(" ", lines).ToLowerInvariant(), " ") + " ";
        text = Spaces().Replace(text, " ");
        foreach (var (level, words, strictOnly) in Levels)
            if (words.Any(w => !(strict && strictOnly.Contains(w)) && text.Contains($" {w} ")))
                return level;
        return EducationLevel.None;
    }

    // ===================================================================== Employment history

    private static List<CvJob> FindJobs(List<string> lines, DateOnly today)
    {
        var jobs = new List<CvJob>();
        for (var i = 0; i < lines.Count; i++)
        {
            foreach (Match m in DateRange().Matches(lines[i]))
            {
                if (ReadDate(m.Groups["from"].Value, start: true) is not { } from) continue;
                var toText = m.Groups["to"].Value;
                var current = CurrentWords().IsMatch(toText);
                var to = current ? today : ReadDate(toText, start: false);
                if (to is null || to < from || from > today || from.Year < today.Year - 60) continue;
                if (to > today) to = today;

                var description = Spaces().Replace(DateRange().Replace(lines[i], " "), " ").Trim(' ', '-', '–', '—', '|', ',', '(', ')', ':');
                if (description.Length < 3 && i > 0) description = lines[i - 1];
                jobs.Add(new CvJob(description.Length > 200 ? description[..200] : description, from, to.Value, current));
            }
        }
        return jobs.Take(MaxItems).ToList();
    }

    /// <summary>Total time covered by the jobs, with overlapping periods counted once.</summary>
    public static double TotalYears(IEnumerable<CvJob> jobs)
    {
        var days = 0;
        DateOnly? start = null, end = null;
        foreach (var j in jobs.OrderBy(j => j.From))
        {
            if (start is null) { start = j.From; end = j.To; continue; }
            if (j.From <= end) { if (j.To > end) end = j.To; continue; }
            days += end!.Value.DayNumber - start.Value.DayNumber;
            start = j.From;
            end = j.To;
        }
        if (start is not null) days += end!.Value.DayNumber - start.Value.DayNumber;
        return days / 365.25;
    }

    private static readonly string[] Months = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];

    /// <summary>"Jan 2019", "January 2019", "01/2019", "2019" → a date (start = 1st of the month; a bare year = January or December).</summary>
    private static DateOnly? ReadDate(string s, bool start)
    {
        s = s.Trim().ToLowerInvariant().Replace(".", "");
        var year = YearRegex().Match(s);
        if (!year.Success) return null;
        var y = int.Parse(year.Value, CultureInfo.InvariantCulture);
        var month = 0;
        var numeric = NumericMonth().Match(s);
        if (numeric.Success) month = int.Parse(numeric.Groups[1].Value, CultureInfo.InvariantCulture);
        else
            for (var i = 0; i < 12; i++)
                if (s.StartsWith(Months[i], StringComparison.Ordinal)) { month = i + 1; break; }
        if (month is < 0 or > 12) return null;
        if (month == 0) month = start ? 1 : 12;
        var d = new DateOnly(y, month, 1);
        return start ? d : d.AddMonths(1).AddDays(-1);
    }

    // ===================================================================== Lists

    private static IReadOnlyList<string> Items(List<string>? lines) =>
        (lines ?? [])
            .SelectMany(l => ItemSplit().Split(l))
            .Select(i => i.Trim(' ', '.', '-', '•', '*', '·', '–', '▪', '●', '○', '■', '◦', '>'))
            .Where(i => i.Length is >= 2 and <= 80)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxItems)
            .ToList();

    // ===================================================================== Patterns

    [GeneratedRegex(@"\s+")] private static partial Regex Spaces();
    [GeneratedRegex(@"[^\p{L}\p{N}&]+")] private static partial Regex HeadingClean();
    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")] private static partial Regex EmailRegex();
    // Ghana (+233 / 0 followed by 9 digits, any spacing) or any international number of 9-15 digits.
    [GeneratedRegex(@"(?<![\d+])(?:\+?233[\s\-]?\(?0?\)?[\s\-]?|0)\d{2}[\s\-]?\d{3}[\s\-]?\d{4}(?!\d)|\+\d[\d\s\-]{8,16}\d")] private static partial Regex PhoneRegex();
    [GeneratedRegex(@"^(?:full\s+)?name\s*[:\-]\s*(.+)$", RegexOptions.IgnoreCase)] private static partial Regex NameLabel();
    [GeneratedRegex(@"^[\p{Lu}][\p{L}'\-\.]*$|^[\p{Lu}]\.?$")] private static partial Regex NameWord();
    [GeneratedRegex(@"(?:19|20)\d{2}")] private static partial Regex YearRegex();
    [GeneratedRegex(@"^(\d{1,2})\s*[/\-]")] private static partial Regex NumericMonth();
    [GeneratedRegex(@"present|current|to\s*date|^\s*date\s*$|now|till\s*date|ongoing", RegexOptions.IgnoreCase)] private static partial Regex CurrentWords();
    [GeneratedRegex(@"\s*[•·▪●○■◦;|]\s*|,\s+|\s+-\s+")] private static partial Regex ItemSplit();

    // A month-year (or bare year) range: "Jan 2019 – Present", "January 2015 to March 2018", "2015 - 2019", "03/2017 - 12/2020".
    [GeneratedRegex(
        @"(?<from>(?:(?:jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)[a-z]*\.?\s*,?\s*|\d{1,2}\s*[/\-]\s*)?(?:19|20)\d{2})\s*(?:-|–|—|to|until|till)\s*(?<to>(?:(?:jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)[a-z]*\.?\s*,?\s*|\d{1,2}\s*[/\-]\s*)?(?:19|20)\d{2}|present|current|to\s*date|date|now|till\s*date|ongoing)",
        RegexOptions.IgnoreCase)]
    private static partial Regex DateRange();
}
