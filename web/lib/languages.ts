export const LANGUAGES = [
  { code: "sn", name: "Shona", greeting: "Mhoro" },
  { code: "xh", name: "isiXhosa", greeting: "Molo" },
  { code: "sw", name: "Kiswahili", greeting: "Habari" },
  { code: "en", name: "English", greeting: "Hello" },
] as const;
export type LanguageCode = (typeof LANGUAGES)[number]["code"];
export const LANGUAGE_KEY = "mazwi-language";
export function resolveLanguage(value: unknown): LanguageCode {
  return LANGUAGES.find((language) => language.code === value)?.code ?? "sn";
}
