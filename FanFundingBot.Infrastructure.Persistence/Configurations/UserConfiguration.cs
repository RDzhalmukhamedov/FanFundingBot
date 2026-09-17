using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FanFundingBot.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .ValueGeneratedNever();

        builder.Property(u => u.FirstName)
            .HasMaxLength(32);

        builder.Property(u => u.LastName)
            .HasMaxLength(32);

        builder.Property(u => u.Username)
            .HasMaxLength(32);

        builder.Property(u => u.Phone)
            .HasConversion(
                v => v.HasValue ? v.Value.ToString() : null,
                v => string.IsNullOrEmpty(v) ? (PhoneNumber?)null : PhoneNumber.Create(v))
            .HasMaxLength(16);

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.HasIndex(u => u.Role);

        builder.Ignore(u => u.Orders);
        builder.Ignore(u => u.Questions);
        builder.Ignore(u => u.DomainEvents);
    }
}
