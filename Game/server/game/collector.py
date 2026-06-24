"""
Observation flattening for neural network input.

Converts binary game state (equipment, room, player, boss, projectiles)
into normalized feature vectors suitable for policy network.
"""

from __future__ import annotations

from typing import Any
import math
import torch

from utils.normalization import normalize_range, normalize_01, angle_to_sin_cos


def flatten_observation(static_state: dict[str, Any], dynamic_state: dict[str, Any]) -> torch.Tensor:
    """
    Convert static and dynamic game state into a normalized feature vector.
    
    Args:
        static_state: Equipment and room bounds (sent once per session)
        dynamic_state: Player, boss, projectile state (sent every frame)
    
    Returns:
        Normalized feature tensor (62 dims)
    """
    features: list[float] = []

    e = static_state["equipment"]
    r = static_state["room"]

    p = dynamic_state["player"]
    b = dynamic_state["boss"]
    pr = dynamic_state["projectiles"]

    # Static room bounds
    min_x = r["MinX"]
    max_x = r["MaxX"]
    min_y = r["MinY"]
    max_y = r["MaxY"]

    room_width = max_x - min_x
    room_height = max_y - min_y
    room_diag = math.sqrt(room_width * room_width + room_height * room_height)
    if room_diag <= 0.0:
        room_diag = 1.0

    # Static features
    features.extend([
        normalize_01(e["WeaponDamageLight"], 100.0),
        normalize_01(e["WeaponDamageHeavy"], 100.0),
        normalize_01(e["WeaponRangeLight"], 1000.0),
        normalize_01(e["WeaponRangeHeavy"], 1000.0),
        normalize_01(e["WeaponChargeTimeLight"], 5.0),
        normalize_01(e["WeaponChargeTimeHeavy"], 5.0),
        normalize_01(e["WeaponCooldownLight"], 5),
        normalize_01(e["WeaponCooldownHeavy"], 5),
        normalize_range(e["ArmorDamageModifier"], 0.25, 1.5),
        normalize_01(e["ArmorKnockbackModifier"], 0.4, 1.2),
        normalize_01(e["ArmorSpeedModifier"], 0.65, 1.25),
        1.0 if e["HasConsumable"] else 0.0,
        normalize_01(e["ConsumableEffectDuration"], 30.0),
        normalize_01(e["ConsumableHealAmount"], 100.0),
        normalize_01(e["ConsumableChargeAmount"], 100.0),

        -1.0,  # room min/max are already in normalized features below
        1.0,
        -1.0,
        1.0,
    ])

    # Player position and motion
    px, py = p["Position"]
    mx, my = p["MovementDirection"]

    features.extend([
        normalize_range(px, min_x, max_x),
        normalize_range(py, min_y, max_y),
        mx,  # movement direction is already normalised
        my,
    ])

    # Player combat state
    features.extend([
        normalize_01(float(p["CurrentAction"]), 9.0),
        normalize_01(p["Health"], max(p["MaxHealth"])),
        normalize_01(p["CurrentLightAttackCooldown"], 5.0),
        normalize_01(p["CurrentHeavyAttackCooldown"], 5.0),
        normalize_01(p["CurrentLightAttackChargeTime"], 5.0),
        normalize_01(p["CurrentHeavyAttackChargeTime"], 5.0),
        normalize_01(p["InvulnerabilityTimeRemaining"], 2),
        normalize_01(p["DamageTaken"], 100.0),
        normalize_01(p["CurrentConsumableChargeTime"], 10.0),
    ])

    # Boss position and combat state
    bx, by = b["Position"]
    attack_sin, attack_cos = angle_to_sin_cos(b["AngleToPlayer"])

    features.extend([
        normalize_range(bx, min_x, max_x),
        normalize_range(by, min_y, max_y),
        normalize_01(b["Health"], max(b["MaxHealth"])),
    ])

    for cd in b["AttackCooldowns"]:
        features.append(normalize_01(cd, 1.0))

    for t in b["AttackCooldownTimers"]:
        features.append(normalize_01(t, 1.0))

    features.extend([
        normalize_range(float(b["CurrentAttackId"]), -1, 4.0),
        normalize_01(b["InvulnerabilityTimeRemaining"], 2.0),
        normalize_01(b["DamageTaken"], 100.0),
        normalize_01(b["DistanceToPlayer"], room_diag),
        attack_sin,
        attack_cos,
    ])

    # Projectile state
    pvx, pvy = pr["NearestProjectileVelocity"]

    features.extend([
        normalize_01(float(pr["ActiveProjectileCount"]), 50.0),
        normalize_01(pr["NearestProjectileDistance"], room_diag),
        angle_to_sin_cos(pr["NearestProjectileAngle"])[0],
        angle_to_sin_cos(pr["NearestProjectileAngle"])[1],
        pvx,
        pvy,
        normalize_01(pr["NearestProjectileDamage"], 100.0),
    ])

    return torch.tensor(features, dtype=torch.float32)
