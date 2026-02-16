import { NextRequest, NextResponse } from 'next/server';

const API_URL = process.env.API_URL || 'http://localhost:5001';

async function proxyRequest(req: NextRequest) {
  const url = new URL(req.url);
  const target = `${API_URL}${url.pathname}${url.search}`;

  const headers = new Headers(req.headers);
  headers.delete('host');

  try {
    const res = await fetch(target, {
      method: req.method,
      headers,
      body: req.body,
      // @ts-expect-error duplex is required for streaming bodies
      duplex: 'half',
      cache: 'no-store',
    });

    const responseHeaders = new Headers(res.headers);
    responseHeaders.delete('transfer-encoding');

    // Preserve Set-Cookie headers (Headers API may merge them)
    const setCookies = res.headers.getSetCookie?.() ?? [];

    const response = new NextResponse(res.body, {
      status: res.status,
      statusText: res.statusText,
      headers: responseHeaders,
    });

    // Re-apply individual Set-Cookie headers
    for (const cookie of setCookies) {
      response.headers.append('set-cookie', cookie);
    }

    return response;
  } catch {
    return NextResponse.json(
      { error: 'Backend unavailable' },
      { status: 502 }
    );
  }
}

export const GET = proxyRequest;
export const POST = proxyRequest;
export const PUT = proxyRequest;
export const DELETE = proxyRequest;
export const PATCH = proxyRequest;
