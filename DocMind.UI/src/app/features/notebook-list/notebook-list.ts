import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { Api, ErrorResponse, NotebookResponse } from '../../core/api';

// Matches Notebook.MaxTitleLength in DocMind.Core, so the API's 400 is never what the user reads.
const maxTitleLength = 100;

@Component({
  imports: [
    DatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    RouterLink,
  ],
  selector: 'app-notebook-list',
  styleUrl: './notebook-list.css',
  templateUrl: './notebook-list.html',
})
export class NotebookList implements OnInit {
  private readonly api = inject(Api);
  private readonly router = inject(Router);

  protected readonly maxTitleLength = maxTitleLength;
  protected readonly notebooks = signal<NotebookResponse[]>([]);
  protected readonly loaded = signal(false);
  protected readonly title = signal('');
  protected readonly creating = signal(false);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.api.getNotebooks().subscribe({
      next: (notebooks) => {
        this.notebooks.set(notebooks);
        this.loaded.set(true);
      },
      error: () => this.error.set('Could not load your notebooks.'),
    });
  }

  protected onTitleInput(event: Event): void {
    this.title.set((event.target as HTMLInputElement).value);
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();

    const title = this.title().trim();
    if (!title || this.creating()) {
      return;
    }

    this.creating.set(true);
    this.error.set(null);

    // A new notebook has nothing to show yet, so the next step is always to open it.
    this.api.createNotebook({ title }).subscribe({
      next: (notebook) => this.router.navigate(['/notebooks', notebook.notebookId]),
      error: (error: HttpErrorResponse) => {
        this.creating.set(false);
        this.error.set(this.messageFrom(error, 'Could not create the notebook.'));
      },
    });
  }

  protected renameNotebook(notebook: NotebookResponse): void {
    const title = prompt('Notebook title', notebook.title)?.trim();
    if (!title || title === notebook.title) {
      return;
    }

    if (title.length > maxTitleLength) {
      this.error.set(`Notebook titles can't be longer than ${maxTitleLength} characters.`);
      return;
    }

    this.error.set(null);
    this.api.renameNotebook(notebook.notebookId, { title }).subscribe({
      next: (renamed) =>
        this.notebooks.update((notebooks) =>
          notebooks.map((existing) =>
            existing.notebookId === renamed.notebookId ? renamed : existing,
          ),
        ),
      error: (error: HttpErrorResponse) =>
        this.error.set(this.messageFrom(error, 'Could not rename the notebook.')),
    });
  }

  protected deleteNotebook(notebook: NotebookResponse): void {
    if (
      !confirm(`Delete "${notebook.title}"? All of its documents and conversations will be lost.`)
    ) {
      return;
    }

    this.error.set(null);
    this.api.deleteNotebook(notebook.notebookId).subscribe({
      next: () =>
        this.notebooks.update((notebooks) =>
          notebooks.filter((existing) => existing.notebookId !== notebook.notebookId),
        ),
      error: (error: HttpErrorResponse) =>
        this.error.set(this.messageFrom(error, 'Could not delete the notebook.')),
    });
  }

  private messageFrom(error: HttpErrorResponse, fallback: string): string {
    return (error.error as ErrorResponse | undefined)?.message ?? fallback;
  }
}
