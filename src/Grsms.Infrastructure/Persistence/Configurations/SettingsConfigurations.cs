using Grsms.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grsms.Infrastructure.Persistence.Configurations;

// Phase 16 tables.

/// <summary>Table "SystemSettings": values saved on the System settings screen (secrets encrypted).</summary>
internal class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> b)
    {
        b.ToTable("SystemSettings");
        b.ConfigureBase();
        b.Property(s => s.Key).HasMaxLength(100).IsRequired();
        b.Property(s => s.SettingValue).HasColumnType("text"); // an encrypted secret is much longer than what was typed
        // Not unique: a soft-deleted row could block a new one. The store keeps one live row per key.
        b.HasIndex(s => s.Key);
    }
}

/// <summary>Table "EmailTemplates": candidate emails reworded on the Email templates screen.</summary>
internal class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    public void Configure(EntityTypeBuilder<EmailTemplate> b)
    {
        b.ToTable("EmailTemplates");
        b.ConfigureBase();
        b.Property(t => t.Key).HasMaxLength(100).IsRequired();
        b.Property(t => t.Subject).HasMaxLength(200).IsRequired();
        b.Property(t => t.Body).HasColumnType("text").IsRequired();
        b.HasIndex(t => t.Key); // not unique: "back to the built-in wording" soft-deletes the row, and it may be edited again
    }
}
