export interface Bundle {
  bundleId: string;
  code: string;
  name: string;
  status: string;
  enabledBranchIds: string[];
  totalPrice: number;
  totalPriceCurrency: string;
  promotionalPrice: number | null;
  promotionalPriceCurrency: string | null;
  items: BundleItem[] | null;
  img: string | null;
  categoryId: string | null;
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
  enabled_branch_ids?: string[];
  img?: string | null;
  category_id?: string | null;
}
