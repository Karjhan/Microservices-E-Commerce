export interface ApiError {
  error: string;
  /** Problem Details fields returned for 401/403 refusals. */
  title?: string;
  detail?: string;
  code: string;
  details?: Record<string, unknown>;
  /** Per-field messages on a 400 `validation_failed` response. */
  errors?: Record<string, string[]>;
}

export interface PaginatedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages?: number;
}
