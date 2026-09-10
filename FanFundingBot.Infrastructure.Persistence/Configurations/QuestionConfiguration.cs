using FanFundingBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FanFundingBot.Infrastructure.Persistence.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.HasKey(q => q.Id);

        builder.Property(q => q.Text)
            .IsRequired()
            .HasMaxLength(4096);

        builder.Property(q => q.Answer)
            .HasMaxLength(4096);

        builder.HasOne(q => q.User)
            .WithMany()
            .HasForeignKey("UserId")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(q => q.AnsweredBy)
            .WithMany()
            .HasForeignKey("AnsweredById")
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(q => q.AnsweredAt);

        builder.Ignore(q => q.DomainEvents);
    }
}
