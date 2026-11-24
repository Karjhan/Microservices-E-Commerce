'use client';

import { useMutation } from '@tanstack/react-query';
import { chat } from '@/lib/api/search';
import type { ChatRequest, ChatResponse } from '@/types/search';

export function useChat() {
  return useMutation({
    mutationFn: (request: ChatRequest) => chat(request),
  });
}
