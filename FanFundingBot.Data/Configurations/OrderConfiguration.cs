using FanFundingBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FanFundingBot.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Phone)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(o => o.DeliveryMethod)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(o => o.Address)
            .HasMaxLength(512);

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.HasMany(o => o.OrderAddons)
            .WithOne(oa => oa.Order)
            .HasForeignKey(oa => oa.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => o.UserId);
        builder.HasIndex(o => o.ProjectId);
    }
}
