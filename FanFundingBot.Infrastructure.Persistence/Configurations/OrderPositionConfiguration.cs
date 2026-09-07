using FanFundingBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FanFundingBot.Infrastructure.Persistence.Configurations;

public class OrderPositionConfiguration : IEntityTypeConfiguration<OrderPosition>
{
    public void Configure(EntityTypeBuilder<OrderPosition> builder)
    {
        builder.HasKey(p => new { p.OrderId, p.AddonId });

        builder.Property(p => p.Quantity)
            .IsRequired()
            .HasDefaultValue(1);

        builder.HasOne(p => p.Addon)
            .WithMany()
            .HasForeignKey(p => p.AddonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
