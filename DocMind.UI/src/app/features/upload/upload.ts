import { Component, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Api, ErrorResponse } from '../../core/api';

type UploadStatus = 'idle' | 'uploading' | 'success' | 'error';

@Component({
  imports: [],
  selector: 'app-upload',
  styleUrl: './upload.css',
  templateUrl: './upload.html',
})
export class Upload {
  private readonly api = inject(Api);

  protected readonly selectedFile = signal<File | null>(null);
  protected readonly status = signal<UploadStatus>('idle');
  protected readonly message = signal<string | null>(null);

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile.set(input.files?.item(0) ?? null);
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
        this.message.set(`${response.message} (documentId: ${response.documentId})`);
      },
      error: (error: HttpErrorResponse) => {
        this.status.set('error');
        const body = error.error as ErrorResponse | undefined;
        this.message.set(body?.message ?? 'Something went wrong while uploading the document.');
      },
    });
  }
}
