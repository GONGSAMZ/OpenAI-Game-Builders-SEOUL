"""Pack generated character triptychs into the original Unity sprite-sheet layout.

The script creates review candidates only. It retains the original sheet sizes and
the three existing Unity slice rectangles; no Unity asset or .meta file is changed.
"""

from __future__ import annotations

import json
from pathlib import Path

import numpy as np
from PIL import Image


WORKSPACE = Path(r"C:\Users\이혜연\Documents\ChatGPT\OpenAI GAME BUILDERS SEOUL")
UNITY_SPRITES = Path(r"C:\DevHub\02_GameDev\GONGSAMZ\BungeoppangTycoon\Assets\Resources\Sprites\Customers")
OUTPUT = WORKSPACE / "assets" / "gongsamz-revised-characters"

# Unity sprite rectangles use a bottom-left origin: x, y, width, height.
CHARACTERS = {
    "HaYoung": {
        "original": UNITY_SPRITES / "HaYoung.png",
        "generated": OUTPUT / "source" / "HaYoung-v1-source.png",
        "rects": [(0, 103, 380, 700), (425, 103, 380, 700), (850, 103, 380, 700)],
    },
    "JeongHyun": {
        "original": UNITY_SPRITES / "JeongHyun.png",
        "generated": OUTPUT / "source" / "JeongHyun-v1-source.png",
        "rects": [(10, 0, 338, 580), (348, 0, 338, 580), (686, 0, 338, 580)],
    },
    "MiJu": {
        "original": UNITY_SPRITES / "MiJu.png",
        "generated": OUTPUT / "source" / "MiJu-v1-source.png",
        "rects": [(3, 450, 340, 700), (342, 450, 340, 700), (683, 450, 340, 700)],
    },
}


def content_bbox(image: Image.Image, alpha_threshold: int = 32) -> tuple[int, int, int, int]:
    alpha = image.getchannel("A")
    mask = alpha.point(lambda value: 255 if value > alpha_threshold else 0)
    return mask.getbbox() or (0, 0, image.width, image.height)


def source_columns(image: Image.Image) -> list[Image.Image]:
    width = image.width // 3
    columns = []
    for index in range(3):
        left = index * width
        right = image.width if index == 2 else (index + 1) * width
        column = image.crop((left, 0, right, image.height))
        columns.append(column.crop(content_bbox(column)))
    return columns


def resize_with_clean_alpha(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    """Resize in premultiplied-alpha space to avoid dark/coloured edge halos."""
    data = np.asarray(image.convert("RGBA"), dtype=np.uint16)
    alpha = data[:, :, 3:4]
    data[:, :, :3] = (data[:, :, :3] * alpha) // 255
    resized = Image.fromarray(data.astype(np.uint8), "RGBA").resize(size, Image.Resampling.LANCZOS)
    result = np.asarray(resized, dtype=np.uint16).copy()
    result_alpha = result[:, :, 3:4]
    visible = result_alpha > 0
    result[:, :, :3] = np.where(visible, np.minimum(255, (result[:, :, :3] * 255) // np.maximum(result_alpha, 1)), 0)
    return Image.fromarray(result.astype(np.uint8), "RGBA")


def pack_character(name: str, config: dict[str, object]) -> dict[str, object]:
    original = Image.open(config["original"]).convert("RGBA")
    generated = Image.open(config["generated"]).convert("RGBA")
    canvas = Image.new("RGBA", original.size, (0, 0, 0, 0))
    columns = source_columns(generated)

    for sprite, (x, unity_y, width, height) in zip(columns, config["rects"]):
        # Fit inside each original slice while retaining a small safety margin.
        max_width = round(width * 0.94)
        max_height = round(height * 0.96)
        scale = min(max_width / sprite.width, max_height / sprite.height)
        size = (max(1, round(sprite.width * scale)), max(1, round(sprite.height * scale)))
        resized = resize_with_clean_alpha(sprite, size)

        left = x + (width - resized.width) // 2
        top = original.height - unity_y - height + (height - resized.height)
        canvas.alpha_composite(resized, (left, top))

    output = OUTPUT / f"{name}-revised-v2.png"
    canvas.save(output, "PNG", optimize=True)
    return {
        "name": name,
        "originalSize": list(original.size),
        "spriteRects": config["rects"],
        "output": str(output),
        "alphaBounds": list(content_bbox(canvas)),
    }


def main() -> None:
    report = [pack_character(name, config) for name, config in CHARACTERS.items()]
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
