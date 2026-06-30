export interface Bundle {
  bundleId: string;
  code: string;
  name: string;
  status: string;
  branchId: string | null;
  totalPrice: number;
  totalPriceCurrency: string;
  promotionalPrice: number | null;
  promotionalPriceCurrency: string | null;
  items: BundleItem[] | null;
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
