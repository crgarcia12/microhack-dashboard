const API_BASE = '';

interface ApiOptions {
  body?: unknown;
}

async function request<T>(method: string, path: string, options?: ApiOptions): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    method,
    headers: {
      'Content-Type': 'application/json',
    },
    credentials: 'include',
    body: options?.body ? JSON.stringify(options.body) : undefined,
  });

  if (!res.ok) {
    const data = await res.json().catch(() => ({ error: 'Something went wrong. Please try again.' }));
    throw new ApiError(res.status, data.error || 'Something went wrong. Please try again.');
  }

  return res.json() as Promise<T>;
}

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
    this.name = 'ApiError';
  }
}

export const api = {
  get: <T>(path: string) => request<T>('GET', path),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, { body }),
  delete: <T>(path: string) => request<T>('DELETE', path),
};
