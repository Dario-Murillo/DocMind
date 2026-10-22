namespace DocMind.Core.Conversations;

// Stored as an integer. The values matter: a question and its answer are ordered by CreatedAt and
// then by Role, so User must sort before Assistant.
public enum MessageRole
{
    User = 0,
    Assistant = 1,
}