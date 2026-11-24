export interface SearchFilters {
  category_id?: string;
  status?: string;
  materials?: string[];
  printers?: string[];
  tags?: string[];
  price_min?: number;
  price_max?: number;
  min_rating?: number;
  [key: string]: unknown;
}

export interface SearchRequest {
  query: string;
  filters?: SearchFilters;
  limit?: number;
}

export interface ProductResult {
  product_id: string;
  name: string;
  slug: string;
  score: number;
  vector_score: number;
  graph_score: number;
  rerank_score: number;
  metadata: Record<string, unknown>;
}

export interface SearchResponse {
  results: ProductResult[];
  total_candidates: number;
  intent: string;
  answer: string | null;
}

export interface RecommendRequest {
  product_id: string;
  limit?: number;
}

export interface ChatRequest {
  query: string;
  filters?: SearchFilters;
}

export interface ChatResponse {
  answer: string;
  sources: ProductResult[];
  intent: string;
}
