import { apiGet, apiPost, buildQueryString, serverApiGet } from './client';

export interface InventoryRecord {
  productId: string;
  onHand: number;
  reserved: number;
  available: number;
}

export interface PaginatedInventory {
  items: InventoryRecord[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

interface InventoryApiRecord {
  productId: string;
  onHandQuantity: number;
  reservedQuantity: number;
  availableQuantity: number;
}

interface InventoryApiListResponse {
  items: InventoryApiRecord[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

function mapInventoryRecord(item: InventoryApiRecord): InventoryRecord {
  return {
    productId: item.productId,
    onHand: item.onHandQuantity,
    reserved: item.reservedQuantity,
    available: item.availableQuantity,
  };
}

export async function getStock(productId: string): Promise<InventoryRecord> {
  const item = await apiGet<InventoryApiRecord>(`/inventory/${productId}`, { skipAuth: true });
  return mapInventoryRecord(item);
}

export async function getStockServer(productId: string, token?: string): Promise<InventoryRecord> {
  const item = await serverApiGet<InventoryApiRecord>(`/inventory/${productId}`, token);
  return mapInventoryRecord(item);
}

export async function listInventory(filters: { depletedOnly?: boolean; page?: number; pageSize?: number } = {}): Promise<PaginatedInventory> {
  const qs = buildQueryString(filters);
  const response = await apiGet<InventoryApiListResponse>(`/inventory${qs}`);
  return {
    ...response,
    items: response.items.map(mapInventoryRecord),
  };
}

export async function adjustStock(productId: string, delta: number, reason: string): Promise<InventoryRecord> {
  const item = await apiPost<InventoryApiRecord>(`/inventory/${productId}/adjustments`, { delta, reason });
  return mapInventoryRecord(item);
}