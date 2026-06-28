export interface Resource {
  id: string;
  code: string;
}

export interface CreateResourceRequest {
  code: string;
}

export interface UpdateResourceRequest {
  code: string;
}

export interface Role {
  id: string;
  code: string;
  name: string;
  description: string;
  resources: ResourceEntry[];
}

export interface ResourceEntry {
  id: string;
  code: string;
}

export interface CreateRoleRequest {
  code: string;
  name: string;
  description: string;
}

export interface UpdateRoleRequest {
  code: string;
  name: string;
  description: string;
}

export interface AssignResourceRequest {
  resource_id: string;
}

export interface Profile {
  id: string;
  userId?: string;
  firstName: string;
  lastName: string;
  identityDocument: string;
  taxId: string | null;
  whatsappPhone: string;
  fullAddress: string;
  roles: RoleEntry[];
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface RoleEntry {
  id: string;
  code: string;
  name: string;
}

export interface CreateProfileRequest {
  first_name: string;
  last_name: string;
  identity_document: string;
  tax_id: string | null;
  whatsapp_phone: string;
  full_address: string;
}

export interface UpdateProfileRequest {
  first_name: string;
  last_name: string;
  identity_document: string;
  tax_id: string | null;
  whatsapp_phone: string;
  full_address: string;
}

export interface AssignRoleRequest {
  role_id: string;
}

export interface AssignProfileRequest {
  profile_id: string;
}
