import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ConversationResponse } from '../../core/api';
import { Conversations } from '../../core/conversations';
import { ConversationList } from './conversation-list';

describe('ConversationList', () => {
  const stored: ConversationResponse = {
    conversationId: 'c1',
    title: 'What is DocMind?',
    createdAt: '2026-10-22T10:00:00Z',
    updatedAt: '2026-10-22T10:05:00Z',
  };

  let fixture: ComponentFixture<ConversationList>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ConversationList],
      providers: [provideHttpClient(), provideHttpClientTesting(), Conversations],
    }).compileComponents();

    fixture = TestBed.createComponent(ConversationList);
    http = TestBed.inject(HttpTestingController);
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  // ngOnInit asks for the list as soon as the component renders, so every test answers it first.
  async function respondWithConversations(conversations: ConversationResponse[]): Promise<void> {
    http
      .expectOne((req) => req.method === 'GET' && req.url.endsWith('/conversations'))
      .flush(conversations);
    await fixture.whenStable();
  }

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  it('loads and lists the conversations on init', async () => {
    await respondWithConversations([stored]);

    expect(element().textContent).toContain('What is DocMind?');
  });

  it('shows an empty state when there are no conversations', async () => {
    await respondWithConversations([]);

    expect(element().textContent).toContain('No conversations yet');
  });

  it('opens a conversation when it is clicked', async () => {
    await respondWithConversations([stored]);

    (element().querySelector('.conversation-item') as HTMLElement).click();
    http
      .expectOne((req) => req.method === 'GET' && req.url.endsWith('/conversations/c1'))
      .flush({ ...stored, messages: [] });
    await fixture.whenStable();

    expect(element().querySelector('.conversation-item')?.classList).toContain('selected');
  });

  it('deletes a conversation without opening it', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    await respondWithConversations([stored]);

    (
      element().querySelector('button[aria-label="Delete conversation"]') as HTMLButtonElement
    ).click();

    // The click must not reach the list item underneath, which would open the conversation.
    http.expectNone((req) => req.method === 'GET' && req.url.endsWith('/conversations/c1'));
    http
      .expectOne((req) => req.method === 'DELETE' && req.url.endsWith('/conversations/c1'))
      .flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();

    expect(element().textContent).not.toContain('What is DocMind?');
  });
});
