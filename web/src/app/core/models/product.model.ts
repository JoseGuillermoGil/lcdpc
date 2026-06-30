export interface Product {
  productId: string;
  name: string;
  sku: string;
  wholesaleCommercialType: string;
  isActive: boolean;
  img: string | null;
  categoryId: string | null;
  branchId: string | null;
  stock: number;
  stockAvailable: number;
  stockBlocked: number;
  baseUnitId: string | null;
}

export interface CreateProductRequest {
  name: string;
  sku: string;
  wholesale_commercial_type: string;
  img?: string | null;
  category_id?: string | null;
  branch_id?: string | null;
  stock?: number;
  stock_available?: number;
  stock_blocked?: number;
  base_unit_id?: string | null;
  is_active?: boolean;
}
