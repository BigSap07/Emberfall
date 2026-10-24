"""Draws the ember flame from web/index.html (the header svg path) into web/ember.ico. Standard library only.

    python make_icon.py [out.ico]

The path is flattened to a polygon, filled with 4x4 supersampling and stored as PNG images (16 to 256 px)
inside one .ico, which Windows has accepted since Vista.
"""

from __future__ import annotations

import math
import struct
import sys
import zlib
from pathlib import Path

HERE = Path(__file__).resolve().parent
SIZES = (16, 24, 32, 48, 64, 128, 256)
SS = 4                                   # supersampling per axis
FLAME = (0xF9, 0x73, 0x16)               # --hot orange of style.css


def cubic(p0, p1, p2, p3, n=24):
    out = []
    for i in range(1, n + 1):
        t = i / n
        u = 1 - t
        out.append((u**3 * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t**3 * p3[0],
                    u**3 * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t**3 * p3[1]))
    return out


def flame_polygon() -> list[tuple[float, float]]:
    """M12 2 c1 4.2 6.2 6.2 6.2 12 a6.2 6.2 0 0 1 -12.4 0 c0-3.2 2-4.6 3.2-7.4 c.9 1.8 1.6 2.2 3 0 z, in a 24 box."""
    pts = [(12.0, 2.0)]
    pts += cubic((12, 2), (13, 6.2), (18.2, 8.2), (18.2, 14))
    pts += [(12 + 6.2 * math.cos(math.pi * i / 32), 14 + 6.2 * math.sin(math.pi * i / 32)) for i in range(1, 33)]  # semicircle, down
    pts += cubic((5.8, 14), (5.8, 10.8), (7.8, 9.4), (9.0, 6.6))
    pts += cubic((9.0, 6.6), (9.9, 8.4), (10.6, 8.8), (12.0, 6.6))
    return pts


def inside(poly, x: float, y: float) -> bool:
    c = False
    j = len(poly) - 1
    for i, (xi, yi) in enumerate(poly):
        xj, yj = poly[j]
        if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / (yj - yi) + xi:
            c = not c
        j = i
    return c


def render(size: int, poly) -> bytes:
    pad = 0.08 * size
    scale = (size - 2 * pad) / 20.2           # the flame spans y 2..20.2 and x 5.8..18.2 of the 24 box
    ox = size / 2 - 12 * scale
    oy = pad - 2 * scale
    px = bytearray()
    for y in range(size):
        px.append(0)                          # PNG filter: none
        for x in range(size):
            hit = 0
            for sy in range(SS):
                for sx in range(SS):
                    ux = ((x + (sx + 0.5) / SS) - ox) / scale
                    uy = ((y + (sy + 0.5) / SS) - oy) / scale
                    hit += inside(poly, ux, uy)
            a = round(255 * hit / (SS * SS))
            px += bytes((*FLAME, a)) if a else b"\0\0\0\0"
    return bytes(px)


def png(size: int, raw: bytes) -> bytes:
    def chunk(tag: bytes, body: bytes) -> bytes:
        return struct.pack(">I", len(body)) + tag + body + struct.pack(">I", zlib.crc32(tag + body) & 0xFFFFFFFF)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def make_ico(path: Path) -> None:
    poly = flame_polygon()
    images = [(s, png(s, render(s, poly))) for s in SIZES]
    head = struct.pack("<HHH", 0, 1, len(images))
    offset = 6 + 16 * len(images)
    entries, blobs = b"", b""
    for s, blob in images:
        entries += struct.pack("<BBBBHHII", s % 256, s % 256, 0, 0, 1, 32, len(blob), offset + len(blobs))
        blobs += blob
    path.write_bytes(head + entries + blobs)


if __name__ == "__main__":
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else HERE / "web" / "ember.ico"
    make_ico(out)
    print(f"wrote {out} ({out.stat().st_size} bytes, sizes {', '.join(map(str, SIZES))})")
