using AgentRPA.Domain.Mock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentRPA.Infrastructure.Persistence.Configurations;

public sealed class MockSocialEmployeeConfiguration : IEntityTypeConfiguration<MockSocialEmployee>
{
    public void Configure(EntityTypeBuilder<MockSocialEmployee> builder)
    {
        builder.ToTable("MockSocialEmployees");
        builder.HasKey(x => x.IdNumber);
        builder.Property(x => x.IdNumber).HasMaxLength(18);
        builder.Property(x => x.Name).HasMaxLength(40).IsRequired();
    }
}

public sealed class MockSocialReceiptConfiguration : IEntityTypeConfiguration<MockSocialReceipt>
{
    public void Configure(EntityTypeBuilder<MockSocialReceipt> builder)
    {
        builder.ToTable("MockSocialReceipts");
        builder.HasKey(x => x.SubmissionId);
        builder.Property(x => x.SubmissionId).HasMaxLength(128);
        builder.Property(x => x.Operation).HasMaxLength(6).IsRequired();
        builder.Property(x => x.IdNumber).HasMaxLength(18).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(40).IsRequired();
    }
}
