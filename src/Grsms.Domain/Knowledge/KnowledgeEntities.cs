using System.ComponentModel.DataAnnotations;
using Grsms.Domain.Common;

namespace Grsms.Domain.Knowledge;

/// <summary>14 November 2026 (docs/36 AI-7): who may read a knowledge-base document and have it used in answers.</summary>
public enum KnowledgeAudience
{
    /// <summary>GREL staff only.</summary>
    [Display(Name = "Staff")] Staff,

    /// <summary>Staff and new employees who are still onboarding (they ask through the onboarding assistant).</summary>
    [Display(Name = "Staff and new employees")] StaffAndNewEmployees
}

/// <summary>
/// One GREL policy or procedure in the knowledge base (HR policies, handbook, SOPs, conditions of service). The file itself is an
/// <c>Attachment</c> (owner type "KnowledgeDocument", the usual upload checks); its text is split into <see cref="KnowledgeSection"/>s
/// that the search and the AI answers use. Replacing the file keeps the record and re-reads the text.
/// </summary>
public class KnowledgeDocument : BaseEntity
{
    [MaxLength(200)] public string Title { get; set; } = string.Empty;

    /// <summary>Who owns and approves the document, e.g. "HR Manager".</summary>
    [MaxLength(150)] public string Owner { get; set; } = string.Empty;

    [MaxLength(30)] public string Version { get; set; } = "1";
    public DateOnly EffectiveDate { get; set; }
    public KnowledgeAudience Audience { get; set; }

    /// <summary>Archived documents stay listed for HR but are no longer searched or used in answers.</summary>
    public bool IsActive { get; set; } = true;

    [MaxLength(500)] public string? Summary { get; set; }

    /// <summary>The current file (an Attachment row).</summary>
    public int AttachmentId { get; set; }
    [MaxLength(200)] public string FileName { get; set; } = string.Empty;

    /// <summary>Why no text could be read, if so (e.g. a scanned PDF with no text layer).</summary>
    [MaxLength(300)] public string? ReadProblem { get; set; }

    public int SectionCount { get; set; }
    public int CharacterCount { get; set; }

    public List<KnowledgeSection> Sections { get; set; } = [];
}

/// <summary>A searchable piece of a document: a heading and the text under it (about a page at most).</summary>
public class KnowledgeSection : BaseEntity
{
    public int KnowledgeDocumentId { get; set; }
    public int SortOrder { get; set; }
    [MaxLength(300)] public string Heading { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
