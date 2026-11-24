import { Box } from 'lucide-react';

export function ProductViewerSkeleton() {
  return (
    <div className="relative aspect-square w-full rounded-lg border bg-muted flex items-center justify-center">
      <div className="text-center space-y-2">
        <Box className="h-12 w-12 text-muted-foreground mx-auto animate-pulse" />
        <p className="text-sm text-muted-foreground">Loading 3D viewer...</p>
      </div>
    </div>
  );
}
