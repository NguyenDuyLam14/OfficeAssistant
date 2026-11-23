export function parseUtc(value: string): Date {
  const hasTimeZone = /([zZ]|[+-]\d{2}:\d{2})$/.test(value);
  return new Date(hasTimeZone ? value : value + "Z");
}

// UTC từ backend -> giá trị cho <input type="datetime-local">
export function toLocalInput(value: string | null): string {
  if (!value) return "";
  const date = parseUtc(value);
  if (Number.isNaN(date.getTime())) return "";
  const p = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${p(date.getMonth() + 1)}-${p(date.getDate())}T${p(date.getHours())}:${p(date.getMinutes())}`;
}

export function toIso(local: string): string | null {
  return local ? new Date(local).toISOString() : null;
}