import { Component, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ValidationProblem } from '../../../core/api';
import { Auth } from '../../../core/auth';

@Component({
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    RouterLink,
  ],
  selector: 'app-register',
  styleUrl: './register.css',
  templateUrl: './register.html',
})
export class Register {
  private readonly auth = inject(Auth);
  private readonly router = inject(Router);

  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly submitting = signal(false);
  protected readonly errors = signal<string[]>([]);

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
    this.errors.set([]);

    this.auth.register({ email, password }).subscribe({
      next: () => this.router.navigate(['/']),
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false);
        this.errors.set(this.describeRegisterErrors(error));
      },
    });
  }

  // Identity's 400 lists every rule the input broke, keyed by rule name (PasswordRequiresUpper,
  // DuplicateUserName, ...), each with a human-readable message.
  private describeRegisterErrors(error: HttpErrorResponse): string[] {
    const problem = error.error as ValidationProblem | null;
    if (error.status === 400 && problem?.errors) {
      return Object.values(problem.errors).flat();
    }

    return ['Could not reach the server. Please try again.'];
  }
}
