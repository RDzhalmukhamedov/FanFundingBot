using FanFundingBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FanFundingBot.Infrastructure.Persistence.Configurations;

public class ProjectStatusEntryConfiguration : IEntityTypeConfiguration<ProjectStatus>
{
    public void Configure(EntityTypeBuilder<ProjectStatus> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.DisplayName)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(e => e.Order)
            .IsRequired();

        builder.HasIndex(e => e.Order)
            .IsUnique();

        builder.Property(e => e.CanEditOrders)
            .IsRequired();

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();
    }
}
