using Grsms.Domain.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grsms.Infrastructure.Persistence.Configurations;

// 13 November 2026 (docs/36): the AI log and cache.

/// <summary>Table "AiRequestLogs": every AI call (counts, cost, time, outcome); the prompt only when kept for troubleshooting.</summary>
internal class AiRequestLogConfiguration : IEntityTypeConfiguration<AiRequestLog>
{
    public void Configure(EntityTypeBuilder<AiRequestLog> b)
    {
        b.ToTable("AiRequestLogs");
        b.ConfigureBase();
        b.Property(l => l.Feature).HasConversion<string>().HasMaxLength(40);
        b.Property(l => l.Provider).HasConversion<string>().HasMaxLength(20);
        b.Property(l => l.Outcome).HasConversion<string>().HasMaxLength(20);
        b.Property(l => l.UserId).HasMaxLength(450);
        b.Property(l => l.Model).HasMaxLength(100);
        b.Property(l => l.PromptVersion).HasMaxLength(20);
        b.Property(l => l.CostUsd).HasPrecision(12, 6);
        b.Property(l => l.Error).HasMaxLength(300);
        b.Property(l => l.RecordType).HasMaxLength(50);
        b.Property(l => l.Prompt).HasColumnType("text");
        b.HasIndex(l => l.CreatedAt);
        b.HasIndex(l => new { l.UserId, l.CreatedAt });
    }
}

/// <summary>Table "AiCacheEntries": answers of features that send no personal data, for 7 days.</summary>
internal class AiCacheEntryConfiguration : IEntityTypeConfiguration<AiCacheEntry>
{
    public void Configure(EntityTypeBuilder<AiCacheEntry> b)
    {
        b.ToTable("AiCacheEntries");
        b.ConfigureBase();
        b.Property(c => c.Key).HasMaxLength(64).IsRequired();
        b.Property(c => c.Feature).HasConversion<string>().HasMaxLength(40);
        b.Property(c => c.Output).HasColumnType("text").IsRequired();
        b.HasIndex(c => c.Key).IsUnique();
        b.HasIndex(c => c.ExpiresAt);
    }
}

/// <summary>Table "AiInsights" (docs/36 AI-5): AI summaries kept with their record. Audited like any record.</summary>
internal class AiInsightConfiguration : IEntityTypeConfiguration<AiInsight>
{
    public void Configure(EntityTypeBuilder<AiInsight> b)
    {
        b.ToTable("AiInsights");
        b.ConfigureBase();
        b.Property(i => i.Feature).HasConversion<string>().HasMaxLength(40);
        b.Property(i => i.RecordType).HasMaxLength(50).IsRequired();
        b.Property(i => i.Content).HasColumnType("text").IsRequired();
        b.Property(i => i.PromptVersion).HasMaxLength(20);
        b.Property(i => i.GeneratedByUserId).HasMaxLength(450);
        b.HasIndex(i => new { i.RecordType, i.RecordId, i.Feature });
    }
}

/// <summary>Table "KnowledgeDocuments" (docs/36 AI-7): GREL's policy library. Audited like any record.</summary>
internal class KnowledgeDocumentConfiguration : IEntityTypeConfiguration<Domain.Knowledge.KnowledgeDocument>
{
    public void Configure(EntityTypeBuilder<Domain.Knowledge.KnowledgeDocument> b)
    {
        b.ToTable("KnowledgeDocuments");
        b.ConfigureBase();
        b.Property(d => d.Title).HasMaxLength(200).IsRequired();
        b.Property(d => d.Owner).HasMaxLength(150).IsRequired();
        b.Property(d => d.Version).HasMaxLength(30).IsRequired();
        b.Property(d => d.Audience).HasConversion<string>().HasMaxLength(30);
        b.Property(d => d.Summary).HasMaxLength(500);
        b.Property(d => d.FileName).HasMaxLength(200).IsRequired();
        b.Property(d => d.ReadProblem).HasMaxLength(300);
        b.HasMany(d => d.Sections).WithOne().HasForeignKey(s => s.KnowledgeDocumentId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(d => d.IsActive);
    }
}

/// <summary>Table "KnowledgeSections": the searchable pieces of each document (searched in GRSMS itself, see KnowledgeSearch).</summary>
internal class KnowledgeSectionConfiguration : IEntityTypeConfiguration<Domain.Knowledge.KnowledgeSection>
{
    public void Configure(EntityTypeBuilder<Domain.Knowledge.KnowledgeSection> b)
    {
        b.ToTable("KnowledgeSections");
        b.ConfigureBase();
        b.Property(s => s.Heading).HasMaxLength(300).IsRequired();
        b.Property(s => s.Text).HasColumnType("text").IsRequired();
        b.HasIndex(s => new { s.KnowledgeDocumentId, s.SortOrder });
    }
}
