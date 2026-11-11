// Mirror of the API catalogue (GET /api/languages, src/Mazwi.Domain/StoryLanguage.cs). The browser test
// e2e/languages.spec.ts fails if the two drift apart, so the API stays the single source of truth.
export const LANGUAGES = [
  { code: "sn", name: "Shona", nativeName: "chiShona", greeting: "Mhoro", aiChecked: true },
  { code: "nd", name: "isiNdebele", nativeName: "isiNdebele", greeting: "Salibonani", aiChecked: false },
  { code: "xh", name: "isiXhosa", nativeName: "isiXhosa", greeting: "Molo", aiChecked: true },
  { code: "zu", name: "isiZulu", nativeName: "isiZulu", greeting: "Sawubona", aiChecked: false },
  { code: "st", name: "Sesotho", nativeName: "Sesotho", greeting: "Lumela", aiChecked: false },
  { code: "tn", name: "Setswana", nativeName: "Setswana", greeting: "Dumela", aiChecked: false },
  { code: "af", name: "Afrikaans", nativeName: "Afrikaans", greeting: "Hallo", aiChecked: false },
  { code: "ny", name: "Chichewa", nativeName: "Chichewa", greeting: "Moni", aiChecked: false },
  { code: "mg", name: "Malagasy", nativeName: "Malagasy", greeting: "Manao ahoana", aiChecked: false },
  { code: "sw", name: "Kiswahili", nativeName: "Kiswahili", greeting: "Habari", aiChecked: true },
  { code: "lg", name: "Luganda", nativeName: "Luganda", greeting: "Oli otya", aiChecked: false },
  { code: "rw", name: "Kinyarwanda", nativeName: "Ikinyarwanda", greeting: "Muraho", aiChecked: false },
  { code: "so", name: "Somali", nativeName: "Soomaali", greeting: "Salaan", aiChecked: false },
  { code: "am", name: "Amharic", nativeName: "አማርኛ", greeting: "ሰላም", aiChecked: false },
  { code: "om", name: "Oromo", nativeName: "Afaan Oromoo", greeting: "Akkam", aiChecked: false },
  { code: "yo", name: "Yoruba", nativeName: "Yorùbá", greeting: "Ẹ n lẹ", aiChecked: false },
  { code: "ig", name: "Igbo", nativeName: "Igbo", greeting: "Ndewo", aiChecked: false },
  { code: "ha", name: "Hausa", nativeName: "Hausa", greeting: "Sannu", aiChecked: false },
  { code: "ak", name: "Akan", nativeName: "Akan", greeting: "Akwaaba", aiChecked: false },
  { code: "wo", name: "Wolof", nativeName: "Wolof", greeting: "Na nga def", aiChecked: false },
  { code: "ln", name: "Lingala", nativeName: "Lingála", greeting: "Mbote", aiChecked: false },
  { code: "ar", name: "Arabic", nativeName: "العربية", greeting: "مرحبا", aiChecked: false },
  { code: "fr", name: "French", nativeName: "Français", greeting: "Bonjour", aiChecked: false },
  { code: "pt", name: "Portuguese", nativeName: "Português", greeting: "Olá", aiChecked: false },
  { code: "en", name: "English", nativeName: "English", greeting: "Hello", aiChecked: true },
] as const;
export type LanguageCode = (typeof LANGUAGES)[number]["code"];
export type Language = (typeof LANGUAGES)[number];
export const LANGUAGE_KEY = "mazwi-language";
export function resolveLanguage(value: unknown): LanguageCode {
  return LANGUAGES.find((language) => language.code === value)?.code ?? "sn";
}
export function findLanguage(code: unknown): Language | undefined {
  return LANGUAGES.find((language) => language.code === code);
}
/** Option label: the name Mazwi uses, plus the speakers' own name when it differs ("Yoruba · Yorùbá"). */
export function languageLabel(language: Language): string {
  return language.nativeName === language.name ? language.name : `${language.name} · ${language.nativeName}`;
}
/** Warning shown before AI drafts or voices in languages Mazwi has not checked. Empty for the pilot languages. */
export function aiQualityNotice(code: unknown): string {
  const language = findLanguage(code);
  if (!language || language.aiChecked) return "";
  return `Mazwi has not checked AI quality in ${language.name} yet. Have a ${language.name} speaker read every AI draft or voice before you approve it.`;
}
