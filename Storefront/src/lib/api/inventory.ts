import { apiGet, apiPost, apiPut, buildQueryString, serverApiGet } from './client';

export type StockStatus = 'ok' | 'low' | 'out';
export type StockStatusFilter = 'all' | 'low' | 'out';

export interface PublicStock {
  productId: string;
  available: number;
  inStock: boolean;
}

export interface InventoryRecord {
  productId: string;
  onHand: number;
  reserved: number;
  available: number;
  lowStockThreshold: number;
  status: StockStatus;
  version: string;
  updatedAt: string;
}

export interface PaginatedInventory {
  items: InventoryRecord[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export type StockMovementType = 'Adjust' | 'Reserve' | 'Release' | 'Deduct';

export interface StockMovement {
  id: string;
  occurredAt: string;
  type: StockMovementType;
  delta: number;
  reason: string | null;
  actorId: string | null;
  actor: string | null;
  orderId: string | null;
  resultingOnHand: number;
  reservedAfter: number;
}

export interface PaginatedStockMovements {
  items: StockMovement[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface InventoryFilters {
  status?: StockStatusFilter;
  productIds?: string[];
  page?: number;
  pageSize?: number;
}

export type AdjustMode = 'delta' | 'set';

export interface AdjustStockInput {
  mode: AdjustMode;
  quantity: number;
  reason: string;
  expectedVersion?: string;
}

interface InventoryApiRecord {
  productId: string;
  onHandQuantity: number;
  reservedQuantity: number;
  availableQuantity: number;
  lowStockThreshold: number;
  status: StockStatus;
  version: string;
  updatedAt: string;
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
    lowStockThreshold: item.lowStockThreshold,
    status: item.status,
    version: item.version,
    updatedAt: item.updatedAt,
  };
}

export async function getStock(productId: string): Promise<PublicStock> {
  return apiGet<PublicStock>(`/inventory/${productId}`, { skipAuth: true });
}

export async function getStockServer(productId: string, token?: string): Promise<PublicStock> {
  return serverApiGet<PublicStock>(`/inventory/${productId}`, token);
}

export async function getAdminStock(productId: string): Promise<InventoryRecord> {
  const item = await apiGet<InventoryApiRecord>(`/inventory/${productId}`);
  return mapInventoryRecord(item);
}

export async function listInventory(filters: InventoryFilters = {}): Promise<PaginatedInventory> {
  const { status, productIds, ...rest } = filters;
  const qs = buildQueryString({
    ...rest,
    status: status && status !== 'all' ? status : undefined,
    productIds: productIds?.length ? productIds.join(',') : undefined,
  });
  const response = await apiGet<InventoryApiListResponse>(`/inventory${qs}`);
  return {
    ...response,
    items: response.items.map(mapInventoryRecord),
  };
}

export async function adjustStock(productId: string, input: AdjustStockInput): Promise<InventoryRecord> {
  const item = await apiPost<InventoryApiRecord>(`/inventory/${productId}/adjustments`, input);
  return mapInventoryRecord(item);
}

export async function setLowStockThreshold(
  productId: string,
  lowStockThreshold: number
): Promise<InventoryRecord> {
  const item = await apiPut<InventoryApiRecord>(`/inventory/${productId}/threshold`, { lowStockThreshold });
  return mapInventoryRecord(item);
}

export async function getStockMovements(
  productId: string,
  paging: { page?: number; pageSize?: number } = {}
): Promise<PaginatedStockMovements> {
  return apiGet<PaginatedStockMovements>(`/inventory/${productId}/movements${buildQueryString(paging)}`);
}
