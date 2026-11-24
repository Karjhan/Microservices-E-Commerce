'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useSearch } from '@/hooks/queries/use-search';
import { SearchFilterPanel } from '@/components/search/search-filters';
import { EmptyState } from '@/components/shared/empty-state';
import { ErrorFallback } from '@/components/shared/error-fallback';
import { ListSkeleton } from '@/components/shared/loading-skeletons';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Search } from 'lucide-react';
import type { SearchFilters } from '@/types/search';

export default function SearchPage() {
  const [query, setQuery] = useState('');
  const [searchQuery, setSearchQuery] = useState('');
  const [filters, setFilters] = useState<SearchFilters>({});
  const { data, isLoading, isError, refetch } = useSearch(searchQuery, filters);

  const handleSearch = () => {
    setSearchQuery(query);
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl sm:text-3xl font-bold">Search</h1>
        <p className="text-muted-foreground mt-1">Find products using natural language or filters</p>
      </div>

      <div className="flex flex-col sm:flex-row gap-2">
        <Input
          placeholder="Search for 3D prints..."
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
          className="w-full sm:max-w-lg sm:flex-1"
        />
        <Button onClick={handleSearch} className="w-full sm:w-auto shrink-0">
          <Search className="mr-2 h-4 w-4" />
          Search
        </Button>
      </div>

      <div className="grid gap-8 md:grid-cols-[250px_1fr]">
        <aside>
          <SearchFilterPanel
            filters={filters}
            onChange={(f) => { setFilters(f); if (searchQuery) setSearchQuery(searchQuery); }}
            onClear={() => setFilters({})}
          />
        </aside>

        <div>
          {isError ? (
            <ErrorFallback onRetry={() => refetch()} />
          ) : isLoading ? (
            <ListSkeleton rows={5} />
          ) : data && data.results.length > 0 ? (
            <div className="space-y-4">
              <p className="text-sm text-muted-foreground">
                {data.total_candidates} candidates · Intent: {data.intent}
              </p>
              {data.results.map((result) => (
                <Link key={result.product_id} href={`/products/${result.product_id}`}>
                  <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between rounded-lg border p-4 hover:bg-accent transition-colors">
                    <div className="min-w-0">
                      <p className="font-medium">{result.name}</p>
                      <p className="text-sm text-muted-foreground">
                        Score: {result.score.toFixed(2)}
                      </p>
                    </div>
                    <Badge variant="outline">
                      {(result.score * 100).toFixed(0)}%
                    </Badge>
                  </div>
                </Link>
              ))}
            </div>
          ) : searchQuery ? (
            <EmptyState
              title="No results found"
              description="Try broadening your search or adjusting filters"
            />
          ) : (
            <EmptyState
              title="Start searching"
              description="Enter a query to find 3D-printed products"
            />
          )}
        </div>
      </div>
    </div>
  );
}
