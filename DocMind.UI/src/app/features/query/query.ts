import { Component, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Api, ErrorResponse, SourceResult } from '../../core/api';

type QueryStatus = 'idle' | 'loading' | 'success' | 'error';

@Component({
  imports: [DecimalPipe],
  selector: 'app-query',
  styleUrl: './query.css',
  templateUrl: './query.html',
})
export class Query {
  private readonly api = inject(Api);

  protected readonly question = signal('');
  protected readonly topK = signal(5);
  protected readonly status = signal<QueryStatus>('idle');
  protected readonly answer = signal<string | null>(null);
  protected readonly sources = signal<SourceResult[]>([]);
  protected readonly errorMessage = signal<string | null>(null);

  protected onQuestionInput(event: Event): void {
    this.question.set((event.target as HTMLInputElement).value);
  }

  protected onTopKInput(event: Event): void {
    const value = Number((event.target as HTMLInputElement).value);
    this.topK.set(Number.isFinite(value) && value > 0 ? value : 5);
  }

  protected ask(): void {
    const question = this.question().trim();
    if (!question) {
      return;
    }

    this.status.set('loading');
    this.errorMessage.set(null);

    this.api.query({ question, topK: this.topK() }).subscribe({
      next: (response) => {
        this.status.set('success');
        this.answer.set(response.answer);
        this.sources.set(response.sources);
      },
      error: (error: HttpErrorResponse) => {
        this.status.set('error');
        const body = error.error as ErrorResponse | undefined;
        this.errorMessage.set(body?.message ?? 'Something went wrong while asking the question.');
      },
    });
  }
}
