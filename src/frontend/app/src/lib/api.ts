const apiBaseUrl = resolveApiBaseUrl();

type RequestOptions = {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  body?: unknown;
  token?: string;
};

export class ApiRequestError extends Error {
  status?: number;
  method: string;
  path: string;
  requestUrl: string;
  responseText?: string;
  payload?: unknown;
  occurredAtUtc: string;

  constructor(
    message: string,
    options: {
      status?: number;
      method: string;
      path: string;
      requestUrl: string;
      responseText?: string;
      payload?: unknown;
    }
  ) {
    super(message);
    this.name = "ApiRequestError";
    this.status = options.status;
    this.method = options.method;
    this.path = options.path;
    this.requestUrl = options.requestUrl;
    this.responseText = options.responseText;
    this.payload = options.payload;
    this.occurredAtUtc = new Date().toISOString();
  }
}

export function getApiBaseUrl() {
  return apiBaseUrl;
}

export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const method = options.method ?? "GET";
  const requestUrl = `${apiBaseUrl}${path}`;
  let response: Response;
  try {
    response = await fetch(requestUrl, {
      method,
      headers: {
        "Content-Type": "application/json",
        ...(options.token ? { Authorization: `Bearer ${options.token}` } : {})
      },
      body: options.body ? JSON.stringify(options.body) : undefined
    });
  } catch (cause) {
    const message =
      cause instanceof Error && cause.message.toLowerCase().includes("failed to fetch")
        ? "Não foi possível concluir a comunicação com a plataforma agora. Tente novamente em instantes."
        : "Não foi possível concluir a comunicação com a plataforma.";

    throw new ApiRequestError(message, {
      method,
      path,
      requestUrl
    });
  }

  const text = await response.text();
  const payload = text ? safeJsonParse(text) : null;

  if (!response.ok) {
    const message =
      extractMessage(payload) ??
      (typeof payload === "string" ? payload : null) ??
      `A requisição falhou com status ${response.status}.`;

    throw new ApiRequestError(message, {
      status: response.status,
      method,
      path,
      requestUrl,
      responseText: text,
      payload
    });
  }

  return payload as T;
}

export async function apiDownload(path: string, token: string): Promise<Blob> {
  const method = "GET";
  const requestUrl = `${apiBaseUrl}${path}`;
  let response: Response;
  try {
    response = await fetch(requestUrl, {
      method,
      headers: {
        Authorization: `Bearer ${token}`
      }
    });
  } catch (cause) {
    const message =
      cause instanceof Error && cause.message.toLowerCase().includes("failed to fetch")
        ? "Não foi possível concluir a comunicação com a plataforma agora. Tente novamente em instantes."
        : "Não foi possível concluir a comunicação com a plataforma.";

    throw new ApiRequestError(message, {
      method,
      path,
      requestUrl
    });
  }

  if (!response.ok) {
    const text = await response.text();
    const payload = text ? safeJsonParse(text) : null;
    const message =
      extractMessage(payload) ??
      (typeof payload === "string" ? payload : null) ??
      `A requisição falhou com status ${response.status}.`;

    throw new ApiRequestError(message, {
      status: response.status,
      method,
      path,
      requestUrl,
      responseText: text,
      payload
    });
  }

  return response.blob();
}

function safeJsonParse(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}

function extractMessage(payload: unknown): string | null {
  if (!payload || typeof payload !== "object") {
    return null;
  }

  const record = payload as Record<string, unknown>;
  if (typeof record.message === "string") {
    return record.message;
  }

  if (typeof record.detail === "string") {
    return record.detail;
  }

  if (typeof record.body === "string" && record.body.length > 0) {
    return record.body;
  }

  return null;
}

function resolveApiBaseUrl() {
  const configured = import.meta.env.VITE_API_BASE_URL?.trim();
  if (configured) {
    return configured;
  }

  if (import.meta.env.DEV) {
    return "http://localhost:7000";
  }

  if (typeof window !== "undefined") {
    return window.location.origin;
  }

  return "http://localhost:7000";
}
