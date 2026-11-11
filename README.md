# Mazwi

**Every African story, in your mother tongue.**

## Why Mazwi exists

It started with a grandmother. She sat watching TikTok videos, laughing at the faces and the music, but she couldn't understand a word being said. The stories were right there on her screen, and they were still out of reach.

Mazwi is the tool we are building so that all Africans can listen to each other's stories in their own language. An artist uploads a story, a poem, a folktale or a song. Mazwi helps turn it into other languages: the words as subtitles, and a voice the listener understands. The artist checks and approves every version before anyone hears it.

- **Any story, any language.** A story told in Shona can be watched in isiZulu, Yoruba, Kiswahili, Amharic or any of the [25 languages](docs/languages.md) Mazwi supports.
- **Story Twins.** The same tale often lives in many versions, because language kept people apart or because it changed as it travelled. Tsuro the hare in Shona is Ìjàpá the tortoise in Yoruba. Mazwi finds these twins, and the artists confirm them. A listener can then go from one telling to the other ("Also told in Yoruba by …"), and ask for it in their own language. See [Story Twins](docs/story-twins.md).
- **Artists stay in control.** Nothing is shown until its artist approves it. AI only drafts; people decide.

## What you need

- Windows with PowerShell. The helper scripts are PowerShell.
- [.NET SDK 10.0.402](https://dotnet.microsoft.com/download).
- [Node.js 24](https://nodejs.org/) or newer.
- [PostgreSQL](https://www.postgresql.org/download/) 18, running locally.
- Optional: an [OpenAI API key](https://platform.openai.com/api-keys) for AI transcripts, translations and voices. Without one, Mazwi still works: artists type their own words and upload their own recordings.

## Set it up (once)

1. **Get the code**

   ```powershell
   git clone https://github.com/Arnold-A-Mubaiwa/mazwi.git
   cd mazwi
   ```

2. **Create two empty PostgreSQL databases**: one for the app (for example `mazwi`) and one for the tests, whose name must end in `_tests` (for example `mazwi_tests`).

3. **Add your settings.** Copy `.env.example` to `.env` and fill it in. `.env` is never committed.

   ```text
   ConnectionStrings__Mazwi=Host=localhost;Database=mazwi;Username=postgres;Password=...
   MAZWI_TEST_CONNECTION_STRING=Host=localhost;Database=mazwi_tests;Username=postgres;Password=...
   OPENAI_API_KEY=            # optional
   ```

4. **Install the packages**

   ```powershell
   dotnet restore Mazwi.sln --configfile NuGet.Config
   npm.cmd install --prefix web --ignore-scripts --no-audit --no-fund
   ```

## Run it

```powershell
./scripts/Start-Mazwi.ps1
```

This builds Mazwi, updates the database and starts it. Then open **http://127.0.0.1:3000**:

- **Explore** to watch stories in your language. Pick it at the top of the page.
- **Upload** to share a story. Create an artist account first.
- **Studio** to write the words, translate, approve and see your Story Twins.

Stop it with:

```powershell
./scripts/Stop-Mazwi.ps1
```

## Check that everything works

```powershell
node .codex/skills/repo-quality/scripts/check.mjs   # build, format, all .NET tests, architecture
npm.cmd --prefix web test                           # web unit tests
npm.cmd --prefix web run test:e2e                   # browser tests (needs the MAZWI_E2E_* passwords in .env)
```

## Learn more

- How it is built and the rules for contributors: [docs/development.md](docs/development.md) and [AGENTS.md](AGENTS.md).
- What each level added: [Level 1](docs/level-1.md) (AI drafts and voices), [Level 2](docs/level-2.md) (accounts, subtitles, dubbing, sharing), [Level 3](docs/level-3.md) (ready for real artists), and Level 4 (all-Africa languages and Story Twins).
- Running on a server: [docs/hosting.md](docs/hosting.md).
