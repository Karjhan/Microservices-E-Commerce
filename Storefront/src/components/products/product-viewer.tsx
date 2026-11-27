'use client';

import { Suspense } from 'react';
import { Canvas, useLoader } from '@react-three/fiber';
import { OrbitControls, Environment, Center, Bounds, useGLTF } from '@react-three/drei';
import { STLLoader } from 'three/examples/jsm/loaders/STLLoader.js';
import { Download, Box } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { ModelFormat } from '@/types/product';

function GltfModel({ url }: { url: string }) {
  const { scene } = useGLTF(url);
  return <primitive object={scene} />;
}

function StlModel({ url }: { url: string }) {
  const geometry = useLoader(STLLoader, url);
  return (
    <mesh geometry={geometry} rotation={[-Math.PI / 2, 0, 0]}>
      <meshStandardMaterial color="#9ca3af" roughness={0.6} metalness={0.1} />
    </mesh>
  );
}

/** Formats the browser viewer can render. Others are stored for download only (research R8). */
export const PREVIEWABLE_FORMATS: ModelFormat[] = ['glb', 'gltf', 'stl'];

interface ProductViewerProps {
  modelUrl: string;
  /** Defaults to GLB, which is what older callers passed. */
  format?: ModelFormat;
}

export function ProductViewer({ modelUrl, format = 'glb' }: ProductViewerProps) {
  return (
    <div className="relative aspect-square w-full rounded-lg border bg-muted overflow-hidden">
      <Canvas dpr={[1, 2]} frameloop="demand" camera={{ position: [0, 0, 5], fov: 50 }}>
        <Suspense fallback={null}>
          <ambientLight intensity={format === 'stl' ? 0.8 : 0} />
          <Bounds fit clip observe margin={1.2}>
            <Center>{format === 'stl' ? <StlModel url={modelUrl} /> : <GltfModel url={modelUrl} />}</Center>
          </Bounds>
          <Environment preset="studio" />
          <OrbitControls enablePan={false} minDistance={2} maxDistance={10} makeDefault />
        </Suspense>
      </Canvas>
      <div className="absolute bottom-2 left-2 rounded bg-background/80 px-2 py-1 text-xs text-muted-foreground">
        Drag to rotate · Scroll to zoom
      </div>
    </div>
  );
}

interface ModelInfoCardProps {
  filename: string;
  format: string;
  sizeLabel: string;
  downloadUrl?: string | null;
}

/** Shown instead of the viewer for formats that cannot be previewed (3MF, OBJ). */
export function ModelInfoCard({ filename, format, sizeLabel, downloadUrl }: ModelInfoCardProps) {
  return (
    <div className="flex items-center gap-4 rounded-lg border bg-muted/40 p-4" data-testid="model-info-card">
      <Box className="h-10 w-10 shrink-0 text-muted-foreground" aria-hidden />
      <div className="min-w-0 flex-1">
        <p className="truncate font-medium">{filename}</p>
        <p className="text-sm text-muted-foreground">
          {format.toUpperCase()} · {sizeLabel}. A preview is not available for this format.
        </p>
      </div>
      {downloadUrl && (
        <Button variant="outline" render={<a href={downloadUrl} download />}>
          <Download className="mr-2 h-4 w-4" />
          Download
        </Button>
      )}
    </div>
  );
}
