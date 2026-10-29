import { inject, Injectable } from '@angular/core';
import { ApiService } from '@core/http/api.service';
import { Appointment, AppointmentDay, AppointmentInput, AppointmentStatus, SlotList } from '@core/models';

@Injectable({ providedIn: 'root' })
export class AppointmentsApi {
  private api = inject(ApiService);

  day(branchId: string, date: string) { return this.api.get<AppointmentDay>('/appointments', { branchId, date }); }
  get(id: string) { return this.api.get<Appointment>(`/appointments/${id}`); }
  slots(branchId: string, date: string, serviceIds: string[], barberId?: string | null, excludeId?: string | null) {
    return this.api.get<SlotList>('/appointments/slots', { branchId, date, serviceIds, barberId, excludeId });
  }
  create(input: AppointmentInput) { return this.api.post<{ id: string; reference: string; message: string }>('/appointments', input); }
  update(id: string, input: AppointmentInput) { return this.api.put<{ message: string }>(`/appointments/${id}`, input); }
  setStatus(id: string, status: AppointmentStatus, reason?: string | null, barberId?: string | null) {
    return this.api.post<{ message: string }>(`/appointments/${id}/status`, { status, reason: reason ?? null, barberId: barberId ?? null });
  }
}
