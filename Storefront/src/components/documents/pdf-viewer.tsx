'use client';

import { useEffect, useRef, useState } from 'react';
import { Button } from '@/components/ui/button';
import { ChevronLeft, ChevronRight, ZoomIn, ZoomOut } from 'lucide-react';

interface PdfViewerProps {
  url: string;
}

export function PdfViewer({ url }: PdfViewerProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [numPages, setNumPages] = useState(0);
  const [currentPage, setCurrentPage] = useState(1);
  const [scale, setScale] = useState(1.5);
  const [error, setError] = useState<string | null>(null);
  const pdfDocRef = useRef<any>(null);

  useEffect(() => {
    let cancelled = false;

    async function loadPdf() {
      try {
        const pdfjs = await import('pdfjs-dist');
        pdfjs.GlobalWorkerOptions.workerSrc = `https://cdnjs.cloudflare.com/ajax/libs/pdf.js/${pdfjs.version}/pdf.worker.min.mjs`;

        const doc = await pdfjs.getDocument({ url }).promise;
        if (cancelled) return;
        pdfDocRef.current = doc;
        setNumPages(doc.numPages);
        setCurrentPage(1);
      } catch (err) {
        if (!cancelled) setError('Failed to load PDF document');
      }
    }

    loadPdf();
    return () => { cancelled = true; };
  }, [url]);

  useEffect(() => {
    async function renderPage() {
      if (!pdfDocRef.current || !canvasRef.current) return;
      try {
        const page = await pdfDocRef.current.getPage(currentPage);
        const viewport = page.getViewport({ scale });
        const canvas = canvasRef.current;
        canvas.height = viewport.height;
        canvas.width = viewport.width;
        const ctx = canvas.getContext('2d');
        if (!ctx) return;
        await page.render({ canvasContext: ctx, viewport }).promise;
      } catch {
        setError('Failed to render page');
      }
    }

    renderPage();
  }, [currentPage, scale]);

  if (error) {
    return (
      <div className="flex items-center justify-center h-96 border rounded-lg bg-muted">
        <p className="text-sm text-destructive">{error}</p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Button variant="outline" size="icon" disabled={currentPage <= 1} onClick={() => setCurrentPage((p) => p - 1)}>
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <span className="text-sm">
            Page {currentPage} of {numPages}
          </span>
          <Button variant="outline" size="icon" disabled={currentPage >= numPages} onClick={() => setCurrentPage((p) => p + 1)}>
            <ChevronRight className="h-4 w-4" />
          </Button>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="icon" onClick={() => setScale((s) => Math.max(0.5, s - 0.25))}>
            <ZoomOut className="h-4 w-4" />
          </Button>
          <span className="text-sm">{Math.round(scale * 100)}%</span>
          <Button variant="outline" size="icon" onClick={() => setScale((s) => Math.min(3, s + 0.25))}>
            <ZoomIn className="h-4 w-4" />
          </Button>
        </div>
      </div>
      <div className="overflow-auto border rounded-lg bg-muted p-4">
        <canvas ref={canvasRef} className="mx-auto" />
      </div>
    </div>
  );
}
