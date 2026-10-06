import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DocumentResponse } from '../../core/api';
import { Upload } from './upload';

describe('Upload', () => {
  const report: DocumentResponse = {
    documentId: 'd1',
    fileName: 'report.pdf',
    sizeBytes: 2048,
    createdAt: '2026-10-06T10:00:00Z',
  };

  let component: Upload;
  let fixture: ComponentFixture<Upload>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Upload],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(Upload);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  // ngOnInit asks for the list as soon as the component renders, so every test answers it first.
  async function respondWithDocuments(documents: DocumentResponse[]): Promise<void> {
    http
      .expectOne((req) => req.method === 'GET' && req.url.endsWith('/documents'))
      .flush(documents);
    await fixture.whenStable();
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  it('should create', async () => {
    await respondWithDocuments([]);

    expect(component).toBeTruthy();
  });

  it('loads and lists the documents on init', async () => {
    await respondWithDocuments([report]);

    expect(text()).toContain('report.pdf');
    expect(text()).toContain('2 KB');
  });

  it('shows an empty state when there are no documents', async () => {
    await respondWithDocuments([]);

    expect(text()).toContain('No documents yet');
  });

  it('reloads the list after uploading', async () => {
    await respondWithDocuments([report]);
    const uploaded: DocumentResponse = {
      documentId: 'd2',
      fileName: 'new.pdf',
      sizeBytes: 1024,
      createdAt: '2026-10-06T11:00:00Z',
    };

    component['setSelectedFile'](new File(['%PDF-'], 'new.pdf', { type: 'application/pdf' }));
    component['upload']();
    http
      .expectOne((req) => req.method === 'POST' && req.url.endsWith('/documents/upload'))
      .flush({ documentId: 'd2', fileName: 'new.pdf', message: 'Document indexed successfully.' });
    await respondWithDocuments([uploaded, report]);

    expect(text()).toContain('new.pdf');
    expect(text()).toContain('Document indexed successfully.');
  });

  it('removes a document after deleting it', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    await respondWithDocuments([report]);

    (
      fixture.nativeElement.querySelector('button[aria-label="Delete"]') as HTMLButtonElement
    ).click();
    http
      .expectOne((req) => req.method === 'DELETE' && req.url.endsWith('/documents/d1'))
      .flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();

    expect(text()).not.toContain('report.pdf');
  });

  it('does not delete when the confirmation is cancelled', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    await respondWithDocuments([report]);

    (
      fixture.nativeElement.querySelector('button[aria-label="Delete"]') as HTMLButtonElement
    ).click();

    http.expectNone((req) => req.method === 'DELETE');
    expect(text()).toContain('report.pdf');
  });
});
