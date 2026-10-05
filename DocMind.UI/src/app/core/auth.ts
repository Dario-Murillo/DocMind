import { Service, computed, inject, signal } from '@angular/core';
import { Observable, catchError, finalize, map, of, switchMap, tap } from 'rxjs';
import { Api, AuthRequest, UserInfo } from './api';

export type SessionStatus = 'unknown' | 'authenticated' | 'anonymous';

// Holds who is signed in. The session itself lives in an HttpOnly cookie the UI can't read, so
// the only way to know whether one exists — e.g. after a page reload — is to ask the API.
@Service()
export class Auth {
  private readonly api = inject(Api);

  private readonly user = signal<UserInfo | null>(null);
  private readonly status = signal<SessionStatus>('unknown');

  readonly currentUser = this.user.asReadonly();
  readonly sessionStatus = this.status.asReadonly();
  readonly isAuthenticated = computed(() => this.status() === 'authenticated');

  // Resolves to whether a session exists. Any error counts as "no session": if the API is down
  // the user lands on the login screen, which then reports the failure when they try to log in.
  loadSession(): Observable<boolean> {
    return this.api.getUserInfo().pipe(
      tap((user) => this.setUser(user)),
      map(() => true),
      catchError(() => {
        this.clear();
        return of(false);
      }),
    );
  }

  // Errors (e.g. 401 for wrong credentials) propagate so the login screen can show them.
  login(request: AuthRequest): Observable<UserInfo> {
    return this.api.login(request).pipe(
      switchMap(() => this.api.getUserInfo()),
      tap((user) => this.setUser(user)),
    );
  }

  // Identity's /register doesn't sign the user in, so log in right after for a smoother flow.
  register(request: AuthRequest): Observable<UserInfo> {
    return this.api.register(request).pipe(switchMap(() => this.login(request)));
  }

  // Clears local state even if the request fails: the user asked to leave either way.
  logout(): Observable<void> {
    return this.api.logout().pipe(finalize(() => this.clear()));
  }

  // Also called by the HTTP interceptor when the API answers 401 (the cookie expired).
  clear(): void {
    this.user.set(null);
    this.status.set('anonymous');
  }

  private setUser(user: UserInfo): void {
    this.user.set(user);
    this.status.set('authenticated');
  }
}
