import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface UploadDocumentResponse {
  documentId: string;
  fileName: string;
  message: string;
}

export interface AskRequest {
  question: string;
  topK?: number;
}

export interface SourceResult {
  // null once the cited document has been deleted; fileName and excerpt are copies and remain.
  documentId: string | null;
  fileName: string;
  sequenceNumber: number;
  score: number;
  excerpt: string;
}

export interface MessageResponse {
  messageId: string;
  role: 'user' | 'assistant';
  content: string;
  createdAt: string;
  sources: SourceResult[];
}

export interface ConversationResponse {
  conversationId: string;
  title: string;
  createdAt: string;
  updatedAt: string;
}

export interface ConversationDetailResponse extends ConversationResponse {
  messages: MessageResponse[];
}

export interface ExchangeResponse {
  conversation: ConversationResponse;
  question: MessageResponse;
  answer: MessageResponse;
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

export interface DocumentResponse {
  documentId: string;
  fileName: string;
  sizeBytes: number;
  createdAt: string;
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

  getDocuments(): Observable<DocumentResponse[]> {
    return this.http.get<DocumentResponse[]>(`${this.baseUrl}/documents`);
  }

  // A URL for an <a href> rather than an HttpClient call: the browser downloads the file itself,
  // sending the session cookie, and the API's Content-Disposition header keeps it on the page.
  documentFileUrl(documentId: string): string {
    return `${this.baseUrl}/documents/${documentId}/file`;
  }

  deleteDocument(documentId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/documents/${documentId}`);
  }

  getConversations(): Observable<ConversationResponse[]> {
    return this.http.get<ConversationResponse[]>(`${this.baseUrl}/conversations`);
  }

  getConversation(conversationId: string): Observable<ConversationDetailResponse> {
    return this.http.get<ConversationDetailResponse>(
      `${this.baseUrl}/conversations/${conversationId}`,
    );
  }

  startConversation(request: AskRequest): Observable<ExchangeResponse> {
    return this.http.post<ExchangeResponse>(`${this.baseUrl}/conversations`, request);
  }

  addMessage(conversationId: string, request: AskRequest): Observable<ExchangeResponse> {
    return this.http.post<ExchangeResponse>(
      `${this.baseUrl}/conversations/${conversationId}/messages`,
      request,
    );
  }

  deleteConversation(conversationId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/conversations/${conversationId}`);
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
