import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface UploadDocumentResponse {
  documentId: string;
  fileName: string;
  message: string;
}

export interface QueryRequest {
  question: string;
  topK?: number;
}

export interface SourceResult {
  documentId: string;
  sequenceNumber: number;
  score: number;
  excerpt: string;
}

export interface QueryResponse {
  answer: string;
  sources: SourceResult[];
}

export interface ErrorResponse {
  message: string;
}

export type QueryStatus = 'idle' | 'loading';

export interface ChatMessage {
  role: 'user' | 'assistant';
  text: string;
  sources?: SourceResult[];
  error?: boolean;
}

export type UploadStatus = 'idle' | 'uploading' | 'success' | 'error';

export interface IndexedDocument {
  documentId: string;
  fileName: string;
}

@Service()
export class Api {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  uploadDocument(file: File): Observable<UploadDocumentResponse> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<UploadDocumentResponse>(`${this.baseUrl}/documents/upload`, formData);
  }

  query(request: QueryRequest): Observable<QueryResponse> {
    return this.http.post<QueryResponse>(`${this.baseUrl}/query`, request);
  }
}
