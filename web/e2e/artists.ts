import { resolve } from "node:path";

function password(name: string): string {
  const value = process.env[name];
  if (!value || value.length < 10) throw new Error(`Set ${name} (10-128 characters) in the ignored root .env for the browser-test fixture accounts.`);
  return value;
}

// Stable local fixture accounts, reused across runs so the shared test database does not fill with accounts.
// Their passwords come from the ignored .env; missing values fail the run instead of skipping it.
export const ARTIST = { email: "e2e-artist@mazwi.test", get password() { return password("MAZWI_E2E_ARTIST_PASSWORD"); } };
export const OTHER_ARTIST = { email: "e2e-other-artist@mazwi.test", get password() { return password("MAZWI_E2E_OTHER_ARTIST_PASSWORD"); } };
export const AUTH_FILE = resolve("../.local/e2e-auth.json");
