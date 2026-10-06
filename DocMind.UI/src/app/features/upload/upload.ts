import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Api, ErrorResponse, UploadStatus, DocumentResponse } from '../../core/api';

@Component({
  imports: [
    DatePipe,
    DecimalPipe,
    MatButtonModule,
    MatIconModule,
    MatListModule,
    MatProgressSpinnerModule,
  ],
  selector: 'app-upload',
  styleUrl: './upload.css',
  templateUrl: './upload.html',
})
export class Upload implements OnInit {
  private readonly api = inject(Api);

  protected readonly selectedFile = signal<File | null>(null);
  protected readonly status = signal<UploadStatus>('idle');
  protected readonly message = signal<string | null>(null);
  protected readonly documents = signal<DocumentResponse[]>([]);
  protected readonly isDragOver = signal(false);

  ngOnInit(): void {
    this.loadDocuments();
  }

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.setSelectedFile(input.files?.item(0) ?? null);
  }

  protected onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(true);
  }

  protected onDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(false);
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(false);
    this.setSelectedFile(event.dataTransfer?.files.item(0) ?? null);
  }

  private setSelectedFile(file: File | null): void {
    this.selectedFile.set(file);
    this.status.set('idle');
    this.message.set(null);
  }

  protected upload(): void {
    const file = this.selectedFile();
    if (!file) {
      return;
    }

    this.status.set('uploading');
    this.message.set(null);

    this.api.uploadDocument(file).subscribe({
      next: (response) => {
        this.status.set('success');
        this.message.set(response.message);
        this.loadDocuments();
      },
      error: (error: HttpErrorResponse) => {
        this.status.set('error');
        const body = error.error as ErrorResponse | undefined;
        this.message.set(body?.message ?? 'Something went wrong while uploading the document.');
      },
    });
  }

  protected fileUrl(documentId: string): string {
    return this.api.documentFileUrl(documentId);
  }

  protected deleteDocument(document: DocumentResponse): void {
    if (!confirm(`Delete "${document.fileName}"? It will no longer be used to answer questions.`)) {
      return;
    }

    this.api.deleteDocument(document.documentId).subscribe({
      next: () =>
        this.documents.update((documents) =>
          documents.filter((existing) => existing.documentId !== document.documentId),
        ),
      error: (error: HttpErrorResponse) => {
        this.status.set('error');
        const body = error.error as ErrorResponse | undefined;
        this.message.set(body?.message ?? 'Something went wrong while deleting the document.');
      },
    });
  }

  private loadDocuments(): void {
    this.api.getDocuments().subscribe({
      next: (documents) => this.documents.set(documents),
      error: () => {
        this.status.set('error');
        this.message.set('Could not load your documents.');
      },
    });
  }
}
