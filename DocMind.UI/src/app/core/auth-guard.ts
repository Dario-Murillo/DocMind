import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Observable, map, of } from 'rxjs';
import { Auth } from './auth';

// On the first navigation after a page load the session status is still 'unknown', so ask the
// API; afterwards the answer is already known and no request is needed.
function checkSession(auth: Auth): Observable<boolean> {
  return auth.sessionStatus() === 'unknown' ? auth.loadSession() : of(auth.isAuthenticated());
}

// Lets signed-in users through and sends everyone else to /login.
export const authGuard: CanActivateFn = () => {
  const loginPage = inject(Router).createUrlTree(['/login']);
  return checkSession(inject(Auth)).pipe(map((hasSession) => hasSession || loginPage));
};

// The reverse, for /login and /register: a signed-in user has nothing to do there.
export const guestGuard: CanActivateFn = () => {
  const home = inject(Router).createUrlTree(['/']);
  return checkSession(inject(Auth)).pipe(map((hasSession) => !hasSession || home));
};
