export function formatCurrency(value: number | null | undefined) {
  return new Intl.NumberFormat("pt-BR", {
    style: "currency",
    currency: "BRL",
    maximumFractionDigits: 2
  }).format(value ?? 0);
}

export function formatDateTime(value: string | null | undefined) {
  if (!value) {
    return "-";
  }

  return new Intl.DateTimeFormat("pt-BR", {
    dateStyle: "short",
    timeStyle: "short"
  }).format(new Date(value));
}

export function formatDate(value: string | null | undefined) {
  if (!value) {
    return "-";
  }

  return new Intl.DateTimeFormat("pt-BR", {
    dateStyle: "short"
  }).format(new Date(value));
}

export function formatMinutes(value: number | null | undefined) {
  const total = value ?? 0;
  const sign = total < 0 ? "-" : "";
  const absolute = Math.abs(total);
  const hours = Math.floor(absolute / 60);
  const minutes = absolute % 60;
  return `${sign}${hours}h ${minutes.toString().padStart(2, "0")}m`;
}

export function formatRelativeDistance(value: string | null | undefined) {
  if (!value) {
    return "-";
  }

  const target = new Date(value).getTime();
  const diffMinutes = Math.round((target - Date.now()) / 60_000);
  const absolute = Math.abs(diffMinutes);

  if (absolute < 60) {
    return diffMinutes >= 0 ? `em ${absolute} min` : `${absolute} min atras`;
  }

  const hours = Math.round(absolute / 60);
  if (hours < 24) {
    return diffMinutes >= 0 ? `em ${hours} h` : `${hours} h atras`;
  }

  const days = Math.round(hours / 24);
  return diffMinutes >= 0 ? `em ${days} d` : `${days} d atras`;
}

export function toLocalDateTimeInput(value: string | null | undefined) {
  if (!value) {
    return "";
  }

  const date = new Date(value);
  const timezoneOffset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - timezoneOffset).toISOString().slice(0, 16);
}

export function fromLocalDateTimeInput(value: string) {
  return value ? new Date(value).toISOString() : null;
}
