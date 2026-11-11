import { resolve } from "node:path";
// Stable local fixture accounts, reused across runs so the shared test database does not fill with accounts.
// They exist only in the local _tests database and are not secrets.
export const ARTIST = { email: "e2e-artist@mazwi.test", password: "browser fixture password" };
export const OTHER_ARTIST = { email: "e2e-other-artist@mazwi.test", password: "other browser fixture password" };
export const AUTH_FILE = resolve("../.local/e2e-auth.json");
