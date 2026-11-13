using System.Globalization;
using System.Text;
using Grsms.Application.Common;
using Grsms.Domain.Advertisements;
using Grsms.Domain.Vacancies;

namespace Grsms.Application.Adverts;

/// <summary>
/// Writes advert text: the default advert body built from a vacancy, and the copy-ready text for each external
/// channel (LinkedIn, Indeed, Jobberman, newspapers), which the recruiter pastes into that site or sends to the paper.
/// Plain text only, so it pastes cleanly anywhere.
/// </summary>
public static class AdvertText
{
    public const string Employer = "Ghana Rubber Estates Limited (GREL)";

    private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");

    /// <summary>Default advert body for a vacancy: description, requirements, salary and location.</summary>
    public static string DefaultBody(Vacancy v, string departmentName, string siteName)
    {
        var sb = new StringBuilder();
        sb.AppendLine(v.JobDescription.Trim());
        sb.AppendLine();
        sb.AppendLine("Requirements");

        var education = v.MinimumEducation == EducationLevel.None ? "No minimum" : EnumText.Label(v.MinimumEducation);
        sb.AppendLine($"• Education: {education}{(v.FieldOfStudy is null ? "" : $" in {v.FieldOfStudy}")}");
        if (v.QualificationNotes is not null) sb.AppendLine($"• {v.QualificationNotes}");
        sb.AppendLine(v.MinYearsExperience == 0
            ? $"• Experience: no minimum{(v.ExperienceNotes is null ? "" : $" ({v.ExperienceNotes})")}"
            : $"• Experience: at least {v.MinYearsExperience} year{(v.MinYearsExperience == 1 ? "" : "s")}{(v.ExperienceNotes is null ? "" : $" {v.ExperienceNotes}")}");

        void List(string label, IEnumerable<VacancyRequirement> items)
        {
            var names = items.Select(r => r.Name).ToList();
            if (names.Count > 0) sb.AppendLine($"• {label}: {string.Join(", ", names)}");
        }
        List("Essential skills", v.Requirements.Where(r => r.Type == RequirementType.Skill && r.IsEssential));
        List("Desirable skills", v.Requirements.Where(r => r.Type == RequirementType.Skill && !r.IsEssential));
        List("Required certifications", v.Requirements.Where(r => r.Type == RequirementType.Certification && r.IsEssential));
        List("Desirable certifications", v.Requirements.Where(r => r.Type == RequirementType.Certification && !r.IsEssential));

        sb.AppendLine();
        sb.AppendLine($"Department: {departmentName}");
        sb.AppendLine($"Location: {siteName}");
        sb.AppendLine($"Employment type: {EnumText.Label(v.EmploymentType)}");
        if (v.NumberOfPositions > 1) sb.AppendLine($"Positions: {v.NumberOfPositions}");
        if (v.ShowSalary && Salary(v) is { } salary) sb.AppendLine($"Salary: {salary}");
        return sb.ToString().TrimEnd();
    }

    /// <summary>Default "How to apply" wording.</summary>
    public static string DefaultHowToApply(DateOnly closingDate) =>
        $"Apply online through the GREL careers page by {closingDate.ToString("d MMMM yyyy", Gb)}. Only shortlisted candidates will be contacted.";

    /// <summary>Monthly salary range text, or null when none is set.</summary>
    public static string? Salary(Vacancy v) => (v.SalaryMin, v.SalaryMax) switch
    {
        ({ } min, { } max) => $"GHS {min:N0} – {max:N0} per month",
        ({ } min, null) => $"From GHS {min:N0} per month",
        (null, { } max) => $"Up to GHS {max:N0} per month",
        _ => null
    };

    /// <summary>Copy-ready text for one external channel.</summary>
    public static string ForChannel(AdvertChannel channel, JobAdvert a, string link)
    {
        var closing = a.EndDate.ToString("d MMMM yyyy", Gb);
        var sb = new StringBuilder();

        if (channel == AdvertChannel.Newspaper)
        {
            // Newspapers: a headline block, the text, and the web address instead of a clickable link.
            sb.AppendLine("VACANCY");
            sb.AppendLine(a.Title.ToUpperInvariant());
            sb.AppendLine(Employer);
            sb.AppendLine();
            sb.AppendLine(a.ExternalText ?? a.Body); // docs/36 AI-1: the job-board/newspaper version when written
            sb.AppendLine();
            sb.AppendLine("HOW TO APPLY");
            sb.AppendLine(a.HowToApply);
            sb.AppendLine($"Apply at: {link}");
            sb.AppendLine($"Closing date: {closing}");
            sb.AppendLine($"Ref: {a.AdvertNumber}");
            return sb.ToString().TrimEnd();
        }

        // Job sites (LinkedIn, Indeed, Jobberman): title and company go in their own form fields,
        // so the text starts with a short intro line and ends with the apply link.
        sb.AppendLine($"{Employer} is recruiting: {a.Title}");
        sb.AppendLine();
        // docs/36 AI-1: LinkedIn uses its own post when written; Indeed and Jobberman the job-board version.
        sb.AppendLine((channel == AdvertChannel.LinkedIn ? a.LinkedInText : a.ExternalText) ?? a.Body);
        sb.AppendLine();
        sb.AppendLine("How to apply");
        sb.AppendLine(a.HowToApply);
        sb.AppendLine($"Apply here: {link}");
        sb.AppendLine($"Closing date: {closing}");
        sb.AppendLine($"Reference: {a.AdvertNumber}");
        return sb.ToString().TrimEnd();
    }
}
