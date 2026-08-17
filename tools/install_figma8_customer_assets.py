from __future__ import annotations

import hashlib
import shutil
from pathlib import Path


WORKSPACE = Path(r"C:\Users\이혜연\Documents\ChatGPT\OpenAI GAME BUILDERS SEOUL")
SOURCE_DIR = WORKSPACE / "assets" / "figma-8-customers"
UNITY_PROJECT = Path(r"C:\DevHub\02_GameDev\GONGSAMZ\BungeoppangTycoon")
TARGET_DIR = UNITY_PROJECT / "Assets" / "Resources" / "Sprites" / "Customers" / "Figma8"
TARGET_DIR_META = TARGET_DIR.with_suffix(".meta")
DOC_TARGET = UNITY_PROJECT.parent / "docs" / "06_FIGMA_8_CUSTOMERS_IMPORT.md"
MANIFEST_SOURCE = SOURCE_DIR / "IMPORT_MANIFEST.md"

ASSETS = (
    ("01_JeongHyun.png", "JeongHyun"),
    ("02_HaYoung.png", "HaYoung"),
    ("03_MiJu.png", "MiJu"),
    ("04_Sunja.png", "Sunja"),
    ("05_Geonwoo.png", "Geonwoo"),
    ("06_Taesu.png", "Taesu"),
    ("07_Nari.png", "Nari"),
    ("08_Junho.png", "Junho"),
)

ORIGINALS = (
    UNITY_PROJECT / "Assets" / "Resources" / "Sprites" / "Customers" / "JeongHyun.png",
    UNITY_PROJECT / "Assets" / "Resources" / "Sprites" / "Customers" / "HaYoung.png",
    UNITY_PROJECT / "Assets" / "Resources" / "Sprites" / "Customers" / "MiJu.png",
)


def md5_hex(seed: str) -> str:
    return hashlib.md5(seed.encode("utf-8"), usedforsecurity=False).hexdigest()


def signed_id(seed: str) -> int:
    value = int(md5_hex(seed)[:8], 16)
    return value if value < 2**31 else value - 2**32


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def sprite_block(asset_name: str, label: str, index: int, x: int) -> tuple[str, int]:
    internal_id = signed_id(f"gongsamz/figma8/{asset_name}/{label}/internal")
    sprite_id = md5_hex(f"gongsamz/figma8/{asset_name}/{label}/sprite")
    block = f"""    - serializedVersion: 2
      name: {asset_name}_{label}
      rect:
        serializedVersion: 2
        x: {x}
        y: 0
        width: 512
        height: 1024
      alignment: 0
      pivot: {{x: 0, y: 0}}
      border: {{x: 0, y: 0, z: 0, w: 0}}
      outline: []
      physicsShape: []
      tessellationDetail: 0
      bones: []
      spriteID: {sprite_id}
      internalID: {internal_id}
      vertices: []
      indices: 
      edges: []
      weights: []
"""
    return block, internal_id


def texture_meta(file_name: str, asset_name: str) -> str:
    guid = md5_hex(f"gongsamz/figma8/{file_name}/guid")
    blocks: list[str] = []
    name_ids: list[tuple[str, int]] = []
    for label, index, x in (("Default", 0, 0), ("Joy", 1, 512), ("Disappointed", 2, 1024)):
        block, internal_id = sprite_block(asset_name, label, index, x)
        blocks.append(block)
        name_ids.append((f"{asset_name}_{label}", internal_id))
    table = "\n".join(f"      {name}: {internal_id}" for name, internal_id in name_ids)
    sprites = "".join(blocks)
    return f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 2
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 3
    buildTarget: Standalone
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 3
    buildTarget: iPhone
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites:
{sprites}    outline: []
    physicsShape: []
    bones: []
    spriteID: {md5_hex(f'gongsamz/figma8/{file_name}/sheet')} 
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable:
{table}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: Figma source Yrun8rClSF4bLDLSsDDjuQ
  assetBundleName: 
  assetBundleVariant: 
"""


def main() -> None:
    missing = [str(SOURCE_DIR / file_name) for file_name, _ in ASSETS if not (SOURCE_DIR / file_name).is_file()]
    if missing:
        raise FileNotFoundError("Missing source assets: " + ", ".join(missing))

    original_hashes = {path: sha256(path) for path in ORIGINALS}
    TARGET_DIR.mkdir(parents=True, exist_ok=True)
    if not TARGET_DIR_META.exists():
        TARGET_DIR_META.write_text(
            f"fileFormatVersion: 2\nguid: {md5_hex('gongsamz/figma8/folder/guid')}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n",
            encoding="utf-8",
        )

    for file_name, asset_name in ASSETS:
        source = SOURCE_DIR / file_name
        target = TARGET_DIR / file_name
        shutil.copy2(source, target)
        target.with_suffix(target.suffix + ".meta").write_text(
            texture_meta(file_name, asset_name), encoding="utf-8", newline="\n"
        )

    DOC_TARGET.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(MANIFEST_SOURCE, DOC_TARGET)

    changed = [str(path) for path, before in original_hashes.items() if sha256(path) != before]
    if changed:
        raise RuntimeError("Existing customer originals changed: " + ", ".join(changed))

    print(f"Installed {len(ASSETS)} PNG files and {len(ASSETS)} Unity meta files to {TARGET_DIR}")
    print(f"Preserved {len(ORIGINALS)} existing customer PNG files without changes")
    print(f"Wrote import record to {DOC_TARGET}")


if __name__ == "__main__":
    main()
