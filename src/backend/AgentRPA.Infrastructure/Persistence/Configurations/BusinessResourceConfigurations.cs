using AgentRPA.Domain.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentRPA.Infrastructure.Persistence.Configurations;

public sealed class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("countries"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired(); builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public sealed class ProvinceConfiguration : IEntityTypeConfiguration<Province>
{
    public void Configure(EntityTypeBuilder<Province> builder)
    {
        builder.ToTable("provinces"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired(); builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.HasOne<Country>().WithMany().HasForeignKey(x => x.CountryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CountryId, x.Code }).IsUnique();
    }
}

public sealed class DistrictConfiguration : IEntityTypeConfiguration<District>
{
    public void Configure(EntityTypeBuilder<District> builder)
    {
        builder.ToTable("districts"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired(); builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.HasOne<City>().WithMany().HasForeignKey(x => x.CityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CityId, x.Code }).IsUnique();
    }
}

public sealed class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("cities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.HasOne<Province>().WithMany().HasForeignKey(x => x.ProvinceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public sealed class BusinessSystemConfiguration : IEntityTypeConfiguration<BusinessSystem>
{
    public void Configure(EntityTypeBuilder<BusinessSystem> builder)
    {
        builder.ToTable("business_systems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.Property(x => x.BaseUrl).HasMaxLength(1024);
        builder.HasIndex(x => new { x.CityId, x.Code }).IsUnique();
    }
}

public sealed class BusinessFunctionConfiguration : IEntityTypeConfiguration<BusinessFunction>
{
    public void Configure(EntityTypeBuilder<BusinessFunction> builder)
    {
        builder.ToTable("business_functions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.SystemId, x.Code }).IsUnique();
    }
}
