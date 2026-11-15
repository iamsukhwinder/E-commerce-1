import { Injectable, Optional } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { EMPTY } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class ApiClient {
  readonly baseUrl = environment.apiUrl;
  constructor(@Optional() private readonly http: HttpClient | null) {}

  get<T>(path: string, params?: Record<string, string | number | boolean | null | undefined>) {
    let httpParams = new HttpParams();
    Object.entries(params || {}).forEach(([key, value]) => {
      if (value !== null && value !== undefined && value !== '') httpParams = httpParams.set(key, String(value));
    });
    return this.http ? this.http.get<T>(`${this.baseUrl}${path}`, { params: httpParams }) : EMPTY;
  }
  post<T>(path: string, body: unknown) { return this.http ? this.http.post<T>(`${this.baseUrl}${path}`, body) : EMPTY; }
  put<T>(path: string, body: unknown) { return this.http ? this.http.put<T>(`${this.baseUrl}${path}`, body) : EMPTY; }
  delete<T = void>(path: string) { return this.http ? this.http.delete<T>(`${this.baseUrl}${path}`) : EMPTY; }
}
