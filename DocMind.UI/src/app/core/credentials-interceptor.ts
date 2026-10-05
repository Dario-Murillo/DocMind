import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { Auth } from './auth';

// Sends the session cookie with every request. In development the API is on another origin
// (localhost:5276 vs 4200), and browsers only attach cookies cross-origin when asked to.
// Also sends the user back to /login whenever the API reports the session is gone.
export const credentialsInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(Auth);
  const router = inject(Router);

  return next(req.clone({ withCredentials: true })).pipe(
    catchError((error: unknown) => {
      // /auth/ calls handle their own 401s: a wrong password on /login, or "no session" when
      // Auth.loadSession checks /manage/info.
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !req.url.includes('/auth/')
      ) {
        auth.clear();
        router.navigate(['/login']);
      }

      return throwError(() => error);
    }),
  );
};
