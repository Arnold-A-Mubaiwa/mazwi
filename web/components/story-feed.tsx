"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { getFeed, getViewerStory, languageName, playbackLabel, viewingNote, type FeedPage, type ViewerStory } from "@/lib/feed";
import { usePreferredLanguage } from "./language-picker";
import { RecordingPlayer } from "./recording-player";

function StoryCard({ story, preferred }: { story: ViewerStory; preferred: ReturnType<typeof usePreferredLanguage> }) {
  const [original, setOriginal] = useState(false);
  const mediaUrl = original
    ? `/api/feed/${story.id}/recording?language=${story.textLanguage}&version=${story.reviewVersion}&originalAudio=true`
    : story.mediaUrl;
  return <>
    <div className="viewer-heading"><p className="eyebrow">{story.type.toUpperCase()} / ARTIST APPROVED</p><span>{languageName(story.textLanguage)}</span></div>
    <h3><Link href={`/story/${story.id}`}>{story.title}</Link></h3>
    <p className="viewer-credit">{story.artistName} · {story.community} · {story.country}</p>
    <p className="viewer-note">{viewingNote(story, preferred)}</p>
    <RecordingPlayer key={mediaUrl} src={mediaUrl} extension={original ? (story.originalRecordingExtension ?? ".mp4") : story.recordingExtension} label={original ? `Original ${languageName(story.originalLanguage)} audio` : playbackLabel(story)}
      cues={original ? null : story.cues} subtitlesUrl={original ? null : story.subtitlesUrl} subtitleLanguage={story.textLanguage} />
    {story.audioLanguage !== story.originalLanguage && <button className="text-control" onClick={() => setOriginal(value => !value)}>{original ? "Hear translated recording" : "Hear original"}</button>}
    <div className="viewer-text" lang={story.textLanguage}><p className="eyebrow">APPROVED WORDS / {languageName(story.textLanguage).toUpperCase()}</p><p>{story.text}</p></div>
    <p className="viewer-languages">Approved in: {story.availableLanguages.map(languageName).join(" · ")}</p>
  </>;
}

export function StoryFeed({ storyId }: { storyId?: string }) {
  const language = usePreferredLanguage();
  const [state, setState] = useState<{ key: string; page?: FeedPage; error?: string } | null>(null);
  const [index, setIndex] = useState(0);
  const [retry, setRetry] = useState(0);
  const [moreBusy, setMoreBusy] = useState(false);
  const touch = useRef<{ x: number; y: number } | null>(null);
  const key = `${storyId ?? "feed"}:${language}:${retry}`;
  const generation = useRef(key);
  useEffect(() => {
    generation.current = key;
    const controller = new AbortController();
    const load = storyId ? getViewerStory(storyId, language, controller.signal).then(story => ({ items: [story], nextOffset: null })) : getFeed(language, 0, controller.signal);
    load.then(page => { setState({ key, page }); setIndex(0); }).catch(failure => {
      if (!controller.signal.aborted) setState({ key, error: failure instanceof Error ? failure.message : "Could not load stories." });
    });
    return () => controller.abort();
  }, [key, language, retry, storyId]);

  async function next() {
    const page = state?.page;
    if (!page || moreBusy) return;
    if (index + 1 < page.items.length) { setIndex(value => value + 1); return; }
    if (page.nextOffset === null) return;
    setMoreBusy(true);
    try {
      const more = await getFeed(language, page.nextOffset);
      if (generation.current !== key) return;
      setState({ key, page: { items: [...page.items, ...more.items], nextOffset: more.nextOffset } });
      if (more.items.length) setIndex(page.items.length);
    } catch (failure) { setState({ key, error: failure instanceof Error ? failure.message : "Could not load more stories." }); }
    finally { setMoreBusy(false); }
  }

  if (state?.key !== key) return <div className="empty-stories"><p role="status">Loading approved stories…</p></div>;
  if (state.error) return <div className="empty-stories"><p role="alert">{state.error}</p><button className="retry-button" onClick={() => setRetry(value => value + 1)}>Try stories again</button></div>;
  const page = state.page!;
  const story = page.items[index];
  if (!story) return <div className="empty-stories"><div className="empty-mark" aria-hidden="true"><span /><span /><span /><span /><span /></div>
    <p className="eyebrow">GOOD STORIES DESERVE A MOMENT</p><h3>The first voices<br />are on their way.</h3>
    <p>Every version here needs its artist’s approval.<br />Choose another language or come back soon.</p>
    {page.nextOffset !== null && <button className="retry-button" onClick={() => void next()} disabled={moreBusy}>Load more stories</button>}
  </div>;

  return <div className="viewer-feed">
    <article className="viewer-card" tabIndex={0} aria-label="Story player"
      onKeyDown={event => { if (event.target !== event.currentTarget) return; if (event.key === "ArrowDown" || event.key === "ArrowRight") { event.preventDefault(); void next(); } if (event.key === "ArrowUp" || event.key === "ArrowLeft") { event.preventDefault(); setIndex(value => Math.max(0, value - 1)); } }}
      onTouchStart={event => { touch.current = { x: event.touches[0].clientX, y: event.touches[0].clientY }; }}
      onTouchEnd={event => { const start = touch.current; touch.current = null; if (!start) return; const y = start.y - event.changedTouches[0].clientY; const x = start.x - event.changedTouches[0].clientX; if (Math.abs(y) < 80 || Math.abs(y) < Math.abs(x) * 1.5) return; if (y > 0) void next(); else setIndex(value => Math.max(0, value - 1)); }}>
      <StoryCard key={`${story.id}-${story.reviewVersion}-${language}`} story={story} preferred={language} />
    </article>
    {!storyId && <nav className="viewer-navigation" aria-label="Story navigation"><button onClick={() => setIndex(value => Math.max(0, value - 1))} disabled={index === 0}>Previous story</button><span>{index + 1} / {page.items.length}{page.nextOffset !== null ? "+" : ""}</span><button onClick={() => void next()} disabled={moreBusy || (index === page.items.length - 1 && page.nextOffset === null)}>Next story</button></nav>}
    <button className="text-control" onClick={() => setRetry(value => value + 1)}>Refresh stories</button>
  </div>;
}
