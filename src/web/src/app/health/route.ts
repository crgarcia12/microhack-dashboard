import { NextRequest, NextResponse } from 'next/server';

const API_URL = process.env.API_URL || 'http://localhost:5001';

export async function GET(req: NextRequest) {
  const url = new URL(req.url);
  const target = `${API_URL}/health${url.search}`;

  try {
    const res = await fetch(target);
    const data = await res.text();
    return new NextResponse(data, {
      status: res.status,
      headers: { 'content-type': res.headers.get('content-type') || 'application/json' },
    });
  } catch {
    return NextResponse.json({ error: 'Backend unavailable' }, { status: 502 });
  }
}
