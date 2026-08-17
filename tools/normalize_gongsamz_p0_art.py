"""Normalize GONGSAMZ P0 review candidates to the original Unity sprite canvases.

This preserves the original canvas size and places the generated cutout over the
same visible alpha bounds as its source sprite.  It creates review candidates;
it never overwrites Unity project files.
"""

from __future__ import annotations

import json
from pathlib import Path

from PIL import Image


ROOT = Path(r"C:\Users\이혜연\Documents\ChatGPT\OpenAI GAME BUILDERS SEOUL")
UNITY = Path(r"C:\DevHub\02_GameDev\GONGSAMZ\BungeoppangTycoon\Assets\Resources\Sprites")
OUT = ROOT / "assets" / "gongsamz-revised-art"

ITEMS = {
    "cookingPlate": (UNITY / "cookingPlate.png", OUT / "cookingPlate-revised-v2-cutout.png"),
    "displayPlate": (UNITY / "displayPlate.png", OUT / "displayPlate-revised-v2-cutout.png"),
    "fishMold": (UNITY / "fishMold.png", OUT / "fishMold-revised-v2-cutout.png"),
    "tongs": (UNITY / "tongs.png", OUT / "tongs-revised-v2-cutout.png"),
}


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int]:
    return image.getchannel("A").getbbox() or (0, 0, image.width, image.height)


def normalize(name: str, source: Path, candidate: Path) -> dict[str, object]:
    original = Image.open(source).convert("RGBA")
    generated = Image.open(candidate).convert("RGBA")
    target_bbox = alpha_bbox(original)
    source_bbox = alpha_bbox(generated)

    crop = generated.crop(source_bbox)
    target_width = target_bbox[2] - target_bbox[0]
    target_height = target_bbox[3] - target_bbox[1]
    resized = crop.resize((target_width, target_height), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", original.size, (0, 0, 0, 0))
    canvas.alpha_composite(resized, (target_bbox[0], target_bbox[1]))

    output = OUT / f"{name}-revised-v2.png"
    canvas.save(output, "PNG", optimize=True)
    return {
        "name": name,
        "originalSize": list(original.size),
        "targetAlphaBounds": list(target_bbox),
        "output": str(output),
        "outputSize": list(canvas.size),
    }


def main() -> None:
    report = [normalize(name, source, candidate) for name, (source, candidate) in ITEMS.items()]
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
