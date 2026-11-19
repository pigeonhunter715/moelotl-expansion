#!/usr/bin/env python3
"""Split a 4x3 chroma-key medicine sheet into centered transparent RimWorld icons."""
from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image


NAMES = (
    "MX_WarmingPill",
    "MX_CoolingPill",
    "MX_IndustryPill",
    "MX_RecoveryPill",
    "MX_CombatPill",
    "MX_DispelPill",
    "MX_CapacityPill",
    "MX_PurifyPill",
    "MX_GatherPill",
    "MX_ReleasePill",
    "MX_RescuePill",
    "MX_MedicinePendant",
)
KEY = np.array((0.0, 255.0, 0.0), dtype=np.float32)


def connected_background(rgb: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    distance = np.linalg.norm(rgb.astype(np.float32) - KEY, axis=2)
    candidate = (
        (distance < 135)
        & (rgb[:, :, 1] > rgb[:, :, 0] * 1.08)
        & (rgb[:, :, 1] > rgb[:, :, 2] * 1.08)
    )
    height, width = candidate.shape
    background = np.zeros_like(candidate)
    queue = deque()

    def add(y: int, x: int) -> None:
        if candidate[y, x] and not background[y, x]:
            background[y, x] = True
            queue.append((y, x))

    for x in range(width):
        add(0, x)
        add(height - 1, x)
    for y in range(height):
        add(y, 0)
        add(y, width - 1)

    while queue:
        y, x = queue.popleft()
        for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
            if 0 <= ny < height and 0 <= nx < width:
                add(ny, nx)

    alpha = np.full(distance.shape, 255, dtype=np.uint8)
    matte = np.clip((distance - 35.0) / 100.0, 0.0, 1.0)
    alpha[background] = np.round(matte[background] * 255).astype(np.uint8)
    return background, alpha


def make_icon(cell: Image.Image, size: int = 128) -> Image.Image:
    rgb = np.asarray(cell.convert("RGB"), dtype=np.uint8)
    background, alpha = connected_background(rgb)
    alpha_f = alpha.astype(np.float32) / 255.0
    rgb_f = rgb.astype(np.float32)
    partial = background & (alpha_f > 0.0) & (alpha_f < 1.0)
    if np.any(partial):
        rgb_f[partial] = (
            rgb_f[partial] - KEY * (1.0 - alpha_f[partial, None])
        ) / alpha_f[partial, None]
    rgba = np.dstack((np.clip(rgb_f, 0, 255).astype(np.uint8), alpha))
    icon = Image.fromarray(rgba, "RGBA")
    bounds = icon.getchannel("A").point(lambda value: 255 if value > 12 else 0).getbbox()
    if bounds is None:
        raise ValueError("A grid cell became fully transparent; check the generated sheet and grid alignment.")

    left, top, right, bottom = bounds
    pad = 6
    crop = icon.crop((max(0, left - pad), max(0, top - pad), min(icon.width, right + pad), min(icon.height, bottom + pad)))
    crop.thumbnail((size - 16, size - 16), Image.Resampling.LANCZOS)
    output = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    output.alpha_composite(crop, ((size - crop.width) // 2, (size - crop.height) // 2))
    return output


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("sheet", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()

    sheet = Image.open(args.sheet).convert("RGB")
    if sheet.width % 4 or sheet.height % 3:
        raise SystemExit("Input dimensions must divide evenly into four columns and three rows.")
    cell_w, cell_h = sheet.width // 4, sheet.height // 3
    args.output.mkdir(parents=True, exist_ok=True)

    for index, name in enumerate(NAMES):
        x, y = index % 4, index // 4
        cell = sheet.crop((x * cell_w, y * cell_h, (x + 1) * cell_w, (y + 1) * cell_h))
        icon = make_icon(cell)
        path = args.output / f"{name}.png"
        icon.save(path, optimize=True)
        print(f"{path}: {icon.width}x{icon.height}, alpha={icon.getchannel('A').getextrema()}")


if __name__ == "__main__":
    main()
