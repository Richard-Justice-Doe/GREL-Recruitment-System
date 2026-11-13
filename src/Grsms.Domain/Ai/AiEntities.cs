using System.ComponentModel.DataAnnotations;
using Grsms.Domain.Common;

namespace Grsms.Domain.Ai;

/// <summary>13 November 2026 (docs/36): the AI services GRSMS can use, chosen in Administration → AI settings &amp; usage.</summary>
public enum AiProvider
{
    [Display(Name = "Anthropic (Claude)")] Anthropic,
    [Display(Name = "OpenAI")] OpenAI,
    [Display(Name = "Azure OpenAI")] AzureOpenAI
}

/// <summary>
/// docs/36: every AI feature, each with its own on/off switch (all off by default). <see cref="AiFeatures.SendsPersonalData"/>
/// says which ones send personal data to the provider (decision of 13 November 2026: allowed, each behind its own switch).
/// </summary>
public enum AiFeature
{
    [Display(Name = "Job description drafts")] JobDescription,
    [Display(Name = "Advert versions")] Advert,
    [Display(Name = "Interview question sets")] InterviewQuestions,
    [Display(Name = "Executive dashboard summary")] ExecutiveSummary,
    [Display(Name = "Interview summaries")] InterviewSummary,
    [Display(Name = "CV match summaries")] CvSummary,
    [Display(Name = "CV reading")] CvReading,
    [Display(Name = "Onboarding assistant")] OnboardingAssistant,
    [Display(Name = "Knowledge base answers")] KnowledgeAnswer,
    [Display(Name = "Document type check")] DocumentCheck,
    [Display(Name = "Connection test")] ConnectionTest
}

public static class AiFeatures
{
    /// <summary>The feature's display name.</summary>
    public static string Label(AiFeature f) =>
        typeof(AiFeature).GetField(f.ToString())?.GetCustomAttributes(typeof(DisplayAttribute), false).OfType<DisplayAttribute>().FirstOrDefault()?.Name
        ?? f.ToString();

    /// <summary>Features that send personal data (CVs, interview comments, a person's status, ID images) to the provider.</summary>
    public static bool SendsPersonalData(AiFeature f) =>
        f is AiFeature.InterviewSummary or AiFeature.CvSummary or AiFeature.CvReading or AiFeature.OnboardingAssistant or AiFeature.DocumentCheck;

    /// <summary>What each feature sends, shown next to its switch.</summary>
    public static string WhatIsSent(AiFeature f) => f switch
    {
        AiFeature.JobDescription => "Job title, department, grade and the responsibilities typed in. No personal data.",
        AiFeature.Advert => "The vacancy's title, description, requirements, benefits and closing date. No personal data.",
        AiFeature.InterviewQuestions => "The vacancy's description, requirements, department and level. No personal data.",
        AiFeature.ExecutiveSummary => "Totals and trends only (counts, days, percentages per department). No names.",
        AiFeature.InterviewSummary => "Validated scorecards and panel comments about one candidate.",
        AiFeature.CvSummary => "The candidate's CV text and the ATS results.",
        AiFeature.CvReading => "The text of every CV received (replaces the earlier \"Claude CV reader\" switch).",
        AiFeature.OnboardingAssistant => "The person's question and their own onboarding status (documents, steps, dates).",
        AiFeature.KnowledgeAnswer => "The question and the matching sections of GREL's policy documents.",
        AiFeature.DocumentCheck => "Images of uploaded ID documents and certificates.",
        _ => "A short test message."
    };
}

/// <summary>One call to the AI provider (or one refused before calling). Not in the audit trail: this is its own log.</summary>
public class AiRequestLog : BaseEntity
{
    public AiFeature Feature { get; set; }
    public string? UserId { get; set; }
    public AiProvider Provider { get; set; }
    public string Model { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }

    /// <summary>Worked out from the prices in the settings; 0 when no prices are set.</summary>
    public decimal CostUsd { get; set; }

    public int DurationMs { get; set; }
    public AiOutcome Outcome { get; set; }

    /// <summary>Short reason when it failed or was refused (never the API key).</summary>
    public string? Error { get; set; }

    /// <summary>The record it was about, e.g. "Vacancy" 12.</summary>
    public string? RecordType { get; set; }
    public int? RecordId { get; set; }

    /// <summary>The instructions and input sent, kept only when "Keep prompts for troubleshooting" is on (deleted after 30 days).</summary>
    public string? Prompt { get; set; }
}

public enum AiOutcome
{
    [Display(Name = "Answered")] Success,
    [Display(Name = "From cache")] CacheHit,
    [Display(Name = "Failed")] Failed,
    [Display(Name = "Answer not usable")] BadAnswer,
    [Display(Name = "Switched off")] Disabled,
    [Display(Name = "Budget reached")] BudgetReached,
    [Display(Name = "Too many requests")] RateLimited
}

/// <summary>A cached answer of a feature that sends no personal data (generators), keyed by a hash of what was asked.</summary>
public class AiCacheEntry : BaseEntity
{
    /// <summary>SHA-256 (hex) of feature + prompt version + model + instructions + input. Unique.</summary>
    public string Key { get; set; } = string.Empty;

    public AiFeature Feature { get; set; }
    public string Output { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// docs/36 AI-5: an AI summary kept with its record (an interview summary on a hiring case, a CV match summary on an application),
/// so it is not paid for again on every page view. The latest one per record and feature is shown; older ones stay as history.
/// Advisory only: nothing in GRSMS reads it to decide anything.
/// </summary>
public class AiInsight : BaseEntity
{
    public AiFeature Feature { get; set; }
    public string RecordType { get; set; } = string.Empty;
    public int RecordId { get; set; }

    /// <summary>The answer (JSON of the feature's answer type).</summary>
    public string Content { get; set; } = string.Empty;

    public string PromptVersion { get; set; } = string.Empty;
    public string? GeneratedByUserId { get; set; }
}
