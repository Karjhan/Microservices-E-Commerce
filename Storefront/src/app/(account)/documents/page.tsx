'use client';

import { useState } from 'react';
import dynamic from 'next/dynamic';
import { useDocuments } from '@/hooks/queries/use-documents';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/shared/empty-state';
import { ErrorFallback } from '@/components/shared/error-fallback';
import { TableSkeleton } from '@/components/shared/loading-skeletons';
import { formatDate } from '@/lib/utils';
import type { DocumentFilters } from '@/lib/api/documents';

const PdfViewer = dynamic(() => import('@/components/documents/pdf-viewer').then((m) => m.PdfViewer), { ssr: false });

export default function DocumentsPage() {
  const [orderId, setOrderId] = useState('');
  const [searchOrderId, setSearchOrderId] = useState('');
  const [selectedDocId, setSelectedDocId] = useState<string | null>(null);
  const filters: DocumentFilters = { orderId: searchOrderId || undefined };
  const { data, isLoading, isError, refetch } = useDocuments(filters);

  const handleSearch = () => {
    setSearchOrderId(orderId);
    setSelectedDocId(null);
  };

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">Documents</h1>

      <div className="flex flex-col sm:flex-row gap-2 sm:items-end">
        <div className="flex-1 space-y-2 w-full">
          <Label>Order ID</Label>
          <Input
            value={orderId}
            onChange={(e) => setOrderId(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
            placeholder="Enter order ID to find documents"
            className="w-full"
          />
        </div>
        <Button onClick={handleSearch} className="w-full sm:w-auto shrink-0">Search</Button>
      </div>

      {isError ? (
        <ErrorFallback onRetry={() => refetch()} />
      ) : isLoading ? (
        <TableSkeleton rows={3} cols={5} />
      ) : data && data.items.length > 0 ? (
        <div className="space-y-6">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Type</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Date</TableHead>
                <TableHead>Size</TableHead>
                <TableHead></TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((doc) => (
                <TableRow key={doc.id}>
                  <TableCell>{doc.documentType}</TableCell>
                  <TableCell><Badge variant="secondary">{doc.status}</Badge></TableCell>
                  <TableCell>{formatDate(doc.createdAt)}</TableCell>
                  <TableCell>{(doc.fileSizeBytes / 1024).toFixed(1)} KB</TableCell>
                  <TableCell>
                    {doc.status === 'Completed' && (
                      <Button variant="ghost" size="sm" onClick={() => setSelectedDocId(doc.id)}>
                        View
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>

          {selectedDocId && (
            <div>
              <h3 className="text-lg font-semibold mb-4">Document Preview</h3>
              <PdfViewer url={`/api/documents/${selectedDocId}/download`} />
            </div>
          )}
        </div>
      ) : searchOrderId ? (
        <EmptyState title="No documents found" description="No documents found for this order" />
      ) : (
        <EmptyState title="Search for documents" description="Enter an order ID to find related documents" />
      )}
    </div>
  );
}
