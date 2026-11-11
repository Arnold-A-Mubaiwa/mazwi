# Languages

Mazwi supports 25 languages: 21 African languages and four languages shared across Africa. Artists can upload, translate and approve in any of them, and viewers can choose any of them.

| Region | Languages (code) |
| --- | --- |
| Southern Africa | Shona (sn), isiNdebele (nd), isiXhosa (xh), isiZulu (zu), Sesotho (st), Setswana (tn), Afrikaans (af), Chichewa (ny), Malagasy (mg) |
| East Africa and the Horn | Kiswahili (sw), Luganda (lg), Kinyarwanda (rw), Somali (so), Amharic (am), Oromo (om) |
| West and Central Africa | Yoruba (yo), Igbo (ig), Hausa (ha), Akan (ak), Wolof (wo), Lingala (ln) |
| Shared across Africa | Arabic (ar), French (fr), Portuguese (pt), English (en) |

## One catalogue

- The list lives in one place: `src/Mazwi.Domain/StoryLanguage.cs` (`StoryLanguages.All`). Each language has a code, the name Mazwi uses in sentences, the name its speakers use, an English name for AI instructions, a greeting, and whether Mazwi has checked AI quality in it.
- `GET /api/languages` returns the catalogue (signed out, cached for an hour).
- The web keeps a mirror in `web/lib/languages.ts` so pages render without waiting. The browser test `e2e/languages.spec.ts` fails if the mirror and the API differ.
- The database only checks that a language code is two lowercase letters (migration 013); the Domain decides which codes are allowed.

## Adding a language

1. Add it to `StoryLanguages.All` and to `web/lib/languages.ts` with `aiChecked: false`.
2. Update the counts in the catalogue tests (`StoryLanguageTests`, `languages.test.mjs`, `LanguagesEndpointTests`, `languages.spec.ts`).
3. No migration is needed.

## AI quality

Only Shona, isiXhosa, Kiswahili and English were used while building Mazwi's AI drafts and voices. For every other language, the story editor shows **"AI quality untested"** and asks for a speaker to read every AI draft or voice before approval. Nothing is blocked: artists can still write and approve their own words in any language.

A language moves to `AiChecked` only after speakers have reviewed real AI drafts and voices in it.
