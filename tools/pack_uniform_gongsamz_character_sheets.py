"""Create uniformly sized GONGSAMZ customer review sheets.

Every output has a 1536x1024 canvas and three 512x1024 sprite cells.  The
characters are centered inside their cells at a shared 900px maximum height,
so stern/happy/disappointed spacing is mathematically identical.  This creates
review candidates and never overwrites the Unity originals.
"""

from __future__ import annotations

import json
from pathlib import Path

import numpy as np
from PIL import Image


ROOT = Path(r"C:\Users\이혜연\Documents\ChatGPT\OpenAI GAME BUILDERS SEOUL")
OUT = ROOT / "assets" / "gongsamz-revised-characters"
SHEET_SIZE = (1536, 1024)
CELL_WIDTH = 512
CELL_HEIGHT = 1024
MAX_WIDTH = 460
MAX_HEIGHT = 900
BOTTOM_MARGIN = 56

SOURCES = {
    "HaYoung": OUT / "HaYoung-revised-v3-cutout.png",
    "JeongHyun": OUT / "JeongHyun-revised-v2.png",
    "MiJu": OUT / "MiJu-revised-v2.png",
}


def bbox(image: Image.Image, threshold: int = 32) -> tuple[int, int, int, int]:
    alpha = image.getchannel("A").point(lambda value: 255 if value > threshold else 0)
    return alpha.getbbox() or (0, 0, image.width, image.height)


def crop_columns(image: Image.Image) -> list[Image.Image]:
    boundaries = [round(image.width * index / 3) for index in range(4)]
    columns = []
    for left, right in zip(boundaries[:-1], boundaries[1:]):
        column = image.crop((left, 0, right, image.height))
        columns.append(column.crop(bbox(column)))
    return columns


def resize_premultiplied(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    data = np.asarray(image.convert("RGBA"), dtype=np.uint16)
    alpha = data[:, :, 3:4]
    data[:, :, :3] = (data[:, :, :3] * alpha) // 255
    resized = Image.fromarray(data.astype(np.uint8), "RGBA").resize(size, Image.Resampling.LANCZOS)
    result = np.asarray(resized, dtype=np.uint16).copy()
    alpha = result[:, :, 3:4]
    result[:, :, :3] = np.where(alpha > 0, np.minimum(255, (result[:, :, :3] * 255) // np.maximum(alpha, 1)), 0)
    return Image.fromarray(result.astype(np.uint8), "RGBA")


def pack(name: str, source: Path) -> dict[str, object]:
    sheet = Image.open(source).convert("RGBA")
    canvas = Image.new("RGBA", SHEET_SIZE, (0, 0, 0, 0))
    placements = []
    for index, sprite in enumerate(crop_columns(sheet)):
        scale = min(MAX_WIDTH / sprite.width, MAX_HEIGHT / sprite.height)
        size = (max(1, round(sprite.width * scale)), max(1, round(sprite.height * scale)))
        sprite = resize_premultiplied(sprite, size)
        x = index * CELL_WIDTH + (CELL_WIDTH - sprite.width) // 2
        y = CELL_HEIGHT - BOTTOM_MARGIN - sprite.height
        canvas.alpha_composite(sprite, (x, y))
        placements.append({"index": index, "cell": [index * CELL_WIDTH, 0, CELL_WIDTH, CELL_HEIGHT], "placed": [x, y, sprite.width, sprite.height]})
    destination = OUT / f"{name}-uniform-v4.png"
    canvas.save(destination, "PNG", optimize=True)
    return {"name": name, "sheetSize": list(SHEET_SIZE), "cellSize": [CELL_WIDTH, CELL_HEIGHT], "placements": placements, "output": str(destination)}


def main() -> None:
    print(json.dumps([pack(name, source) for name, source in SOURCES.items()], ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
