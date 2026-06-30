export interface Category {
  categoryId: string;
  name: string;
  slug: string;
  sortOrder: number;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateCategoryRequest {
  name: string;
  slug: string;
  sort_order?: number;
}

export interface UpdateCategoryRequest {
  name?: string;
  slug?: string;
  sort_order?: number;
  is_active?: boolean;
}
