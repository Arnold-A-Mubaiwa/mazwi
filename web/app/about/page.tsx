import Link from "next/link";
import type { Metadata } from "next";
export const metadata: Metadata = { title: "Our story" };
export default function About() {
  return <main id="main" className="about-page">
    <p className="eyebrow">MAZWI MEANS WORDS. MAZWI MEANS VOICES.</p><h1>Stories belong<br /><em>to all of us.</em></h1>
    <p className="about-intro">Mazwi began with a grandmother who speaks Shona, and a world of stories she couldn’t understand.</p>
    <div className="about-grid">
      <section><span>01</span><h2>A bridge between languages.</h2><p>We’re building a place where African stories can travel between African languages, while their storytellers keep their names, humour, and meaning.</p></section>
      <section><span>02</span><h2>The artist has the last word.</h2><p>Our approach puts review before reach: artists listen, correct, and approve a translation before it is shared.</p></section>
      <section><span>03</span><h2>Every community gets its credit.</h2><p>Folktales connect us across borders. We want those connections to honour the people and places behind every telling.</p></section>
    </div><Link className="text-link" href="/">Back to Explore <span aria-hidden="true">↗</span></Link>
  </main>;
}
