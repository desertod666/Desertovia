# Ready outlined buildings — 52 sprites

26 day/night pairs are baked into the two runtime atlases in `web/src/assets/night`.
No runtime contour/filter pass remains. Individual PNGs are ready to reuse; pair
canvases and ground registration match. The existing lighting/crossfade stays intact.

One profile: `art/style/building-outline.json`, #2b1d2b opaque ink,2 source pixels
on the shared2px/world grid (1 world pixel at native scale), external square contour.
Transparent interior holes are preserved. Building silhouettes naturally differ;
the stroke style/colour/thickness/morphology are identical.

`validation.json` lists all52 frames, contour pixel counts/hashes, style, dimensions,
and checks that pixels outside the contour did not change after normalization.
`comparison.png` shows all pairs. Source images remain in `art/night-buildings`;
`source-registration.json` preserves their original alignment.

Rebuild with `python scripts/bake_building_outlines.py` (Pillow required). Always
rebuild from originals, never outline ready images a second time. A repeated build
was verified byte-identical for52 PNGs and runtime atlases/registration. Outlines
are not clipped; all atlas sides remain <=1536 for older phones. Body dimensions
round to the nearest half world pixel (maximum0.25px), preserving ground anchors.

Owner requested GitHub only. This is a prepared branch, not a live deployment.

Local browser checks:4 passed for26 day/night crossfades and emission on desktop/phone;
2 passed for all52 loaded atlas textures' opaque ink. Production build/lint completed.
`outlined-buildings.zip` contains all52 ready individual PNGs, this README and validation.
