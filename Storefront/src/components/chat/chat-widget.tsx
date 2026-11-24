'use client';

import { useState } from 'react';
import Link from 'next/link';
import { Send, X, MessageCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Badge } from '@/components/ui/badge';
import { useChat } from '@/hooks/queries/use-chat';
import type { ChatResponse } from '@/types/search';

interface Message {
  role: 'user' | 'assistant';
  content: string;
  sources?: ChatResponse['sources'];
}

export function ChatWidget() {
  const [isOpen, setIsOpen] = useState(false);
  const [messages, setMessages] = useState<Message[]>([]);
  const [input, setInput] = useState('');
  const chatMutation = useChat();

  const handleSend = async () => {
    if (!input.trim()) return;
    const userMessage: Message = { role: 'user', content: input.trim() };
    setMessages((prev) => [...prev, userMessage]);
    setInput('');

    try {
      const response = await chatMutation.mutateAsync({ query: userMessage.content });
      setMessages((prev) => [
        ...prev,
        { role: 'assistant', content: response.answer, sources: response.sources },
      ]);
    } catch {
      setMessages((prev) => [
        ...prev,
        { role: 'assistant', content: 'Sorry, I could not process your request. Please try again.' },
      ]);
    }
  };

  if (!isOpen) {
    return (
      <Button
        onClick={() => setIsOpen(true)}
        size="icon"
        className="fixed bottom-4 right-4 z-50 h-14 w-14 rounded-full shadow-lg sm:bottom-6 sm:right-6"
        aria-label="Open chat"
      >
        <MessageCircle className="h-6 w-6" />
      </Button>
    );
  }

  return (
    <div className="fixed inset-x-4 bottom-4 z-50 flex flex-col rounded-lg border bg-background shadow-xl h-[min(500px,calc(100dvh-5rem))] sm:inset-x-auto sm:right-6 sm:bottom-6 sm:w-96 sm:max-w-[calc(100vw-3rem)]">
      <div className="flex items-center justify-between border-b px-4 py-3">
        <h3 className="font-semibold">Shopping Assistant</h3>
        <Button variant="ghost" size="icon" onClick={() => setIsOpen(false)} aria-label="Close chat">
          <X className="h-4 w-4" />
        </Button>
      </div>

      <ScrollArea className="flex-1 p-4">
        <div className="space-y-4">
          {messages.length === 0 && (
            <p className="text-sm text-muted-foreground text-center py-8">
              Ask me about 3D-printed products, materials, or compatibility!
            </p>
          )}
          {messages.map((msg, i) => (
            <div key={i} className={`flex ${msg.role === 'user' ? 'justify-end' : 'justify-start'}`}>
              <div className={`rounded-lg px-3 py-2 max-w-[85%] ${msg.role === 'user' ? 'bg-primary text-primary-foreground' : 'bg-muted'}`}>
                <p className="text-sm whitespace-pre-wrap">{msg.content}</p>
                {msg.sources && msg.sources.length > 0 && (
                  <div className="mt-2 flex flex-wrap gap-1">
                    {msg.sources.slice(0, 3).map((source) => (
                      <Link key={source.product_id} href={`/products/${source.product_id}`}>
                        <Badge variant="outline" className="text-xs cursor-pointer hover:bg-accent">
                          {source.name}
                        </Badge>
                      </Link>
                    ))}
                  </div>
                )}
              </div>
            </div>
          ))}
          {chatMutation.isPending && (
            <div className="flex justify-start">
              <div className="rounded-lg bg-muted px-3 py-2">
                <p className="text-sm text-muted-foreground">Thinking...</p>
              </div>
            </div>
          )}
        </div>
      </ScrollArea>

      <div className="border-t p-4">
        <div className="flex gap-2">
          <Input
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && !e.shiftKey && handleSend()}
            placeholder="Ask about products..."
            disabled={chatMutation.isPending}
          />
          <Button size="icon" onClick={handleSend} disabled={chatMutation.isPending || !input.trim()} aria-label="Send message">
            <Send className="h-4 w-4" />
          </Button>
        </div>
      </div>
    </div>
  );
}
