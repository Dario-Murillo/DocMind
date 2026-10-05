import { Component, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Auth } from '../../../core/auth';

@Component({
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    RouterLink,
  ],
  selector: 'app-login',
  styleUrl: './login.css',
  templateUrl: './login.html',
})
export class Login {
  private readonly auth = inject(Auth);
  private readonly router = inject(Router);

  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected onEmailInput(event: Event): void {
    this.email.set((event.target as HTMLInputElement).value);
  }

  protected onPasswordInput(event: Event): void {
    this.password.set((event.target as HTMLInputElement).value);
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();

    const email = this.email().trim();
    const password = this.password();

    if (!email || !password || this.submitting()) {
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    this.auth.login({ email, password }).subscribe({
      next: () => this.router.navigate(['/']),
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false);
        this.error.set(this.describeLoginError(error));
      },
    });
  }

  // Identity answers 401 for both unknown emails and wrong passwords on purpose, so this screen
  // can't be used to find out which emails have an account. After 5 failures the account is
  // locked for 5 minutes and the 401 says "LockedOut" instead of "Failed".
  private describeLoginError(error: HttpErrorResponse): string {
    if (error.status !== 401) {
      return 'Could not reach the server. Please try again.';
    }

    return error.error?.detail === 'LockedOut'
      ? 'Too many failed attempts. Try again in a few minutes.'
      : 'Incorrect email or password.';
  }
}
