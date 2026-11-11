"use client";

import { useState } from "react";
import { shareMessage, shareStory, storyShareData } from "@/lib/share";

export function ShareButton({ id, title, artistName }: { id: string; title: string; artistName: string }) {
  const [message, setMessage] = useState("");
  async function share() {
    const url = `${window.location.origin}/story/${id}`;
    setMessage(shareMessage(await shareStory(storyShareData(title, artistName, url), navigator), url));
  }
  return <div className="share-story">
    <button className="text-control" type="button" onClick={() => void share()}>Share this story</button>
    {message && <p className="share-message" role="status">{message}</p>}
  </div>;
}
