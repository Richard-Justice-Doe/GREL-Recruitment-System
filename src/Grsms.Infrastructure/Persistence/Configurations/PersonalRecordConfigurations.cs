using Grsms.Domain.Onboarding;
using Grsms.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grsms.Infrastructure.Persistence.Configurations;

// Phase 19 tables: the Employee Personal Record Declaration. Enums are stored as readable text.

/// <summary>Table "EmployeePersonalRecords": one per onboarding.</summary>
internal class EmployeePersonalRecordConfiguration : IEntityTypeConfiguration<EmployeePersonalRecord>
{
    public void Configure(EntityTypeBuilder<EmployeePersonalRecord> b)
    {
        b.ToTable("EmployeePersonalRecords");
        b.ConfigureBase();
        b.Property(p => p.EmployeeUserId).HasMaxLength(255).IsRequired();
        b.Property(p => p.Qualification).HasMaxLength(200);
        b.Property(p => p.SsnitNumber).HasMaxLength(30);
        b.Property(p => p.ResidentialAddress).HasMaxLength(500);
        b.Property(p => p.HomeTown).HasMaxLength(100);
        b.Property(p => p.Region).HasConversion<string>().HasMaxLength(20);
        b.Property(p => p.Tribe).HasMaxLength(100);
        b.Property(p => p.Religion).HasMaxLength(100);
        b.Property(p => p.Email).HasMaxLength(256);
        b.Property(p => p.MobileNumber).HasMaxLength(30);
        b.Property(p => p.DisabilityDetails).HasColumnType("text");
        b.Property(p => p.VisibleMarks).HasMaxLength(500);
        b.Property(p => p.NextOfKinName).HasMaxLength(200);
        b.Property(p => p.NextOfKinTelephone).HasMaxLength(30);
        b.Property(p => p.NextOfKinRelationship).HasMaxLength(100);
        b.Property(p => p.NextOfKinAddress).HasMaxLength(500);
        b.Property(p => p.Version).IsConcurrencyToken();

        b.HasOne<OnboardingRecord>().WithMany().HasForeignKey(p => p.OnboardingRecordId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.EmployeeUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => p.OnboardingRecordId).IsUnique();
        b.HasIndex(p => p.EmployeeUserId);

        // The lists are replaced as a whole on every save (rows removed are deleted; the audit trail keeps them).
        b.HasMany(p => p.Languages).WithOne().HasForeignKey(x => x.PersonalRecordId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.BankAccounts).WithOne().HasForeignKey(x => x.PersonalRecordId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.Spouses).WithOne().HasForeignKey(x => x.PersonalRecordId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.Children).WithOne().HasForeignKey(x => x.PersonalRecordId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.Dependants).WithOne().HasForeignKey(x => x.PersonalRecordId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.EmergencyContacts).WithOne().HasForeignKey(x => x.PersonalRecordId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.Beneficiaries).WithOne().HasForeignKey(x => x.PersonalRecordId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class EmployeeLanguageConfiguration : IEntityTypeConfiguration<EmployeeLanguage>
{
    public void Configure(EntityTypeBuilder<EmployeeLanguage> b)
    {
        b.ToTable("EmployeeLanguages");
        b.Property(x => x.Language).HasMaxLength(60).IsRequired();
        b.Property(x => x.Proficiency).HasConversion<string>().HasMaxLength(20);
    }
}

internal class EmployeeBankAccountConfiguration : IEntityTypeConfiguration<EmployeeBankAccount>
{
    public void Configure(EntityTypeBuilder<EmployeeBankAccount> b)
    {
        b.ToTable("EmployeeBankAccounts");
        b.Property(x => x.BankName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Branch).HasMaxLength(100).IsRequired();
        b.Property(x => x.AccountNumber).HasMaxLength(40).IsRequired();
    }
}

internal class EmployeeSpouseConfiguration : IEntityTypeConfiguration<EmployeeSpouse>
{
    public void Configure(EntityTypeBuilder<EmployeeSpouse> b)
    {
        b.ToTable("EmployeeSpouses");
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Occupation).HasMaxLength(100);
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.Telephone).HasMaxLength(30);
    }
}

internal class EmployeeChildConfiguration : IEntityTypeConfiguration<EmployeeChild>
{
    public void Configure(EntityTypeBuilder<EmployeeChild> b)
    {
        b.ToTable("EmployeeChildren");
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Gender).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.PlaceOfBirth).HasMaxLength(100);
        b.Property(x => x.CurrentResidence).HasMaxLength(200);
    }
}

internal class EmployeeDependantConfiguration : IEntityTypeConfiguration<EmployeeDependant>
{
    public void Configure(EntityTypeBuilder<EmployeeDependant> b)
    {
        b.ToTable("EmployeeDependants");
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Relationship).HasMaxLength(100).IsRequired();
    }
}

internal class EmployeeEmergencyContactConfiguration : IEntityTypeConfiguration<EmployeeEmergencyContact>
{
    public void Configure(EntityTypeBuilder<EmployeeEmergencyContact> b)
    {
        b.ToTable("EmployeeEmergencyContacts");
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Relationship).HasMaxLength(100).IsRequired();
        b.Property(x => x.Telephone1).HasMaxLength(30).IsRequired();
        b.Property(x => x.Telephone2).HasMaxLength(30);
    }
}

internal class EmployeeBeneficiaryConfiguration : IEntityTypeConfiguration<EmployeeBeneficiary>
{
    public void Configure(EntityTypeBuilder<EmployeeBeneficiary> b)
    {
        b.ToTable("EmployeeBeneficiaries");
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Relationship).HasMaxLength(100).IsRequired();
        b.Property(x => x.Percentage).HasPrecision(5, 2);
    }
}

/// <summary>Table "EmployeeDeclarations": every signature of the declaration. Insert-only (enforced by the audit interceptor).</summary>
internal class EmployeeDeclarationConfiguration : IEntityTypeConfiguration<EmployeeDeclaration>
{
    public void Configure(EntityTypeBuilder<EmployeeDeclaration> b)
    {
        b.ToTable("EmployeeDeclarations");
        b.ConfigureBase();
        b.Property(d => d.EmployeeUserId).HasMaxLength(255).IsRequired();
        b.Property(d => d.DeclarationText).HasMaxLength(500).IsRequired();
        b.Property(d => d.SignatureName).HasMaxLength(200).IsRequired();
        b.Property(d => d.IpAddress).HasMaxLength(64);
        b.Property(d => d.Snapshot).HasColumnType("text").IsRequired();
        b.Property(d => d.SnapshotSha256).HasMaxLength(64).IsRequired();
        b.HasOne<EmployeePersonalRecord>().WithMany().HasForeignKey(d => d.PersonalRecordId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(d => d.EmployeeUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(d => new { d.PersonalRecordId, d.SignedAt });
    }
}
