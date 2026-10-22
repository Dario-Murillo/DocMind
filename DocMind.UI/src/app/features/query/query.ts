import { Component, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Conversations } from '../../core/conversations';

@Component({
  imports: [
    DecimalPipe,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  selector: 'app-query',
  styleUrl: './query.css',
  templateUrl: './query.html',
})
export class Query {
  protected readonly conversations = inject(Conversations);

  protected readonly question = signal('');
  protected readonly topK = signal(5);

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
    if (!question || this.conversations.status() === 'loading') {
      return;
    }

    this.conversations.ask(question, this.topK());
    this.question.set('');
  }
}
