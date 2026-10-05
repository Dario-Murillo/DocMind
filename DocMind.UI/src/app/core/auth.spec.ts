import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Auth } from './auth';

describe('Auth', () => {
  let auth: Auth;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    auth = TestBed.inject(Auth);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('starts with an unknown session', () => {
    expect(auth.sessionStatus()).toBe('unknown');
  });

  it('logs in with cookies and loads the current user', () => {
    auth.login({ email: 'ana@test.local', password: 'Passw0rd!' }).subscribe();

    const login = http.expectOne((req) => req.url.endsWith('/auth/login'));
    expect(login.request.params.get('useCookies')).toBe('true');
    login.flush(null);
    http
      .expectOne((req) => req.url.endsWith('/auth/manage/info'))
      .flush({ email: 'ana@test.local', isEmailConfirmed: false });

    expect(auth.sessionStatus()).toBe('authenticated');
    expect(auth.currentUser()?.email).toBe('ana@test.local');
  });

  it('treats a 401 from the API as no session', () => {
    let hasSession: boolean | undefined;
    auth.loadSession().subscribe((result) => (hasSession = result));

    http
      .expectOne((req) => req.url.endsWith('/auth/manage/info'))
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(hasSession).toBe(false);
    expect(auth.sessionStatus()).toBe('anonymous');
  });

  it('clears the session on logout', () => {
    auth.logout().subscribe();

    http
      .expectOne((req) => req.url.endsWith('/auth/logout'))
      .flush(null, { status: 204, statusText: 'No Content' });

    expect(auth.sessionStatus()).toBe('anonymous');
    expect(auth.currentUser()).toBeNull();
  });
});
