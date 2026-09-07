using FanFundingBot.Domain.Entities;
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
            .HasMaxLength(32);

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.HasIndex(u => u.Role);

        builder.Ignore(u => u.Orders);
        builder.Ignore(u => u.Questions);
    }
}
