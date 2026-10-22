import { HttpErrorResponse } from '@angular/common/http';
import { Service, computed, inject, signal } from '@angular/core';
import {
  Api,
  AskRequest,
  ChatMessage,
  ConversationResponse,
  ErrorResponse,
  MessageResponse,
  QueryStatus,
} from './api';

// The signed-in user's conversations and the one open in the chat, shared by the list in the
// sidebar and the chat itself. autoProvided: false because Workspace provides it: each time the
// workspace opens it gets a fresh instance, dropped when it closes (e.g. on logout), so one user's
// conversations never show up for the next.
@Service({ autoProvided: false })
export class Conversations {
  private readonly api = inject(Api);

  private readonly conversationList = signal<ConversationResponse[]>([]);
  private readonly openId = signal<string | null>(null);
  private readonly messageList = signal<ChatMessage[]>([]);
  private readonly askStatus = signal<QueryStatus>('idle');
  private readonly listError = signal<string | null>(null);

  // Most recently used first, the order the API returns them in.
  readonly list = this.conversationList.asReadonly();
  readonly selectedId = this.openId.asReadonly();
  readonly messages = this.messageList.asReadonly();
  readonly status = this.askStatus.asReadonly();
  readonly error = this.listError.asReadonly();
  readonly selectedTitle = computed(
    () =>
      this.conversationList().find((existing) => existing.conversationId === this.openId())
        ?.title ?? 'New conversation',
  );

  load(): void {
    this.listError.set(null);
    this.api.getConversations().subscribe({
      next: (conversations) => this.conversationList.set(conversations),
      error: () => this.listError.set('Could not load your conversations.'),
    });
  }

  // Ignored while a question is being answered: the answer belongs to the conversation it was
  // asked in, so the chat can't switch to another one in the meantime.
  open(conversationId: string): void {
    if (this.askStatus() === 'loading' || conversationId === this.openId()) {
      return;
    }

    this.openId.set(conversationId);
    this.messageList.set([]);
    this.api.getConversation(conversationId).subscribe({
      // If the user opened another conversation before this one arrived, it's no longer wanted.
      next: (conversation) => {
        if (this.openId() === conversationId) {
          this.messageList.set(conversation.messages.map(toChatMessage));
        }
      },
      error: () => {
        if (this.openId() === conversationId) {
          this.messageList.set([errorMessage('Could not load this conversation.')]);
        }
      },
    });
  }

  // Only clears the chat: the API creates the conversation together with its first question.
  startNew(): void {
    if (this.askStatus() === 'loading') {
      return;
    }

    this.openId.set(null);
    this.messageList.set([]);
  }

  // The question shows up right away and the answer is appended when it arrives. If asking fails
  // the API stored nothing, so the error only lives here and is gone when the conversation is
  // opened again.
  ask(question: string, topK: number): void {
    if (this.askStatus() === 'loading') {
      return;
    }

    const conversationId = this.openId();
    const request: AskRequest = { question, topK };
    this.messageList.update((messages) => [...messages, { role: 'user', text: question }]);
    this.askStatus.set('loading');

    const exchange = conversationId
      ? this.api.addMessage(conversationId, request)
      : this.api.startConversation(request);

    exchange.subscribe({
      next: (response) => {
        this.askStatus.set('idle');
        this.openId.set(response.conversation.conversationId);
        this.messageList.update((messages) => [...messages, toChatMessage(response.answer)]);
        // A new conversation joins the list; an existing one moves to the top.
        this.conversationList.update((conversations) => [
          response.conversation,
          ...conversations.filter(
            (existing) => existing.conversationId !== response.conversation.conversationId,
          ),
        ]);
      },
      error: (error: HttpErrorResponse) => {
        this.askStatus.set('idle');
        const body = error.error as ErrorResponse | undefined;
        this.messageList.update((messages) => [
          ...messages,
          errorMessage(body?.message ?? 'Something went wrong while asking the question.'),
        ]);
      },
    });
  }

  delete(conversationId: string): void {
    this.api.deleteConversation(conversationId).subscribe({
      next: () => {
        this.conversationList.update((conversations) =>
          conversations.filter((existing) => existing.conversationId !== conversationId),
        );
        if (this.openId() === conversationId) {
          this.openId.set(null);
          this.messageList.set([]);
        }
      },
      error: () => this.listError.set('Could not delete the conversation.'),
    });
  }
}

function toChatMessage(message: MessageResponse): ChatMessage {
  return { role: message.role, text: message.content, sources: message.sources };
}

function errorMessage(text: string): ChatMessage {
  return { role: 'assistant', text, error: true };
}
