'use client';

import { Suspense } from 'react';
import { Canvas } from '@react-three/fiber';
import { OrbitControls, Environment, Center, Bounds, useGLTF } from '@react-three/drei';

function Model({ url }: { url: string }) {
  const { scene } = useGLTF(url);
  return <primitive object={scene} />;
}

interface ProductViewerProps {
  modelUrl: string;
}

export function ProductViewer({ modelUrl }: ProductViewerProps) {
  return (
    <div className="relative aspect-square w-full rounded-lg border bg-muted overflow-hidden">
      <Canvas
        dpr={[1, 2]}
        frameloop="demand"
        camera={{ position: [0, 0, 5], fov: 50 }}
      >
        <Suspense fallback={null}>
          <Bounds fit clip observe margin={1.2}>
            <Center>
              <Model url={modelUrl} />
            </Center>
          </Bounds>
          <Environment preset="studio" />
          <OrbitControls
            enablePan={false}
            minDistance={2}
            maxDistance={10}
            makeDefault
          />
        </Suspense>
      </Canvas>
      <div className="absolute bottom-2 left-2 rounded bg-background/80 px-2 py-1 text-xs text-muted-foreground">
        Drag to rotate · Scroll to zoom
      </div>
    </div>
  );
}
