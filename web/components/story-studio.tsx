"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { getStudio, type StudioPage } from "@/lib/studio";
import { languageName } from "@/lib/feed";

export function StoryStudio() {
  const [offset, setOffset] = useState(0);
  const [retry, setRetry] = useState(0);
  const [filter, setFilter] = useState("");
  const [state, setState] = useState<{ offset: number; page?: StudioPage; error?: string } | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    getStudio(offset, controller.signal).then(page => setState({ offset, page })).catch(failure => {
      if (!controller.signal.aborted) setState({ offset, error: failure instanceof Error ? failure.message : "Could not load stories." });
    });
    return () => controller.abort();
  }, [offset, retry]);
  if (state?.offset !== offset) return <p role="status">Loading your local stories…</p>;
  if (state.error) return <section><p role="alert">{state.error}</p><button className="retry-button" onClick={() => setRetry(value => value + 1)}>Try studio again</button></section>;
  const page = state.page!;
  const stories = page.items.filter(story => `${story.title} ${story.artistName} ${story.community}`.toLocaleLowerCase().includes(filter.toLocaleLowerCase()));
  return <>
    <div className="studio-toolbar"><label htmlFor="studio-filter">Filter this page<input id="studio-filter" value={filter} onChange={event => setFilter(event.target.value)} placeholder="Title, artist or community" /></label><Link className="studio-upload" href="/upload">Upload a story ↗</Link></div>
    {!page.items.length && <div className="empty-stories"><h2>Your first story starts here.</h2><p>Upload a recording, add its words and review each language before sharing.</p></div>}
    {!!page.items.length && !stories.length && <p>No matching stories on this page. Clear the filter or try the next page.</p>}
    <div className="studio-grid">{stories.map(story => <article className="studio-card" key={story.id}>
      <span className="draft-badge">{story.status === "published" ? "Original approved" : "Unpublished"}</span>
      <h2>{story.title}</h2><p>{story.artistName} · {story.community}</p><p>Original language: {languageName(story.language)}</p>
      <Link className="text-link" href={`/story/${story.id}/review`}>Edit and approve ↗</Link>
      {story.status === "published" ? <p><Link className="text-link" href={`/story/${story.id}`}>Watch story ↗</Link></p> : <p className="studio-private">Not in the viewer feed.</p>}
    </article>)}</div>
    <nav className="viewer-navigation" aria-label="Studio pages"><button disabled={offset === 0} onClick={() => { setFilter(""); setOffset(value => Math.max(0, value - 20)); }}>Previous page</button><span>Page {Math.floor(offset / 20) + 1}</span><button disabled={page.nextOffset === null} onClick={() => { setFilter(""); setOffset(page.nextOffset!); }}>Next page</button></nav>
    <button className="text-control" onClick={() => setRetry(value => value + 1)}>Refresh studio</button>
  </>;
}
