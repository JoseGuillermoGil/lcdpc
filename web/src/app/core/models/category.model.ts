export interface Category {
  categoryId: string;
  name: string;
  slug: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateCategoryRequest {
  name: string;
  slug: string;
}

export interface UpdateCategoryRequest {
  name?: string;
  slug?: string;
}
