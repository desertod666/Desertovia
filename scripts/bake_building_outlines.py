#!/usr/bin/env python3
"""Bake all 26 registered building pairs on one 2px/world grid; rebuild the two atlases."""
import hashlib
import json
import math
import zipfile
from pathlib import Path

from PIL import Image, ImageColor, ImageDraw, ImageChops
from outline_sprites import STYLE, outline, outer_edge

ROOT = Path(__file__).resolve().parent.parent
DEST = ROOT / "art/outlined-buildings"
RUNTIME = ROOT / "web/src/assets/night"
PPE = STYLE["defaultPixelsPerWorldPixel"]
STROKE = round(STYLE["worldWidth"] * PPE)
INK = ImageColor.getrgb(STYLE["color"])


def read(path):
    return json.loads(path.read_text())


def write(path, value):
    path.write_text(json.dumps(value, indent=2) + "\n")


def pack(entries, letter):
    width, gap, x, y, shelf = 1536, 2, 0, 0, 0
    positions, frames = [], {}
    for name, image in entries:
        if x + image.width > width:
            x, y, shelf = 0, y + shelf + gap, 0
        positions.append((image, x, y))
        frames[name] = {"frame": {"x": x, "y": y, "w": image.width, "h": image.height},
                        "sourceSize": {"w": image.width, "h": image.height},
                        "spriteSourceSize": {"x": 0, "y": 0, "w": image.width, "h": image.height}}
        x += image.width + gap
        shelf = max(shelf, image.height)
    height = y + shelf
    assert height <= 1536, "Atlas exceeds the supported phone texture limit"
    atlas = Image.new("RGBA", (width, height))
    for image, x, y in positions:
        atlas.paste(image, (x, y))
    atlas.save(RUNTIME / f"buildings-{letter}.png")
    write(RUNTIME / f"buildings-{letter}.json", {"frames": frames, "meta": {
        "image": f"buildings-{letter}.png", "size": {"w": width, "h": height}, "scale": "1"}})


def pixels(image):
    return image.get_flattened_data() if hasattr(image, "get_flattened_data") else image.getdata()


