import { defineConfig } from "@playwright/test";
import { resolve } from "node:path";
const authFile = resolve("../.local/e2e-auth.json");
export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false, timeout: 60000, expect: { timeout: 10000 },
  workers: 1,
  reporter: "list",
  use: { baseURL: "http://127.0.0.1:3000", channel: "msedge", headless: true, screenshot: "only-on-failure" },
  projects: [
    { name: "setup", testMatch: /auth\.setup\.ts/ },
    // Every spec starts signed in as the stable browser fixture artist.
    { name: "browser", dependencies: ["setup"], use: { storageState: authFile } },
  ],
  webServer: [
    {
      command: "dotnet run --project ../src/Mazwi.WebApi --no-launch-profile --no-build --urls http://127.0.0.1:5046",
      url: "http://127.0.0.1:5046/api/health", reuseExistingServer: false, timeout: 120000,
      env: {
        DOTNET_CLI_HOME: resolve("../.local/dotnet"), NUGET_PACKAGES: resolve("../.local/nuget/packages"), DOTNET_CLI_TELEMETRY_OPTOUT: "1",
        ConnectionStrings__Mazwi: process.env.MAZWI_TEST_CONNECTION_STRING ?? "",
        MediaStorage__Root: resolve("../.local/browser-media"),
        Mazwi__AccountRequestsPerMinute: "1000",
      },
    },
    {
      command: "npm.cmd run start -- --port 3000", url: "http://127.0.0.1:3000", reuseExistingServer: false, timeout: 120000,
      env: { NEXT_TELEMETRY_DISABLED: "1", MAZWI_API_URL: "http://127.0.0.1:5046" },
    },
  ],
});
