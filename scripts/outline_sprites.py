#!/usr/bin/env python3
"""Add an outer pixel-art outline to transparent PNGs, without generative AI."""
import argparse
import json
from collections import deque
from pathlib import Path

from PIL import Image, ImageColor, ImageFilter, ImageChops


STYLE = json.loads((Path(__file__).resolve().parent.parent / "art/style/building-outline.json").read_text())

def outline(image, width, color, threshold=STYLE["alphaThreshold"], mask=None, padding=0):
    image = image.convert("RGBA")
    if mask is not None and mask.size != image.size:
        raise ValueError("Mask must have the same dimensions and alignment as the sprite")
    alpha = (mask or image).convert("RGBA").getchannel("A")
    silhouette = alpha.point(lambda value: 255 if value >= threshold else 0)
    if silhouette.getextrema() == (255, 255):
        raise ValueError("No transparent background: remove the background first")
    if not silhouette.getbbox():
        raise ValueError("Empty silhouette: lower --threshold or supply a mask")
    if padding:
        canvas = Image.new("RGBA", (image.width + padding * 2, image.height + padding * 2))
        canvas.paste(image, (padding, padding))
        image = canvas
        expanded = Image.new("L", image.size)
        expanded.paste(silhouette, (padding, padding))
        silhouette = expanded

    edge = outer_edge(silhouette, width)
    background = Image.new("RGBA", image.size, (*color, 255))
    # Exact ink, including over semi-transparent halo pixels at the contour.
    return Image.composite(background, image, edge)


def outer_edge(silhouette, width):
    # Flood only background connected to the border: don't outline windows/holes.
    w, h = silhouette.size
    solid = silhouette.tobytes()
    outside = bytearray(w * h)
    queue = deque()

    def visit(index):
        if not solid[index] and not outside[index]:
            outside[index] = 255
            queue.append(index)

    for x in range(w):
        visit(x)
        visit((h - 1) * w + x)
    for y in range(h):
        visit(y * w)
        visit(y * w + w - 1)
    while queue:
        index = queue.popleft()
        x, y = index % w, index // w
        if x: visit(index - 1)
        if x + 1 < w: visit(index + 1)
        if y: visit(index - w)
        if y + 1 < h: visit(index + w)

    expanded = silhouette.filter(ImageFilter.MaxFilter(width * 2 + 1))
    edge = ImageChops.multiply(expanded, Image.frombytes("L", silhouette.size, bytes(outside)))
    return edge


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path, help="PNG file or folder of individual sprites")
    parser.add_argument("--output", required=True, type=Path, help="Separate output folder")
    parser.add_argument("--pattern", default="*.png", help="Folder glob, e.g. building-*-night.png")
    parser.add_argument("--recursive", action="store_true")
    style = STYLE
    parser.add_argument("--pixels-per-world", type=float, default=style["defaultPixelsPerWorldPixel"], help="Source pixels per world pixel; shared style determines contour width")
    parser.add_argument("--width", type=int, default=None, help="Optional standalone override in source pixels (1..16)")
    parser.add_argument("--color", default=style["color"], help="Opaque outline colour")
    parser.add_argument("--threshold", type=int, default=style["alphaThreshold"], help="Silhouette alpha cutoff (1..255)")
    parser.add_argument("--padding", type=int, default=0, help="Extra transparent border; changes dimensions/anchors")
    parser.add_argument("--mask-dir", type=Path, help="Optional aligned silhouette PNGs with matching filenames")
    parser.add_argument("--overwrite", action="store_true", help="Replace previous outputs, never source files")
    args = parser.parse_args()
    if args.pixels_per_world <= 0:
        parser.error("pixels-per-world must be positive")
    if args.width is None:
        args.width = max(1, round(style["worldWidth"] * args.pixels_per_world))
    if not 1 <= args.width <= 16 or not 1 <= args.threshold <= 255 or args.padding < 0:
        parser.error("width must be 1..16, threshold 1..255, padding >= 0")
    try:
        color = ImageColor.getrgb(args.color)
        if len(color) != 3:
            raise ValueError("Use an opaque RGB colour")
        root = args.input if args.input.is_dir() else args.input.parent
        if args.output.resolve() == root.resolve():
            raise ValueError("Output must be a different folder")
        if args.input.is_file():
            files = [args.input]
        elif args.input.is_dir():
            files = sorted((args.input.rglob if args.recursive else args.input.glob)(args.pattern))
            files = [file for file in files if file.is_file() and not file.resolve().is_relative_to(args.output.resolve())]
        else:
            raise ValueError("Input does not exist")
        if not files:
            raise ValueError("No matching PNGs")
        for source in files:
            if source.suffix.lower() != ".png":
                raise ValueError(f"Not a PNG: {source}")
            relative = source.relative_to(root)
            target = args.output / relative
            if target.resolve() == source.resolve():
                raise ValueError("Refusing to overwrite a source")
            if target.exists() and not args.overwrite:
                raise ValueError(f"Output exists: {target}; use --overwrite or another folder")
            with Image.open(source) as image:
                mask = None
                if args.mask_dir:
                    with Image.open(args.mask_dir / relative) as reference:
                        mask = reference.convert("RGBA")
                result = outline(image, args.width, color, args.threshold, mask, args.padding)
                target.parent.mkdir(parents=True, exist_ok=True)
                result.save(target)
        print(f"Saved {len(files)} outlined sprites to {args.output}; sources unchanged")
    except (ValueError, OSError) as error:
        parser.exit(1, f"Error: {error}\n")


if __name__ == "__main__":
    main()
