import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private base = environment.apiUrl;

  get<T>(path: string, params?: Record<string, any>): Observable<T> {
    let p = new HttpParams();
    for (const [k, v] of Object.entries(params ?? {})) {
      if (v !== undefined && v !== null && v !== '') p = p.set(k, String(v));
    }
    return this.http.get<T>(`${this.base}${path}`, { params: p }).pipe(catchError(e => this.toFriendly(e)));
  }

  post<T>(path: string, body: any): Observable<T> {
    return this.http.post<T>(`${this.base}${path}`, body).pipe(catchError(e => this.toFriendly(e)));
  }

  put<T>(path: string, body: any): Observable<T> {
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
      else if (e.status === 409) message = 'هناك تعارض في البيانات. حدّث الصفحة وحاول مرة أخرى';
      else if (e.status >= 500) message = 'حدث خطأ في الخادم. حاول مرة أخرى';
      else message = 'لم نتمكن من إكمال العملية. تحقق من البيانات وحاول مرة أخرى';
    }
    return throwError(() => ({ status: e.status, message }));
  }
}
