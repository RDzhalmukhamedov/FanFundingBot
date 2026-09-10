using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FanFundingBot.Infrastructure.Persistence.Configurations;

public class OrderPositionConfiguration : IEntityTypeConfiguration<OrderPosition>
{
    public void Configure(EntityTypeBuilder<OrderPosition> builder)
    {
        builder.HasKey(p => new { p.OrderId, p.AddonId });

        builder.Property(p => p.Quantity)
            .HasConversion(q => (int)q, v => Quantity.Create(v))
            .IsRequired()
            .HasDefaultValue(Quantity.Create(1));

        builder.HasOne(p => p.Addon)
            .WithMany()
            .HasForeignKey(p => p.AddonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
