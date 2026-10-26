// Appointments, walk-in queue and public online booking

export type AppointmentStatus = 'Pending' | 'Confirmed' | 'Waiting' | 'InService' | 'Completed' | 'Cancelled' | 'NoShow';
export type AppointmentSource = 'Staff' | 'Online' | 'WalkIn';

export interface AppointmentItem { serviceId: string; serviceName: string; price: number; durationMinutes: number; }
export interface Appointment {
  id: string; reference: string; branchId: string;
  barberId: string | null; barberName: string | null;
  customerId: string | null; customerName: string; customerPhone: string | null; whatsApp: string | null;
  startAt: string; endAt: string; date: string; startTime: string; endTime: string; durationMinutes: number;
  status: AppointmentStatus; statusText: string; source: AppointmentSource; sourceText: string;
  notes: string | null; saleId: string | null; cancelReason: string | null;
  items: AppointmentItem[]; total: number;
}
export interface AppointmentDay {
  date: string;
  branch: { id: string; name: string; opensAt: string; closesAt: string };
  slotMinutes: number;
  barbers: { id: string; fullName: string }[];
  appointments: Appointment[];
  waitlist: Appointment[];
  summary: { total: number; completed: number; waiting: number; noShow: number; online: number; expectedRevenue: number };
}
export interface AppointmentInput {
  branchId: string; barberId: string | null; customerId: string | null;
  customerName: string | null; customerPhone: string | null;
  date: string | null; time: string | null; serviceIds: string[]; notes: string | null; walkIn: boolean;
}
export interface SlotList { date: string; durationMinutes: number; slots: { time: string; startAt: string; freeBarbers: number }[]; }

export interface PublicBookingInfo {
  organization: { name: string; logoUrl?: string | null; primaryColor?: string | null; secondaryColor?: string | null; coverImageUrl?: string | null };
  branches: { id: string; name: string; city: string; address?: string | null; phone?: string | null; opensAt: string; closesAt: string }[];
  services: { id: string; name: string; description?: string | null; price: number; durationMinutes: number }[];
  barbers: { id: string; fullName: string; branchIds: string[] }[];
  today: string;
  maxDaysAhead: number;
}
export interface PublicBookingResult {
  reference: string; date: string; time: string; branch: string; barber: string | null;
  services: string[]; total: number; message: string;
}
