using FanFundingBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FanFundingBot.Data.Configurations;

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

        builder.HasOne(q => q.AnsweredByUser)
            .WithMany()
            .HasForeignKey(q => q.AnsweredByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(q => q.AnsweredAt);
    }
}
