using FanFundingBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FanFundingBot.Data.Configurations;

public class ProjectStatusEntryConfiguration : IEntityTypeConfiguration<ProjectStatusEntry>
{
    public void Configure(EntityTypeBuilder<ProjectStatusEntry> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.DisplayName)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(e => e.Order)
            .IsRequired();

        builder.Property(e => e.CanEdit)
            .IsRequired();

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();
    }
}
