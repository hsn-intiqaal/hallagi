import { inject, Injectable } from '@angular/core';
import { ApiService } from '@core/http/api.service';
import { PublicBookingInfo, PublicBookingResult, SlotList } from '@core/models';

/** Anonymous online-booking endpoints (no auth header required). */
@Injectable({ providedIn: 'root' })
export class BookingApi {
  private api = inject(ApiService);

  info(slug: string) { return this.api.get<PublicBookingInfo>(`/public/${encodeURIComponent(slug)}`); }
  slots(slug: string, branchId: string, date: string, serviceIds: string[], barberId?: string | null) {
    return this.api.get<SlotList>(`/public/${encodeURIComponent(slug)}/slots`, { branchId, date, serviceIds, barberId });
  }
  book(slug: string, body: { branchId: string; barberId: string | null; serviceIds: string[]; date: string; time: string; name: string; phone: string; notes: string | null }) {
    return this.api.post<PublicBookingResult>(`/public/${encodeURIComponent(slug)}/book`, body);
  }
  cancel(slug: string, reference: string, phone: string) {
    return this.api.post<{ message: string }>(`/public/${encodeURIComponent(slug)}/cancel`, { reference, phone });
  }
}