def main():
    renderer = (ROOT / "web/src/game/BuildingLighting.ts").read_text()
    assert "bodyAt" not in renderer and "outlineStyle" not in renderer, "Remove the runtime contour before baking"
    original = read(DEST / "source-registration.json")
    original_night = read(ROOT / "art/night-buildings/night-atlas.json")["frames"]
    with Image.open(ROOT / "art/night-buildings/night-atlas.png") as source:
        night_atlas = source.convert("RGBA")
    entries, registered, report = [], {}, []
    for kind, pair in original.items():
        day = Image.open(ROOT / f"art/night-buildings/{pair['day']}-day.png").convert("RGBA")
        f = original_night[pair["night"]]["frame"]
        night = night_atlas.crop((f["x"], f["y"], f["x"] + f["w"], f["y"] + f["h"]))
        # Actual caller footprints: ordinary structures 3x3, project/church footprints below.
        plot = {"great-granary": (4, 3), "bell-tower": (3, 4), "monastery-church": (3, 4)}.get(kind, (3, 3))
        left, top, right, bottom = pair["dayBounds"]
        scale = min(.5, plot[0] * 16 / (right - left), plot[1] * 16 / (bottom - top))
        bw, bh = round((right - left) * scale * PPE), round((bottom - top) * scale * PPE)
        transforms, extents = [], []
        for image, bounds in [(day, pair["dayBounds"]), (night, pair["nightBounds"])]:
            l, t, r, b = bounds
            sx, sy = bw / (r - l), bh / (b - t)
            transforms.append((image, l, t, sx, sy))
            box = image.getchannel("A").getbbox()
            extents.append(((box[0]-l)*sx, (box[1]-t)*sy, (box[2]-l)*sx, (box[3]-t)*sy))
        x0 = math.floor(min(e[0] for e in extents)) - STROKE - 1
        y0 = math.floor(min(e[1] for e in extents)) - STROKE - 1
        x1 = math.ceil(max(e[2] for e in extents)) + STROKE + 1
        y1 = math.ceil(max(e[3] for e in extents)) + STROKE + 1
        size = (x1 - x0, y1 - y0)
        bounds = [-x0, -y0, bw - x0, bh - y0]
        registered[kind] = {**pair, "dayBounds": bounds, "nightBounds": bounds,
                            "worldSize": [bw / PPE, bh / PPE]}
        for state, (image, l, t, sx, sy) in zip(["day", "night"], transforms):
            aligned = image.transform(size, Image.Transform.AFFINE,
                (1 / sx, 0, l + x0 / sx, 0, 1 / sy, t + y0 / sy), Image.Resampling.NEAREST)
            mask = aligned.getchannel("A").point(lambda a: 255 if a >= STYLE["alphaThreshold"] else 0)
            edge = outer_edge(mask, STROKE)
            baked = outline(aligned, STROKE, INK)
            # Every added contour pixel is exactly the same opaque ink. No other pixels change.
            assert all(pixel == (*INK, 255) for pixel, selected in zip(pixels(baked), pixels(edge)) if selected)
            difference = ImageChops.difference(baked, aligned)
            assert not ImageChops.multiply(difference.getchannel("A"), ImageChops.invert(edge)).getbbox()
            for channel in difference.split()[:3]:
                assert not ImageChops.multiply(channel, ImageChops.invert(edge)).getbbox()
            assert edge.getbbox() and not any(pixels(edge.crop((0, 0, size[0], 1))))
            assert not any(pixels(edge.crop((0, size[1] - 1, size[0], size[1]))))
            assert not any(pixels(edge.crop((0, 0, 1, size[1]))))
            assert not any(pixels(edge.crop((size[0] - 1, 0, size[0], size[1]))))
            name = pair["day"] if state == "day" else pair["night"]
            baked.save(DEST / f"{name}{'-day' if state == 'day' else ''}.png")
            entries.append((name, baked))
            report.append({"name": name, "state": state, "size": list(size), "ink": STYLE["color"],
                           "strokePixels": STROKE, "pixelsPerWorld": PPE, "shape": "square-outer",
                           "contourPixels": sum(bool(x) for x in pixels(edge)),
                           "outlineMaskSha256": hashlib.sha256(edge.tobytes()).hexdigest(),
                           "sourceAlignedPixelsPreservedOutsideContour": True,
                           "sha256": hashlib.sha256(baked.tobytes()).hexdigest()})
    assert len(entries) == 52
    pack(entries[:26], "a")
    pack(entries[26:], "b")
    write(RUNTIME / "registration.json", registered)
    write(DEST / "validation.json", {"style": STYLE, "frames": report, "count": len(entries),
                                     "runtimeOutlineShader": False, "sourceArtChanged": False})
    # Ready-to-view gallery; day/night pairs share their canvas and ground anchor.
    cell = 240
    gallery = Image.new("RGB", (cell * 4, 190 * 13), "#718354")
    draw = ImageDraw.Draw(gallery)
    for index, (name, image) in enumerate(entries):
        x, y = (index % 4) * cell, (index // 4) * 190
        draw.text((x + 5, y + 4), name, fill="white")
        preview = image.copy()
        preview.thumbnail((230, 162), Image.Resampling.NEAREST)
        gallery.paste(preview, (x + (cell - preview.width) // 2, y + 22), preview)
    gallery.save(DEST / "comparison.png")
    with zipfile.ZipFile(DEST / "outlined-buildings.zip", "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(DEST.glob("building-*.png")) + [DEST / "validation.json", DEST / "README.md"]:
            info = zipfile.ZipInfo(path.name)  # Fixed default timestamp for byte-identical rebuilds.
            info.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(info, path.read_bytes())
    print("Baked and validated all 52 frames; two ready runtime atlases, no outline shader required")


if __name__ == "__main__":
    main()
