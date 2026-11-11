// A tiny stand-in for OpenAI used only by the browser tests (the API honours it only in the Testing environment,
// on a loopback address). The "gist" echoes the story's words; the "embedding" hashes its words, so stories that
// share most words are similar. Nothing here is Mazwi code, so Story Twins run for real against it.
import { createServer } from "node:http";

const PORT = Number(process.env.FAKE_OPENAI_PORT ?? 5099);
const DIMENSIONS = 256;

function embed(text) {
  const vector = new Array(DIMENSIONS).fill(0);
  for (const word of text.toLocaleLowerCase().match(/[\p{L}\p{N}]{3,}/gu) ?? []) {
    let hash = 2166136261;
    for (const char of word) hash = Math.imul(hash ^ char.codePointAt(0), 16777619) >>> 0;
    vector[hash % DIMENSIONS] += 1;
  }
  if (vector.every(value => value === 0)) vector[0] = 1;
  return vector;
}

createServer(async (request, response) => {
  const send = (status, value) => { response.writeHead(status, { "Content-Type": "application/json" }); response.end(JSON.stringify(value)); };
  if (request.method === "GET") return send(200, { status: "fake openai" });
  if (request.headers.authorization !== "Bearer fake-key") return send(401, { error: "bad key" });
  let body = "";
  for await (const chunk of request) body += chunk;
  const json = JSON.parse(body || "{}");
  if (request.url === "/v1/responses") {
    const passage = JSON.parse(json.input ?? "{}").passage ?? "";
    return send(200, { status: "completed", output: [{ type: "message", content: [{ type: "output_text", text: passage.slice(0, 1900) }] }] });
  }
  if (request.url === "/v1/embeddings") return send(200, { data: [{ embedding: embed(String(json.input ?? "")) }] });
  return send(404, { error: "not faked" });
}).listen(PORT, "127.0.0.1");
