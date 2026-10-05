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

export interface AuthRequest {
  email: string;
  password: string;
}

export interface UserInfo {
  email: string;
  isEmailConfirmed: boolean;
}

// Shape of Identity's 400 responses on /auth/register (e.g. weak password, duplicate email).
export interface ValidationProblem {
  title?: string;
  errors?: Record<string, string[]>;
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

  register(request: AuthRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/auth/register`, request);
  }

  login(request: AuthRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/auth/login`, request, {
      params: { useCookies: true },
    });
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/auth/logout`, null);
  }

  getUserInfo(): Observable<UserInfo> {
    return this.http.get<UserInfo>(`${this.baseUrl}/auth/manage/info`);
  }
}
