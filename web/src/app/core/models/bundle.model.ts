export interface Bundle {
  bundleId: string;
  code: string;
  name: string;
  status: string;
  branchId: string | null;
  items: BundleItem[] | null;
  prices: BundlePrice[];
  img: string | null;
  categoryId: string | null;
  stock: number;
  stockAvailable: number;
  stockBlocked: number;
}

export interface BundleItem {
  id: string;
  bundleId: string;
  productId: string;
  quantity: number;
}

export interface BundlePrice {
  id: string;
  bundleId: string;
  priceCategoryId: string | null;
  amount: number;
}

export interface BundleItemRequest {
  product_id: string;
  quantity: number;
}

export interface CreateBundleRequest {
  code: string;
  name: string;
  items: BundleItemRequest[];
  branch_id?: string | null;
  stock?: number;
  stock_available?: number;
  stock_blocked?: number;
  img?: string | null;
  category_id?: string | null;
}
