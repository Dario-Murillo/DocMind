import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import {
  ConversationDetailResponse,
  ConversationResponse,
  ExchangeResponse,
  MessageResponse,
} from './api';
import { Conversations } from './conversations';

describe('Conversations', () => {
  let conversations: Conversations;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      // Not auto-provided (Workspace provides it in the app), so the test provides it itself.
      providers: [provideHttpClient(), provideHttpClientTesting(), Conversations],
    });
    conversations = TestBed.inject(Conversations);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function conversation(id: string, title = `Conversation ${id}`): ConversationResponse {
    return {
      conversationId: id,
      title,
      createdAt: '2026-10-22T10:00:00Z',
      updatedAt: '2026-10-22T10:00:00Z',
    };
  }

  function message(role: 'user' | 'assistant', content: string): MessageResponse {
    return {
      messageId: `${role}: ${content}`,
      role,
      content,
      createdAt: '2026-10-22T10:00:00Z',
      sources: [],
    };
  }

  function exchange(
    conversationResponse: ConversationResponse,
    question: string,
    answer: string,
  ): ExchangeResponse {
    return {
      conversation: conversationResponse,
      question: message('user', question),
      answer: message('assistant', answer),
    };
  }

  function detail(id: string, messages: MessageResponse[]): ConversationDetailResponse {
    return { ...conversation(id), messages };
  }

  function texts(): string[] {
    return conversations.messages().map((chatMessage) => chatMessage.text);
  }

  it('starts a conversation with the first question and asks later ones in it', () => {
    conversations.ask('First?', 5);

    // The question is on screen while the answer is on its way.
    expect(conversations.status()).toBe('loading');
    expect(texts()).toEqual(['First?']);

    const start = http.expectOne(
      (req) => req.method === 'POST' && req.url.endsWith('/conversations'),
    );
    expect(start.request.body).toEqual({ question: 'First?', topK: 5 });
    start.flush(exchange(conversation('c1', 'First?'), 'First?', 'First answer'));

    conversations.ask('Second?', 5);
    http
      .expectOne((req) => req.method === 'POST' && req.url.endsWith('/conversations/c1/messages'))
      .flush(exchange(conversation('c1', 'First?'), 'Second?', 'Second answer'));

    expect(conversations.status()).toBe('idle');
    expect(conversations.selectedId()).toBe('c1');
    expect(conversations.selectedTitle()).toBe('First?');
    expect(texts()).toEqual(['First?', 'First answer', 'Second?', 'Second answer']);
    expect(conversations.list().map((existing) => existing.conversationId)).toEqual(['c1']);
  });

  it('moves a conversation to the top of the list when asked in it', () => {
    conversations.load();
    http
      .expectOne((req) => req.method === 'GET' && req.url.endsWith('/conversations'))
      .flush([conversation('c1'), conversation('c2')]);
    conversations.open('c2');
    http.expectOne((req) => req.url.endsWith('/conversations/c2')).flush(detail('c2', []));

    conversations.ask('Follow-up?', 5);
    http
      .expectOne((req) => req.url.endsWith('/conversations/c2/messages'))
      .flush(exchange(conversation('c2'), 'Follow-up?', 'Answer'));

    expect(conversations.list().map((existing) => existing.conversationId)).toEqual(['c2', 'c1']);
  });

  it('keeps a failed question on screen with the error, without adding a conversation', () => {
    conversations.ask('First?', 5);

    http
      .expectOne((req) => req.method === 'POST' && req.url.endsWith('/conversations'))
      .flush(
        { message: 'Could not reach Ollama.' },
        { status: 503, statusText: 'Service Unavailable' },
      );

    expect(conversations.status()).toBe('idle');
    expect(conversations.messages()).toEqual([
      { role: 'user', text: 'First?' },
      { role: 'assistant', text: 'Could not reach Ollama.', error: true },
    ]);
    expect(conversations.selectedId()).toBeNull();
    expect(conversations.list()).toEqual([]);
  });

  it('opens a stored conversation with its messages and sources', () => {
    const answer: MessageResponse = {
      ...message('assistant', 'Stored answer'),
      sources: [
        {
          documentId: null,
          fileName: 'manual.pdf',
          sequenceNumber: 3,
          score: 0.82,
          excerpt: 'DocMind is...',
        },
      ],
    };

    conversations.open('c1');
    http
      .expectOne((req) => req.method === 'GET' && req.url.endsWith('/conversations/c1'))
      .flush(detail('c1', [message('user', 'Stored question'), answer]));

    expect(conversations.selectedId()).toBe('c1');
    expect(texts()).toEqual(['Stored question', 'Stored answer']);
    expect(conversations.messages()[1].sources?.[0].fileName).toBe('manual.pdf');
  });

  it('ignores a conversation that arrives after another one was opened', () => {
    conversations.open('c1');
    conversations.open('c2');
    const first = http.expectOne((req) => req.url.endsWith('/conversations/c1'));
    const second = http.expectOne((req) => req.url.endsWith('/conversations/c2'));

    // The responses arrive in the opposite order to the clicks.
    second.flush(detail('c2', [message('user', 'From c2')]));
    first.flush(detail('c1', [message('user', 'From c1')]));

    expect(conversations.selectedId()).toBe('c2');
    expect(texts()).toEqual(['From c2']);
  });

  it('starts a new conversation without calling the API', () => {
    conversations.open('c1');
    http
      .expectOne((req) => req.url.endsWith('/conversations/c1'))
      .flush(detail('c1', [message('user', 'Old question')]));

    conversations.startNew();

    expect(conversations.selectedId()).toBeNull();
    expect(conversations.selectedTitle()).toBe('New conversation');
    expect(conversations.messages()).toEqual([]);
  });

  it('clears the chat when the open conversation is deleted', () => {
    conversations.load();
    http
      .expectOne((req) => req.method === 'GET' && req.url.endsWith('/conversations'))
      .flush([conversation('c1')]);
    conversations.open('c1');
    http
      .expectOne((req) => req.url.endsWith('/conversations/c1'))
      .flush(detail('c1', [message('user', 'Question')]));

    conversations.delete('c1');
    http
      .expectOne((req) => req.method === 'DELETE' && req.url.endsWith('/conversations/c1'))
      .flush(null, { status: 204, statusText: 'No Content' });

    expect(conversations.list()).toEqual([]);
    expect(conversations.selectedId()).toBeNull();
    expect(conversations.messages()).toEqual([]);
  });
});
