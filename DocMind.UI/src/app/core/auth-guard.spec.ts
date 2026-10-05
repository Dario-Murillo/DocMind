import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import {
  ActivatedRouteSnapshot,
  CanActivateFn,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { Observable, firstValueFrom } from 'rxjs';
import { authGuard, guestGuard } from './auth-guard';

describe('auth guards', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  // Guards use inject(), so they must run inside an injection context, as the router does.
  const runGuard = (guard: CanActivateFn) =>
    TestBed.runInInjectionContext(() =>
      guard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    ) as Observable<boolean | UrlTree>;

  it('lets the user through when the API confirms a session', async () => {
    const result = firstValueFrom(runGuard(authGuard));

    http
      .expectOne((req) => req.url.endsWith('/auth/manage/info'))
      .flush({ email: 'ana@test.local', isEmailConfirmed: false });

    expect(await result).toBe(true);
  });

  it('redirects to /login when there is no session', async () => {
    const result = firstValueFrom(runGuard(authGuard));

    http
      .expectOne((req) => req.url.endsWith('/auth/manage/info'))
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    const redirect = (await result) as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(redirect)).toBe('/login');
  });

  it('keeps signed-in users away from the guest pages', async () => {
    const result = firstValueFrom(runGuard(guestGuard));

    http
      .expectOne((req) => req.url.endsWith('/auth/manage/info'))
      .flush({ email: 'ana@test.local', isEmailConfirmed: false });

    const redirect = (await result) as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(redirect)).toBe('/');
  });

  it('lets anonymous users reach the guest pages', async () => {
    const result = firstValueFrom(runGuard(guestGuard));

    http
      .expectOne((req) => req.url.endsWith('/auth/manage/info'))
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(await result).toBe(true);
  });
});
