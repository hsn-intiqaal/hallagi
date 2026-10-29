import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { APP_CONFIG } from '@core/config/app-config';

/** Normalized error every API call fails with — components only ever show `message`. */
export interface ApiFailure { status: number; code?: string; message: string; }

/** Query-string values; empty / null / undefined ones are left out. */
export type QueryParams = Record<string, string | number | boolean | readonly string[] | null | undefined>;

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private base = inject(APP_CONFIG).apiUrl;

  get<T>(path: string, params?: QueryParams): Observable<T> {
    return this.http.get<T>(`${this.base}${path}`, { params: this.toParams(params) }).pipe(catchError(e => this.toFriendly(e)));
  }

  /**
   * Downloads a file through the normal HTTP pipeline (auth header + silent session renewal)
   * and saves it with the given name.
   */
  download(path: string, params: QueryParams, fileName: string): Observable<void> {
    return this.http.get(`${this.base}${path}`, { params: this.toParams(params), responseType: 'blob' }).pipe(
      map(blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        a.click();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
      }),
      catchError(e => this.toFriendly(e))
    );
  }

  private toParams(params?: QueryParams): HttpParams {
    let p = new HttpParams();
    for (const [k, v] of Object.entries(params ?? {})) {
      if (v === undefined || v === null || v === '') continue;
      // arrays become repeated keys: ?serviceIds=a&serviceIds=b (ASP.NET Core list binding)
      if (Array.isArray(v)) v.forEach(item => (p = p.append(k, String(item))));
      else p = p.set(k, String(v));
    }
    return p;
  }

  post<T>(path: string, body: unknown): Observable<T> {
    return this.http.post<T>(`${this.base}${path}`, body).pipe(catchError(e => this.toFriendly(e)));
  }

  put<T>(path: string, body: unknown): Observable<T> {
    return this.http.put<T>(`${this.base}${path}`, body).pipe(catchError(e => this.toFriendly(e)));
  }

  /** Converts API errors into Arabic user-facing messages. */
  toFriendly(e: HttpErrorResponse): Observable<never> {
    const serverMessage: string | undefined = e.error?.error?.message;
    let message = serverMessage;
    if (!message) {
      if (e.status === 0) message = 'تعذّر الاتصال بالخادم. تحقق من الشبكة وحاول مرة أخرى';
      else if (e.status === 401) message = 'انتهت جلسة الدخول. سجّل الدخول مرة أخرى';
      else if (e.status === 403) message = 'ليس لديك صلاحية لتنفيذ هذا الإجراء';
      else if (e.status === 404) message = 'العنصر المطلوب غير موجود';
      else if (e.status === 429) message = 'محاولات كثيرة. انتظر دقيقة ثم حاول مرة أخرى';
      else if (e.status === 409) message = 'هناك تعارض في البيانات. حدّث الصفحة وحاول مرة أخرى';
      else if (e.status >= 500) message = 'حدث خطأ في الخادم. حاول مرة أخرى';
      else message = 'لم نتمكن من إكمال العملية. تحقق من البيانات وحاول مرة أخرى';
    }
    return throwError((): ApiFailure => ({ status: e.status, code: e.error?.error?.code, message: message! }));
  }
}
