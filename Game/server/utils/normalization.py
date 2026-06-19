from __future__ import annotations

import math


def clamp(x: float, lo: float, hi: float) -> float:
    return max(lo, min(hi, x))


def normalize_range(value: float, min_val: float, max_val: float) -> float:
    if max_val == min_val:
        return 0.0
    x = 2.0 * (value - min_val) / (max_val - min_val) - 1.0
    return clamp(x, -1.0, 1.0)


def normalize_01(value: float, max_val: float) -> float:
    if max_val == 0:
        return 0.0
    return clamp(value / max_val, 0.0, 1.0)


def angle_to_sin_cos(angle: float) -> tuple[float, float]:
    return math.sin(angle), math.cos(angle)