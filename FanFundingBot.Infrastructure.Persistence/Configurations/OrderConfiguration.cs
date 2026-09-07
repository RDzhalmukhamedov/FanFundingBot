using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FanFundingBot.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);

        builder.HasDiscriminator<string>("OrderType")
            .HasValue<DraftOrder>("Draft")
            .HasValue<FormedOrder>("Formed");

        builder.Property(o => o.Phone)
            .HasConversion(
                p => p.ToString(),
                v => string.IsNullOrEmpty(v) ? PhoneNumber.Empty : PhoneNumber.Create(v))
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(o => o.DeliveryMethod)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(o => o.Address)
            .HasConversion(
                a => a.HasValue ? a.Value.ToString() : null,
                v => string.IsNullOrEmpty(v) ? (Address?)null : Address.Create(v))
            .HasMaxLength(512);

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.HasOne(o => o.User)
            .WithMany()
            .HasForeignKey("UserId")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Project)
            .WithMany()
            .HasForeignKey("ProjectId")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.OrderAddons)
            .WithOne()
            .HasForeignKey("OrderId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(o => o.OrderAddons)
            .HasField("_orderPositions")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(o => o.DomainEvents);

        builder.HasIndex("UserId");
        builder.HasIndex("ProjectId");
    }
}
