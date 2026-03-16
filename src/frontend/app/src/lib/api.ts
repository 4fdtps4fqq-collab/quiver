const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:7000";

type RequestOptions = {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  body?: unknown;
  token?: string;
};

export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}${path}`, {
      method: options.method ?? "GET",
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

    throw new Error(message);
  }

  const text = await response.text();
  const payload = text ? safeJsonParse(text) : null;

  if (!response.ok) {
    const message =
      extractMessage(payload) ??
      (typeof payload === "string" ? payload : null) ??
      `A requisição falhou com status ${response.status}.`;

    throw new Error(message);
  }

  return payload as T;
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
