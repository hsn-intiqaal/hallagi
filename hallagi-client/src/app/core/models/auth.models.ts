// Identity, session and organization context

export type UserRole = 'SuperAdmin' | 'Owner' | 'BranchManager' | 'Cashier' | 'Barber';
export const UserRoleLabel: Record<UserRole, string> = {
  SuperAdmin: 'مالك المنصة',
  Owner: 'مالك',
  BranchManager: 'مدير فرع',
  Cashier: 'كاشير',
  Barber: 'حلاق'
};

export interface Branch { id: string; name: string; city: string; address?: string; phone?: string | null; opensAt?: string; closesAt?: string; status?: string; barberCount?: number; userCount?: number; hasOpenShift?: boolean; }

export interface Profile { id: string; fullName: string; email: string; phone?: string; }
export interface LoginResult {
  token: string;
  refreshToken: string;
  profile: Profile;
  member: { id: string | null; role: UserRole; organizationId: string | null; organizationName: string };
  branches: Branch[];
  expiresInMinutes: number;
}
export interface Me {
  profile: Profile;
  role: UserRole;
  organization: {
    id: string; name: string; currency: string; timeZone: string;
    logoUrl?: string | null;
    primaryColor?: string | null;
    secondaryColor?: string | null;
    coverImageUrl?: string | null;
    slug?: string | null;
  } | null;
  branchIds: string[];
  branches: Branch[];
  allBranches: boolean;
  isSuperAdmin: boolean;
  permissions: Record<string, boolean>;
}
