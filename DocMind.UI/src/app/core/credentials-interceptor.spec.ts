import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { Auth } from './auth';
import { credentialsInterceptor } from './credentials-interceptor';

describe('credentialsInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([credentialsInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  afterEach(() => controller.verify());

  it('sends credentials with every request', () => {
    http.get('/query').subscribe();

    const req = controller.expectOne('/query');
    expect(req.request.withCredentials).toBe(true);
    req.flush({});
  });

  it('redirects to the login page when the API answers 401', () => {
    http.post('/query', {}).subscribe({ error: () => undefined });

    controller.expectOne('/query').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(router.navigate).toHaveBeenCalledWith(['/login']);
    expect(TestBed.inject(Auth).sessionStatus()).toBe('anonymous');
  });

  it('leaves 401s from the auth endpoints to their callers', () => {
    http.post('/auth/login', {}).subscribe({ error: () => undefined });

    controller.expectOne('/auth/login').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(router.navigate).not.toHaveBeenCalled();
  });
});
