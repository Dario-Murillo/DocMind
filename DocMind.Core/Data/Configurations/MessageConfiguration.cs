namespace DocMind.Core.Data.Configurations;

using DocMind.Core.Conversations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        // Messages are always read per conversation and in order. ConversationId leads the index,
        // so it also serves as the foreign key's index.
        _ = builder.HasIndex(message => new { message.ConversationId, message.CreatedAt });

        _ = builder.HasMany(message => message.Sources)
            .WithOne()
            .HasForeignKey(source => source.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
