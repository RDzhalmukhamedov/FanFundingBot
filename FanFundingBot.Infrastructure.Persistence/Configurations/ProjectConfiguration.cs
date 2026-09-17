using FanFundingBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FanFundingBot.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(p => p.Description)
            .HasMaxLength(2048);

        builder.HasOne(p => p.Status)
            .WithMany()
            .HasForeignKey("StatusId")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Addons)
            .WithOne()
            .HasForeignKey("ProjectId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Addons)
            .HasField("_addons")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.Members)
            .WithOne()
            .HasForeignKey("ProjectId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Members)
            .HasField("_members")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(p => p.DomainEvents);
    }
}
