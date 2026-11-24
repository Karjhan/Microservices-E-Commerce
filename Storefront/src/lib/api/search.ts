import { apiPost } from './client';
import type { SearchRequest, SearchResponse, ChatRequest, ChatResponse, RecommendRequest } from '@/types/search';

export async function search(request: SearchRequest): Promise<SearchResponse> {
  return apiPost<SearchResponse>('/graphrag/api/search', request, { skipAuth: true });
}

export async function chat(request: ChatRequest): Promise<ChatResponse> {
  return apiPost<ChatResponse>('/graphrag/api/chat', request, { skipAuth: true });
}

export async function recommend(request: RecommendRequest): Promise<SearchResponse> {
  return apiPost<SearchResponse>('/graphrag/api/recommend', request, { skipAuth: true });
}
