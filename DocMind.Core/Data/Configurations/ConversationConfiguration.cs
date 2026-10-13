namespace DocMind.Core.Data.Configurations;

using DocMind.Core.Conversations;
using DocMind.Core.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        _ = builder.Property(conversation => conversation.Title).HasMaxLength(Conversation.MaxTitleLength);

        // Serves "list this user's conversations, most recent first". UserId leads the index, so
        // EF doesn't create a separate index for the UserId foreign key.
        _ = builder.HasIndex(conversation => new { conversation.UserId, conversation.UpdatedAt });

        _ = builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(conversation => conversation.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        _ = builder.HasMany(conversation => conversation.Messages)
            .WithOne()
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
