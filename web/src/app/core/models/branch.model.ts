export interface Branch {
  id: string;
  storeName: string;
  taxId: string;
  address: string;
  contactPhone: string;
  secondaryContactPhone: string | null;
  businessHours: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateBranchRequest {
  store_name: string;
  tax_id: string;
  address: string;
  contact_phone: string;
  secondary_contact_phone?: string;
  business_hours: string;
}
