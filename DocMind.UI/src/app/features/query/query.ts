import { Component, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Api, ErrorResponse, QueryStatus, ChatMessage } from '../../core/api';
@Component({
  imports: [DecimalPipe, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatProgressSpinnerModule],
  selector: 'app-query',
  styleUrl: './query.css',
  templateUrl: './query.html',
})
export class Query {
  private readonly api = inject(Api);

  protected readonly question = signal('');
  protected readonly topK = signal(5);
  protected readonly status = signal<QueryStatus>('idle');
  protected readonly messages = signal<ChatMessage[]>([]);

  protected onQuestionInput(event: Event): void {
    this.question.set((event.target as HTMLInputElement).value);
  }

  protected onTopKInput(event: Event): void {
    const value = Number((event.target as HTMLInputElement).value);
    this.topK.set(Number.isFinite(value) && value > 0 ? value : 5);
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    this.ask();
  }

  protected ask(): void {
    const question = this.question().trim();
    if (!question || this.status() === 'loading') {
      return;
    }

    this.messages.update((messages) => [...messages, { role: 'user', text: question }]);
    this.question.set('');
    this.status.set('loading');

    this.api.query({ question, topK: this.topK() }).subscribe({
      next: (response) => {
        this.status.set('idle');
        this.messages.update((messages) => [
          ...messages,
          { role: 'assistant', text: response.answer, sources: response.sources },
        ]);
      },
      error: (error: HttpErrorResponse) => {
        this.status.set('idle');
        const body = error.error as ErrorResponse | undefined;
        this.messages.update((messages) => [
          ...messages,
          {
            role: 'assistant',
            text: body?.message ?? 'Something went wrong while asking the question.',
            error: true,
          },
        ]);
      },
    });
  }
}
