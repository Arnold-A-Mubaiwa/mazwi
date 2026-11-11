import test from "node:test";
import assert from "node:assert/strict";
import { LANGUAGES, aiQualityNotice, findLanguage, languageLabel, resolveLanguage } from "../lib/languages.ts";

test("LANGUAGES_Catalogue_HasTwentyFiveUniqueTwoLetterCodes", () => {
  assert.equal(LANGUAGES.length, 25);
  assert.equal(new Set(LANGUAGES.map(language => language.code)).size, 25);
  for (const language of LANGUAGES) {
    assert.match(language.code, /^[a-z]{2}$/);
    assert.ok(language.name && language.nativeName && language.greeting);
  }
  assert.deepEqual(LANGUAGES.filter(language => language.aiChecked).map(language => language.code), ["sn", "xh", "sw", "en"]);
});

test("resolveLanguage_NewCatalogueCodes_AreKeptAndUnknownFallsBackToShona", () => {
  for (const code of ["yo", "am", "nd", "ar"]) assert.equal(resolveLanguage(code), code);
  for (const value of ["de", "YO", " yo", "eng"]) assert.equal(resolveLanguage(value), "sn");
});

test("languageLabel_NativeNameDiffers_ShowsBoth", () => {
  assert.equal(languageLabel(findLanguage("yo")), "Yoruba · Yorùbá");
  assert.equal(languageLabel(findLanguage("am")), "Amharic · አማርኛ");
  assert.equal(languageLabel(findLanguage("xh")), "isiXhosa");
  assert.equal(findLanguage("de"), undefined);
});

test("aiQualityNotice_UncheckedLanguage_AsksForASpeakerAndPilotLanguagesHaveNone", () => {
  assert.equal(aiQualityNotice("ha"), "Mazwi has not checked AI quality in Hausa yet. Have a Hausa speaker read every AI draft or voice before you approve it.");
  for (const code of ["sn", "xh", "sw", "en", "de", null]) assert.equal(aiQualityNotice(code), "");
});
