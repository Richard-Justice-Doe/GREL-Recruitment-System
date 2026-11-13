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
