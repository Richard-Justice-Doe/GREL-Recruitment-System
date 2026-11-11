using Grsms.Domain.Advertisements;
using Grsms.Domain.Organization;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grsms.Infrastructure.Persistence.Configurations;

// Phase 4 tables. Enums are stored as readable text.

/// <summary>Table "JobAdverts".</summary>
internal class JobAdvertConfiguration : IEntityTypeConfiguration<JobAdvert>
{
    public void Configure(EntityTypeBuilder<JobAdvert> b)
    {
        b.ToTable("JobAdverts");
        b.ConfigureBase();

        b.Property(a => a.AdvertNumber).HasMaxLength(40).IsRequired();
        b.HasIndex(a => a.AdvertNumber).IsUnique();

        b.Property(a => a.Title).HasMaxLength(200).IsRequired();
        b.Property(a => a.Body).IsRequired();                        // LONGTEXT; the form limits it to 10,000 characters
        b.Property(a => a.HowToApply).HasMaxLength(2000).IsRequired();
        b.Property(a => a.Status).HasConversion<string>().HasMaxLength(30);   // 30 since 11 Nov 2026 ("ReturnedForCorrection")
        b.Property(a => a.RequestingHodUserId).HasMaxLength(255).IsRequired();
        b.Property(a => a.CreatedByUserId).HasMaxLength(255).IsRequired();
        b.Property(a => a.Version).IsConcurrencyToken();
        b.Property(a => a.SubmittedByUserId).HasMaxLength(255);
        b.Property(a => a.HrReviewedByUserId).HasMaxLength(255);
        b.Property(a => a.ItApprovedByUserId).HasMaxLength(255);
        b.Property(a => a.ReviewComment).HasMaxLength(1000);

        // RESTRICT everywhere: vacancies, master data and users cannot be hard-deleted while adverts use them.
        b.HasOne<Vacancy>().WithMany().HasForeignKey(a => a.VacancyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Division>().WithMany().HasForeignKey(a => a.DivisionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(a => a.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.RequestingHodUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<JobAdvert>().WithMany().HasForeignKey(a => a.ClonedFromAdvertId).OnDelete(DeleteBehavior.Restrict);

        // Channel rows belong to the advert; history rows are never deleted.
        b.HasMany(a => a.Channels).WithOne().HasForeignKey(c => c.JobAdvertId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(a => a.Actions).WithOne().HasForeignKey(x => x.JobAdvertId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(a => new { a.VacancyId, a.Status });
        b.HasIndex(a => new { a.Status, a.StartDate });   // the scheduled-publishing query
        b.HasIndex(a => new { a.Status, a.EndDate });     // the automatic-closing query
        b.HasIndex(a => new { a.DivisionId, a.Status });  // reports by division
        b.HasIndex(a => new { a.DepartmentId, a.Status });
    }
}

/// <summary>Table "AdvertChannels": where each advert is placed and its status per channel.</summary>
internal class AdvertChannelPostConfiguration : IEntityTypeConfiguration<AdvertChannelPost>
{
    public void Configure(EntityTypeBuilder<AdvertChannelPost> b)
    {
        b.ToTable("AdvertChannels");
        b.HasKey(c => c.Id);
        b.Property(c => c.Channel).HasConversion<string>().HasMaxLength(30);
        b.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(c => c.Reference).HasMaxLength(500);
        b.Property(c => c.PostedByName).HasMaxLength(150);

        // Each channel at most once per advert.
        b.HasIndex(c => new { c.JobAdvertId, c.Channel }).IsUnique();
    }
}

/// <summary>Table "JobAdvertActions": an advert's history.</summary>
internal class AdvertActionConfiguration : IEntityTypeConfiguration<AdvertActionEntry>
{
    public void Configure(EntityTypeBuilder<AdvertActionEntry> b)
    {
        b.ToTable("JobAdvertActions");
        b.ConfigureBase();
        b.Property(a => a.Action).HasConversion<string>().HasMaxLength(30);
        b.Property(a => a.FromStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(a => a.ToStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(a => a.ActorUserId).HasMaxLength(255);
        b.Property(a => a.ActorName).HasMaxLength(150).IsRequired();
        b.Property(a => a.Comments).HasMaxLength(2000);

        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => new { a.JobAdvertId, a.ActionAt });
    }
}
