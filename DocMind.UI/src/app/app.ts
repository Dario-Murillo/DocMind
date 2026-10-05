import { Component, inject, signal } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { Auth } from './core/auth';

@Component({
  imports: [MatButtonModule, RouterOutlet],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  private readonly router = inject(Router);
  protected readonly auth = inject(Auth);
  protected readonly title = signal('DocMind');

  // Goes to /login whether or not the request succeeds: Auth clears the local session either way.
  protected logout(): void {
    const toLogin = () => this.router.navigate(['/login']);
    this.auth.logout().subscribe({ complete: toLogin, error: toLogin });
  }
}
