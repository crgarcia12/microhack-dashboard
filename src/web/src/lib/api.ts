const API_BASE = '';

interface ApiOptions {
  body?: unknown;
}

async function throwApiError(res: Response): Promise<never> {
  const data = await res.json().catch(() => ({ error: 'Something went wrong. Please try again.' }));
  throw new ApiError(res.status, data.error || 'Something went wrong. Please try again.');
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
    await throwApiError(res);
  }

  return res.json() as Promise<T>;
}

async function uploadFile(path: string, file: File): Promise<void> {
  const formData = new FormData();
  formData.append('file', file);

  const res = await fetch(`${API_BASE}${path}`, {
    method: 'POST',
    credentials: 'include',
    body: formData,
  });

  if (!res.ok) {
    await throwApiError(res);
  }
}

async function downloadFile(path: string): Promise<Blob> {
  const res = await fetch(`${API_BASE}${path}`, {
    method: 'GET',
    credentials: 'include',
  });

  if (!res.ok) {
    await throwApiError(res);
  }

  return res.blob();
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
  put: <T>(path: string, body?: unknown) => request<T>('PUT', path, { body }),
  delete: <T>(path: string) => request<T>('DELETE', path),
  uploadCsv: (path: string, file: File) => uploadFile(path, file),
  downloadCsv: (path: string) => downloadFile(path),
};
