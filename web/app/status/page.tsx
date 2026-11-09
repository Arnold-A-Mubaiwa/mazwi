"use client";
import { useEffect, useState } from "react";
import { getApiHealth } from "@/lib/api";
export default function Status() {
  const [state, setState] = useState<"checking" | "online" | "offline">("checking");
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    getApiHealth(controller.signal).then(() => setState("online")).catch(() => { if (!controller.signal.aborted) setState("offline"); });
    return () => controller.abort();
  }, [attempt]);
  return <main id="main" className="status-page">
    <p className="eyebrow">LOCAL DEVELOPMENT</p><h1>Connection check.</h1>
    <p role="status">{state === "checking" ? "Checking the API…" : state === "online" ? "Mazwi API is reachable." : "Mazwi API is not reachable."}</p>
    <p>This checks API liveness only. It does not verify database access or audio processing.</p>
    <button className="retry-button" disabled={state === "checking"} onClick={() => { setState("checking"); setAttempt((value) => value + 1); }}>Check again</button>
  </main>;
}
