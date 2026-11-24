import { NextRequest, NextResponse } from 'next/server';
import { auth } from '@/lib/auth';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_GATEWAY_URL || 'http://localhost:8080';

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ documentId: string }> }
) {
  const session = await auth();
  if (!session) {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const { documentId } = await params;
  const accessToken = (session as any).accessToken;

  try {
    const metaRes = await fetch(`${API_BASE_URL}/documents/${documentId}`, {
      headers: { Authorization: `Bearer ${accessToken}` },
    });

    if (!metaRes.ok) {
      return NextResponse.json({ error: 'Document not found' }, { status: metaRes.status });
    }

    const meta = await metaRes.json();
    if (!meta.storageObjectKey || !meta.storageBucket) {
      return NextResponse.json({ error: 'Document not ready' }, { status: 404 });
    }

    const minioUrl = `http://localhost:9000/${meta.storageBucket}/${meta.storageObjectKey}`;
    const pdfRes = await fetch(minioUrl);

    if (!pdfRes.ok) {
      return NextResponse.json({ error: 'Failed to fetch PDF' }, { status: 502 });
    }

    const pdfBuffer = await pdfRes.arrayBuffer();
    return new NextResponse(pdfBuffer, {
      headers: {
        'Content-Type': 'application/pdf',
        'Content-Disposition': `inline; filename="${documentId}.pdf"`,
      },
    });
  } catch {
    return NextResponse.json({ error: 'Internal error' }, { status: 500 });
  }
}
