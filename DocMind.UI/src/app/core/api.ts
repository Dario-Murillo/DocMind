import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface UploadDocumentResponse {
  documentId: string;
  fileName: string;
  message: string;
}

export interface NotebookRequest {
  title: string;
}

export interface NotebookResponse {
  notebookId: string;
  title: string;
  createdAt: string;
  documentCount: number;
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

  getNotebooks(): Observable<NotebookResponse[]> {
    return this.http.get<NotebookResponse[]>(`${this.baseUrl}/notebooks`);
  }

  getNotebook(notebookId: string): Observable<NotebookResponse> {
    return this.http.get<NotebookResponse>(`${this.baseUrl}/notebooks/${notebookId}`);
  }

  createNotebook(request: NotebookRequest): Observable<NotebookResponse> {
    return this.http.post<NotebookResponse>(`${this.baseUrl}/notebooks`, request);
  }

  renameNotebook(notebookId: string, request: NotebookRequest): Observable<NotebookResponse> {
    return this.http.patch<NotebookResponse>(`${this.baseUrl}/notebooks/${notebookId}`, request);
  }

  deleteNotebook(notebookId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/notebooks/${notebookId}`);
  }

  uploadDocument(notebookId: string, file: File): Observable<UploadDocumentResponse> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<UploadDocumentResponse>(
      `${this.baseUrl}/notebooks/${notebookId}/documents`,
      formData,
    );
  }

  getDocuments(notebookId: string): Observable<DocumentResponse[]> {
    return this.http.get<DocumentResponse[]>(`${this.baseUrl}/notebooks/${notebookId}/documents`);
  }

  // A URL for an <a href> rather than an HttpClient call: the browser downloads the file itself,
  // sending the session cookie, and the API's Content-Disposition header keeps it on the page.
  documentFileUrl(documentId: string): string {
    return `${this.baseUrl}/documents/${documentId}/file`;
  }

  deleteDocument(documentId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/documents/${documentId}`);
  }

  getConversations(notebookId: string): Observable<ConversationResponse[]> {
    return this.http.get<ConversationResponse[]>(
      `${this.baseUrl}/notebooks/${notebookId}/conversations`,
    );
  }

  getConversation(conversationId: string): Observable<ConversationDetailResponse> {
    return this.http.get<ConversationDetailResponse>(
      `${this.baseUrl}/conversations/${conversationId}`,
    );
  }

  startConversation(notebookId: string, request: AskRequest): Observable<ExchangeResponse> {
    return this.http.post<ExchangeResponse>(
      `${this.baseUrl}/notebooks/${notebookId}/conversations`,
      request,
    );
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
