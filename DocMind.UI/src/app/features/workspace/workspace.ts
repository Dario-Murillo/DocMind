import { Component, OnInit, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSidenavModule } from '@angular/material/sidenav';
import { Router, RouterLink } from '@angular/router';
import { Api, NotebookResponse } from '../../core/api';
import { Conversations } from '../../core/conversations';
import { ConversationList } from '../conversation-list/conversation-list';
import { Query } from '../query/query';
import { Upload } from '../upload/upload';

@Component({
  imports: [
    ConversationList,
    MatButtonModule,
    MatIconModule,
    MatSidenavModule,
    Query,
    RouterLink,
    Upload,
  ],
  providers: [Conversations],
  selector: 'app-workspace',
  styleUrl: './workspace.css',
  templateUrl: './workspace.html',
})
export class Workspace implements OnInit {
  private readonly api = inject(Api);
  private readonly router = inject(Router);

  // Filled in from the route (withComponentInputBinding). It never changes during the component's
  // life: NotebookRouteReuse creates a new Workspace for each notebook.
  readonly notebookId = input.required<string>();

  protected readonly notebook = signal<NotebookResponse | null>(null);

  // The children only render once the notebook is known, so a notebook that was deleted or belongs
  // to another user goes back to the list without them firing requests that would fail too.
  ngOnInit(): void {
    this.api.getNotebook(this.notebookId()).subscribe({
      next: (notebook) => this.notebook.set(notebook),
      error: () => this.router.navigate(['/']),
    });
  }
}
