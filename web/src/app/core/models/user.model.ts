export interface AppUser {
  id: string;
  email: string;
  firstName: string | null;
  lastName: string | null;
  status: string;
  createdAtUtc: string;
}
