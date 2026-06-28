export interface Product {
  productId: string;
  name: string;
  sku: string;
  baseMeasureType: string;
  wholesaleCommercialType: string;
  unitsPerBox: number | null;
  unitsPerBundle: number | null;
  isActive: boolean;
  img: string | null;
  categoryId: string | null;
}

export interface CreateProductRequest {
  name: string;
  sku: string;
  base_measure_type: string;
  wholesale_commercial_type: string;
  units_per_box?: number | null;
  units_per_bundle?: number | null;
  img?: string | null;
  category_id?: string | null;
}
