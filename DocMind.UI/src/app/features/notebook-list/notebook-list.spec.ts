import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { NotebookResponse } from '../../core/api';
import { NotebookList } from './notebook-list';

describe('NotebookList', () => {
  const research: NotebookResponse = {
    notebookId: 'n1',
    title: 'Thesis research',
    createdAt: '2026-11-02T10:00:00Z',
    documentCount: 1,
  };

  let fixture: ComponentFixture<NotebookList>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NotebookList],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(NotebookList);
    http = TestBed.inject(HttpTestingController);
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  // ngOnInit asks for the list as soon as the component renders, so every test answers it first.
  async function respondWithNotebooks(notebooks: NotebookResponse[]): Promise<void> {
    http
      .expectOne((req) => req.method === 'GET' && req.url.endsWith('/notebooks'))
      .flush(notebooks);
    await fixture.whenStable();
  }

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function click(ariaLabel: string): void {
    (element().querySelector(`button[aria-label="${ariaLabel}"]`) as HTMLButtonElement).click();
  }

  it('loads and lists the notebooks on init', async () => {
    await respondWithNotebooks([research, { ...research, notebookId: 'n2', documentCount: 3 }]);

    expect(element().textContent).toContain('Thesis research');
    expect(element().textContent).toContain('1 document ·');
    expect(element().textContent).toContain('3 documents ·');
    expect(element().querySelector('a')?.getAttribute('href')).toBe('/notebooks/n1');
  });

  it('shows an empty state when there are no notebooks', async () => {
    await respondWithNotebooks([]);

    expect(element().textContent).toContain('No notebooks yet');
  });

  it('opens a notebook right after creating it', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    await respondWithNotebooks([]);
    const input = element().querySelector('input') as HTMLInputElement;
    input.value = '  Thesis research  ';
    input.dispatchEvent(new Event('input'));

    element().querySelector('form')?.dispatchEvent(new Event('submit'));

    const create = http.expectOne((req) => req.method === 'POST' && req.url.endsWith('/notebooks'));
    expect(create.request.body).toEqual({ title: 'Thesis research' });
    create.flush(research);
    expect(navigate).toHaveBeenCalledWith(['/notebooks', 'n1']);
  });

  it('renames a notebook', async () => {
    vi.spyOn(window, 'prompt').mockReturnValue('New title');
    await respondWithNotebooks([research]);

    click('Rename notebook');
    const rename = http.expectOne(
      (req) => req.method === 'PATCH' && req.url.endsWith('/notebooks/n1'),
    );
    expect(rename.request.body).toEqual({ title: 'New title' });
    rename.flush({ ...research, title: 'New title' });
    await fixture.whenStable();

    expect(element().textContent).toContain('New title');
    expect(element().textContent).not.toContain('Thesis research');
  });

  it('does not rename when the prompt is cancelled', async () => {
    vi.spyOn(window, 'prompt').mockReturnValue(null);
    await respondWithNotebooks([research]);

    click('Rename notebook');

    http.expectNone((req) => req.method === 'PATCH');
  });

  it('rejects a title that is too long without calling the API', async () => {
    vi.spyOn(window, 'prompt').mockReturnValue('t'.repeat(101));
    await respondWithNotebooks([research]);

    click('Rename notebook');
    await fixture.whenStable();

    http.expectNone((req) => req.method === 'PATCH');
    expect(element().textContent).toContain("can't be longer than 100 characters");
  });

  it('removes a notebook after deleting it', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    await respondWithNotebooks([research]);

    click('Delete notebook');
    http
      .expectOne((req) => req.method === 'DELETE' && req.url.endsWith('/notebooks/n1'))
      .flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();

    expect(element().textContent).not.toContain('Thesis research');
  });

  it('does not delete when the confirmation is cancelled', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    await respondWithNotebooks([research]);

    click('Delete notebook');

    http.expectNone((req) => req.method === 'DELETE');
    expect(element().textContent).toContain('Thesis research');
  });
});
