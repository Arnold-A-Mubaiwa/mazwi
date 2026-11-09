export type ApiHealth = { status: "ok"; service: "Mazwi" };
export async function getApiHealth(signal?: AbortSignal): Promise<ApiHealth> {
  const response = await fetch("/api/health", { cache: "no-store", signal });
  if (!response.ok) throw new Error("The API is unavailable.");
  const value: unknown = await response.json();
  if (!value || typeof value !== "object" || !("status" in value) || !("service" in value) || value.status !== "ok" || value.service !== "Mazwi") throw new Error("The API returned an unexpected response.");
  return { status: "ok", service: "Mazwi" };
}
