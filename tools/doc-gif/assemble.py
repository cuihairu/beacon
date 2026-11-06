#!/usr/bin/env python3
"""Assemble the off/on captures from capture-config-effects.ps1 into docs GIFs.

    python tools/doc-gif/assemble.py <frames-dir> <out-dir>

Input : <frames-dir>/<key>-off.png, <key>-on.png  (optionally <key>-off-fN.png / <key>-on-fN.png
        for switches whose effect is animation, e.g. motion mode)
Output: <out-dir>/<key>.gif  plus the two stills as <key>-off.png / <key>-on.png

Re-runnable: it only reads the frame directory and rewrites the outputs, so the whole
DOC-GIF pipeline is `capture-config-effects.ps1` (Windows) -> this script (anywhere).
"""
from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

PAD = 16
LABEL_H = 26
BG = (24, 26, 32)
FG = (235, 238, 242)
MAX_W = 760
HOLD_MS = 900


def _labelled(img: Image.Image, text: str) -> Image.Image:
    """Letterbox onto a shared canvas with an off/on caption strip."""
    scale = min(1.0, MAX_W / img.width)
    if scale < 1.0:
        img = img.resize((int(img.width * scale), int(img.height * scale)), Image.LANCZOS)
    canvas = Image.new("RGB", (img.width + PAD * 2, img.height + PAD * 2 + LABEL_H), BG)
    canvas.paste(img, (PAD, PAD))
    draw = ImageDraw.Draw(canvas)
    draw.text((PAD, img.height + PAD + 5), text, fill=FG)
    return canvas


def _fit(img: Image.Image, size: tuple[int, int]) -> Image.Image:
    """Pad (never crop) so every frame of one GIF has identical dimensions."""
    out = Image.new("RGB", size, BG)
    out.paste(img, ((size[0] - img.width) // 2, (size[1] - img.height) // 2))
    return out


def assemble(frames_dir: Path, out_dir: Path) -> list[str]:
    out_dir.mkdir(parents=True, exist_ok=True)
    written: list[str] = []

    import re

    keys = sorted(
        {
            m.group(1)
            for p in frames_dir.glob("*.png")
            if (m := re.match(r"^(.+)-off(?:-f\d+)?\.png$", p.name))
        }
    )
    for key in keys:
        off_frames = sorted(frames_dir.glob(f"{key}-off-f*.png")) or [frames_dir / f"{key}-off.png"]
        on_frames = sorted(frames_dir.glob(f"{key}-on-f*.png")) or [frames_dir / f"{key}-on.png"]
        off_frames = [p for p in off_frames if p.exists()]
        on_frames = [p for p in on_frames if p.exists()]
        if not off_frames or not on_frames:
            print(f"skip {key}: missing off/on pair")
            continue

        still_off = _labelled(Image.open(off_frames[0]).convert("RGB"), f"{key}: off")
        still_on = _labelled(Image.open(on_frames[0]).convert("RGB"), f"{key}: on")
        canvas_size = (
            max(still_off.width, still_on.width),
            max(still_off.height, still_on.height),
        )
        stills = [_fit(still_off, canvas_size), _fit(still_on, canvas_size)]

        # animation: hold each state, and for motion switches keep every captured frame
        sequence: list[Image.Image] = []
        for img, frames in ((stills[0], off_frames), (stills[1], on_frames)):
            if len(frames) > 1:
                for f in frames:
                    labelled = _labelled(Image.open(f).convert("RGB"), f"{key}: {'off' if img is stills[0] else 'on'}")
                    sequence.append(_fit(labelled, canvas_size))
            else:
                sequence.append(img)

        gif_path = out_dir / f"{key}.gif"
        sequence[0].save(
            gif_path,
            save_all=True,
            append_images=sequence[1:],
            duration=[HOLD_MS] * len(sequence),
            loop=0,
            optimize=True,
        )
        for img, suffix in zip(stills, ("off", "on")):
            img.save(out_dir / f"{key}-{suffix}.png")
        written.append(gif_path.name)
        print(f"{gif_path.name}: {len(sequence)} frames")
    return written


def main(argv: list[str]) -> int:
    if len(argv) != 3:
        print(__doc__)
        return 2
    written = assemble(Path(argv[1]), Path(argv[2]))
    print(f"{len(written)} gif(s) -> {argv[2]}")
    return 0 if written else 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
