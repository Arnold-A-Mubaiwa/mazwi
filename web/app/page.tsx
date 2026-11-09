import Link from "next/link";
import { LanguagePicker } from "@/components/language-picker";
export default function Home() {
  return <main id="main">
    <section className="hero" aria-labelledby="hero-title">
      <div className="hero-copy">
        <p className="eyebrow"><span /> MANY LANGUAGES. ONE PLACE TO BELONG.</p>
        <h1 id="hero-title">Every African story,<br /><em>in your language.</em></h1>
        <p className="hero-description">The stories that raised us. The voices we haven’t met yet.<br className="desktop-break" /> A little closer, one language at a time.</p>
        <Link className="text-link" href="/about">Meet Mazwi <span aria-hidden="true">↗</span></Link>
      </div>
      <div className="voice-art" aria-hidden="true"><div className="art-grain" />
        <span className="art-corner">MAZWI / WORDS & VOICES</span>
        <div className="art-orbit orbit-one" /><div className="art-orbit orbit-two" /><div className="art-orbit orbit-three" />
        <div className="art-words"><span>Stories</span><span>find a</span><em>way home.</em></div>
        <div className="art-bottom"><span>FROM ONE VOICE<br />TO ANOTHER.</span><svg viewBox="0 0 100 42"><path d="M3 21h5l4-12 6 25 6-32 6 38 6-28 6 18 6-23 6 27 6-19 6 11 6-16 6 22 6-11h9" /></svg></div>
      </div>
    </section>
    <section id="stories" className="stories-section" aria-labelledby="stories-title">
      <div className="section-heading"><div><p className="eyebrow">PULL UP A CHAIR</p><h2 id="stories-title">A world of voices.</h2></div><span className="section-number">01 — EXPLORE</span></div>
      <div className="explore-layout">
        <aside className="preference-panel"><LanguagePicker /><div className="small-divider" /><p className="preference-caption">A poem. A folktale. A voice that feels like home.</p><p className="muted-copy">Choose your language. Approved stories will appear here as they become available.</p></aside>
        <div className="empty-stories"><div className="empty-mark" aria-hidden="true"><span /><span /><span /><span /><span /></div>
          <p className="eyebrow">GOOD STORIES DESERVE A MOMENT</p><h3>The first voices<br />are on their way.</h3>
          <p>This space is waiting for its storytellers.<br />Every translation is shared with its artist’s approval.</p>
          <div className="trust-note"><span aria-hidden="true">✳</span> Their words. Their voice. Their say.</div>
        </div>
      </div>
    </section>
    <section className="closing-note"><span className="closing-symbol" aria-hidden="true">✳</span><p>Some stories cross borders.<br /><em>Others remind us we were never that far apart.</em></p></section>
  </main>;
}
