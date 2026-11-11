"use client";
import { useSyncExternalStore } from "react";
import { LANGUAGES, LANGUAGE_KEY, languageLabel, resolveLanguage, type LanguageCode } from "@/lib/languages";
const changeEvent = "mazwi-language-change";
let sessionLanguage: LanguageCode = "sn";
function subscribe(listener: () => void) {
  window.addEventListener("storage", listener);
  window.addEventListener(changeEvent, listener);
  return () => {
    window.removeEventListener("storage", listener);
    window.removeEventListener(changeEvent, listener);
  };
}
function getSnapshot() {
  try { return resolveLanguage(window.localStorage.getItem(LANGUAGE_KEY) ?? sessionLanguage); }
  catch { return sessionLanguage; }
}
function setLanguage(value: string) {
  sessionLanguage = resolveLanguage(value);
  try { window.localStorage.setItem(LANGUAGE_KEY, sessionLanguage); } catch { /* Keep a session-only preference. */ }
  window.dispatchEvent(new Event(changeEvent));
}
export function usePreferredLanguage(): LanguageCode {
  return useSyncExternalStore(subscribe, getSnapshot, () => "sn" as const);
}
export function LanguagePicker() {
  const code = usePreferredLanguage();
  const selected = LANGUAGES.find((language) => language.code === code)!;
  return <div className="language-control">
    <div className="language-label"><span aria-hidden="true">◎</span><label htmlFor="language">Your language</label></div>
    <div className="select-wrap"><select id="language" value={code} onChange={(event) => setLanguage(event.target.value)}>
      {LANGUAGES.map((language) => <option key={language.code} value={language.code}>{languageLabel(language)}</option>)}
    </select><span aria-hidden="true">⌄</span></div>
    <p className="language-note" aria-live="polite">{selected.greeting}. Make yourself at home.</p>
  </div>;
}
