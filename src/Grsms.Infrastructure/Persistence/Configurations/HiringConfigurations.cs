using Grsms.Domain.Applications;
using Grsms.Domain.Hiring;
using Grsms.Domain.Organization;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grsms.Infrastructure.Persistence.Configurations;

// Phase 8 tables. Enums are stored as readable text; long free text is TEXT (see docs/03-database.md, "MySQL row size").

internal class BackgroundCheckConfiguration : IEntityTypeConfiguration<BackgroundCheck>
{
    public void Configure(EntityTypeBuilder<BackgroundCheck> b)
    {
        b.ToTable("BackgroundChecks");
        b.ConfigureBase();
        b.Property(c => c.Type).HasConversion<string>().HasMaxLength(30);
        b.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(c => c.Subject).HasMaxLength(300).IsRequired();
        b.Property(c => c.Notes).HasColumnType("text");
        b.Property(c => c.RecordedByUserId).HasMaxLength(255).IsRequired();
        b.HasOne<JobApplication>().WithMany().HasForeignKey(c => c.JobApplicationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(c => c.JobApplicationId);
    }
}

/// <summary>Table "HiringRecommendations": the hiring report and its approvals.</summary>
internal class HiringRecommendationConfiguration : IEntityTypeConfiguration<HiringRecommendation>
{
    public void Configure(EntityTypeBuilder<HiringRecommendation> b)
    {
        b.ToTable("HiringRecommendations");
        b.ConfigureBase();
        b.Property(h => h.Number).HasMaxLength(40).IsRequired();
        b.HasIndex(h => h.Number).IsUnique();
        b.Property(h => h.ProposedSalary).HasPrecision(18, 2);
        b.Property(h => h.Justification).HasColumnType("text").IsRequired();
        b.Property(h => h.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(h => h.HodDecision).HasConversion<string>().HasMaxLength(10);
        b.Property(h => h.HrDecision).HasConversion<string>().HasMaxLength(10);
        b.Property(h => h.MdDecision).HasConversion<string>().HasMaxLength(10);
        b.Property(h => h.HodComment).HasColumnType("text");
        b.Property(h => h.HrComment).HasColumnType("text");
        b.Property(h => h.MdComment).HasColumnType("text");
        foreach (var p in new[] { nameof(HiringRecommendation.RequestingHodUserId), nameof(HiringRecommendation.CreatedByUserId),
                     nameof(HiringRecommendation.HodUserId), nameof(HiringRecommendation.HrUserId), nameof(HiringRecommendation.MdUserId) })
            b.Property(p).HasMaxLength(255);
        b.Property(h => h.Version).IsConcurrencyToken();

        b.HasOne<JobApplication>().WithMany().HasForeignKey(h => h.JobApplicationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Vacancy>().WithMany().HasForeignKey(h => h.VacancyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Division>().WithMany().HasForeignKey(h => h.DivisionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(h => h.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(h => h.RequestingHodUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(h => h.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(h => h.HodUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(h => h.HrUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(h => h.MdUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(h => new { h.JobApplicationId, h.Status });
        b.HasIndex(h => new { h.Status, h.DepartmentId });   // "awaiting my decision"
        b.HasIndex(h => new { h.DivisionId, h.Status });     // reports by division
    }
}

/// <summary>Table "Offers".</summary>
internal class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> b)
    {
        b.ToTable("Offers");
        b.ConfigureBase();
        b.Property(o => o.Number).HasMaxLength(40).IsRequired();
        b.HasIndex(o => o.Number).IsUnique();
        b.Property(o => o.JobTitle).HasMaxLength(150).IsRequired();
        b.Property(o => o.MonthlySalary).HasPrecision(18, 2);
        b.Property(o => o.Terms).HasColumnType("text");
        b.Property(o => o.Reason).HasColumnType("text");
        b.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(o => o.RequestingHodUserId).HasMaxLength(255).IsRequired();
        b.Property(o => o.SentByUserId).HasMaxLength(255).IsRequired();
        b.Property(o => o.Version).IsConcurrencyToken();

        b.HasOne<JobApplication>().WithMany().HasForeignKey(o => o.JobApplicationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<HiringRecommendation>().WithMany().HasForeignKey(o => o.HiringRecommendationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Vacancy>().WithMany().HasForeignKey(o => o.VacancyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Division>().WithMany().HasForeignKey(o => o.DivisionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(o => o.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(o => o.RequestingHodUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(o => o.SentByUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(o => new { o.VacancyId, o.Status });       // positions filled
        b.HasIndex(o => new { o.Status, o.ExpiresOn });       // automatic expiry
        b.HasIndex(o => o.JobApplicationId);
        b.HasIndex(o => new { o.DivisionId, o.Status });
    }
}
