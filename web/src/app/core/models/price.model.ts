export interface ProductBranchPrice {
  id: string;
  productId: string;
  price1Unit: number;
  price1Currency: string;
  price2BoxBundlePiece: number;
  price2Currency: string;
  price3WholesaleFrom2: number;
  price3Currency: string;
  price4Wholesale: number | null;
  price4Currency: string | null;
  price4RequiresAgreement: boolean;
  validFrom: string;
  validUntil: string | null;
}

export interface CreatePriceRequest {
  product_id: string;
  price1_unit: number;
  price2_box_bundle_piece: number;
  price3_wholesale_from2: number;
  price4_wholesale?: number | null;
  price4_requires_agreement?: boolean;
  valid_from: string;
  valid_until?: string | null;
}
