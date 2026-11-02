import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { NotebookResponse } from '../../core/api';
import { Workspace } from './workspace';

describe('Workspace', () => {
  const research: NotebookResponse = {
    notebookId: 'n1',
    title: 'Thesis research',
    createdAt: '2026-11-02T10:00:00Z',
    documentCount: 0,
  };

  let fixture: ComponentFixture<Workspace>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Workspace],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(Workspace);
    fixture.componentRef.setInput('notebookId', 'n1');
    http = TestBed.inject(HttpTestingController);
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  function notebookRequest() {
    return http.expectOne((req) => req.method === 'GET' && req.url.endsWith('/notebooks/n1'));
  }

  it('shows the notebook with its own documents and conversations', async () => {
    notebookRequest().flush(research);
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Thesis research');
    http.expectOne((req) => req.url.endsWith('/notebooks/n1/documents')).flush([]);
    http.expectOne((req) => req.url.endsWith('/notebooks/n1/conversations')).flush([]);
  });

  it('goes back to the list when the notebook cannot be loaded', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    notebookRequest().flush(
      { message: 'Notebook not found.' },
      { status: 404, statusText: 'Not Found' },
    );
    await fixture.whenStable();

    // http.verify() in afterEach also proves the children never asked for its content.
    expect(navigate).toHaveBeenCalledWith(['/']);
  });
});

describe('Workspace route', () => {
  it('takes the notebook id from the URL', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(
          [{ path: 'notebooks/:notebookId', component: Workspace }],
          withComponentInputBinding(),
        ),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    const http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/notebooks/n7');

    http.expectOne((req) => req.url.endsWith('/notebooks/n7'));
    http.verify();
  });
});
