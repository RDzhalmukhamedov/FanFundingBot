using FanFundingBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FanFundingBot.Data.Configurations;

public class OrderAddonConfiguration : IEntityTypeConfiguration<OrderAddon>
{
    public void Configure(EntityTypeBuilder<OrderAddon> builder)
    {
        builder.HasKey(oa => new { oa.OrderId, oa.AddonId });

        builder.Property(oa => oa.Quantity)
            .IsRequired()
            .HasDefaultValue(1);

        builder.HasOne(oa => oa.Addon)
            .WithMany(a => a.OrderAddons)
            .HasForeignKey(oa => oa.AddonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
