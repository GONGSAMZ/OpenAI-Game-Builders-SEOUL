"""Install non-destructive, uniformly sliced customer review candidates into Unity.

Writes only to Assets/Resources/Sprites/Customers/Revised.  Original customer
PNGs and their .meta files remain untouched.  Each candidate uses 1536x1024
with three equal 512x1024 slices, PPU 100, centered pivots, Bilinear filtering,
mipmaps disabled, and alpha transparency enabled.
"""

from __future__ import annotations

import re
import shutil
from pathlib import Path


WORKSPACE = Path(r"C:\Users\이혜연\Documents\ChatGPT\OpenAI GAME BUILDERS SEOUL")
UNITY_CUSTOMERS = Path(r"C:\DevHub\02_GameDev\GONGSAMZ\BungeoppangTycoon\Assets\Resources\Sprites\Customers")
REVISED = UNITY_CUSTOMERS / "Revised"
SOURCE = WORKSPACE / "assets" / "gongsamz-revised-characters"

ITEMS = {
    "HaYoung": ("HaYoung-uniform-v4.png", "15fdeec5f4f64e8e8a1c3ec32649603b"),
    "JeongHyun": ("JeongHyun-uniform-v4.png", "2e3ae326ffc349a1bd042257417ad7d2"),
    "MiJu": ("MiJu-uniform-v4.png", "3e90a31df14f4f7ca24d9693c86a94a7"),
}

RECT_PATTERN = re.compile(
    r"(rect:\s*\n\s*serializedVersion: 2\s*\n\s*x: )\d+(\s*\n\s*y: )\d+(\s*\n\s*width: )\d+(\s*\n\s*height: )\d+"
)


def rewrite_meta(source_meta: Path, destination_meta: Path, guid: str) -> None:
    text = source_meta.read_text(encoding="utf-8")
    text = re.sub(r"(?m)^guid: [0-9a-f]+$", f"guid: {guid}", text, count=1)
    index = 0

    def replacement(match: re.Match[str]) -> str:
        nonlocal index
        x = index * 512
        index += 1
        return f"{match.group(1)}{x}{match.group(2)}0{match.group(3)}512{match.group(4)}1024"

    text, count = RECT_PATTERN.subn(replacement, text, count=3)
    if count != 3:
        raise RuntimeError(f"Expected three sprite rectangles in {source_meta}, found {count}.")
    destination_meta.write_text(text, encoding="utf-8")


def main() -> None:
    REVISED.mkdir(parents=True, exist_ok=True)
    for character, (candidate_name, guid) in ITEMS.items():
        candidate = SOURCE / candidate_name
        target = REVISED / candidate_name
        if not candidate.exists():
            raise FileNotFoundError(candidate)
        shutil.copy2(candidate, target)
        rewrite_meta(UNITY_CUSTOMERS / f"{character}.png.meta", target.with_suffix(".png.meta"), guid)
        print(f"Installed {target}")


if __name__ == "__main__":
    main()
