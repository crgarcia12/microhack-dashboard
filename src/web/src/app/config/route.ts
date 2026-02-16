import { NextResponse } from 'next/server';

const API_URL = process.env.API_URL || 'http://localhost:5000';

export async function GET() {
  return NextResponse.json({ apiUrl: API_URL });
}
