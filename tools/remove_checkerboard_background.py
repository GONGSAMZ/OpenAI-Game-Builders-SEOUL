"""Convert a baked light checkerboard around a sprite sheet into alpha.

Only near-neutral bright pixels connected to the image border are treated as
background. This keeps similarly colored details enclosed by the character's
outline, such as gray hair and reflective clothing stripes.
"""

from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path

from PIL import Image, ImageFilter


def is_background_candidate(rgb: tuple[int, int, int]) -> bool:
    red, green, blue = rgb
    return min(rgb) >= 210 and max(rgb) - min(rgb) <= 18


def build_border_background_mask(image: Image.Image) -> Image.Image:
    rgb = image.convert("RGB")
    width, height = rgb.size
    pixels = rgb.load()
    visited = bytearray(width * height)
    queue: deque[tuple[int, int]] = deque()

    def enqueue_if_background(x: int, y: int) -> None:
        index = y * width + x
        if visited[index] or not is_background_candidate(pixels[x, y]):
            return
        visited[index] = 1
        queue.append((x, y))

    for x in range(width):
        enqueue_if_background(x, 0)
        enqueue_if_background(x, height - 1)
    for y in range(height):
        enqueue_if_background(0, y)
        enqueue_if_background(width - 1, y)

    while queue:
        x, y = queue.popleft()
        for next_x, next_y in (
            (x - 1, y),
            (x + 1, y),
            (x, y - 1),
            (x, y + 1),
            (x - 1, y - 1),
            (x + 1, y - 1),
            (x - 1, y + 1),
            (x + 1, y + 1),
        ):
            if 0 <= next_x < width and 0 <= next_y < height:
                enqueue_if_background(next_x, next_y)

    alpha = Image.new("L", (width, height), 255)
    alpha_pixels = alpha.load()
    for index, is_background in enumerate(visited):
        if is_background:
            alpha_pixels[index % width, index // width] = 0

    # A small feather keeps watercolor edges soft without moving the silhouette.
    return alpha.filter(ImageFilter.GaussianBlur(radius=0.55))


def remove_checkerboard(source: Path, destination: Path) -> None:
    image = Image.open(source).convert("RGBA")
    image.putalpha(build_border_background_mask(image))
    destination.parent.mkdir(parents=True, exist_ok=True)
    image.save(destination)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    return parser.parse_args()


if __name__ == "__main__":
    args = parse_args()
    remove_checkerboard(args.source, args.destination)
