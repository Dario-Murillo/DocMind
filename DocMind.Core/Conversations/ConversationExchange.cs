namespace DocMind.Core.Conversations;

// One question and its answer, as stored. Conversation is included so the caller can move the
// conversation to the top of its list (or add it, if it was just created).
public record ConversationExchange(ConversationSummary Conversation, Message Question, Message Answer);
