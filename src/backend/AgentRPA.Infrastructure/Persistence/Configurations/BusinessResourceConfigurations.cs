using AgentRPA.Domain.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentRPA.Infrastructure.Persistence.Configurations;

public sealed class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("cities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
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
