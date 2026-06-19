from __future__ import annotations
import struct
from typing import Tuple, Dict, Any

# Message types
STATIC_STATE = 0
DYNAMIC_STATE = 1
OUTCOME = 2
COMMAND = 3
ACTION = 10

# Fixed sizes from your C# code
STATIC_STATE_SIZE = 73
DYNAMIC_STATE_SIZE = 142
ACTION_SIZE = 12  # 4 + 4 + 4


def parse_message(packet: bytes) -> tuple[int, bytes]:
    """
    Split [1 byte type][payload...] into (msg_type, payload).
    """
    if not packet:
        raise ValueError("Empty packet")
    return packet[0], packet[1:]


def encode_action(x: float, y: float, action_id: int) -> bytes:
    """
    Matches C# AiAction serialization:
      float X, float Y, int ActionId
    BinaryWriter writes little-endian.
    """
    return bytes([ACTION]) + struct.pack("<ffi", x, y, action_id)


def encode_outcome(won: bool) -> bytes:
    return bytes([OUTCOME, 1 if won else 0])


def encode_command(cmd: int) -> bytes:
    return bytes([COMMAND, cmd & 0xFF])


def decode_outcome(payload: bytes) -> bool:
    if len(payload) < 1:
        raise ValueError("Outcome payload too short")
    return payload[0] != 0


def decode_command(payload: bytes) -> int:
    if len(payload) < 1:
        raise ValueError("Command payload too short")
    return payload[0]


def decode_static_state(payload: bytes) -> Dict[str, Any]:
    if len(payload) != STATIC_STATE_SIZE:
        raise ValueError(f"StaticState payload must be {STATIC_STATE_SIZE} bytes, got {len(payload)}")

    offset = 0

    def f():
        nonlocal offset
        val = struct.unpack_from("<f", payload, offset)[0]
        offset += 4
        return val

    equipment = {
        "WeaponDamageLight": f(),
        "WeaponDamageHeavy": f(),
        "WeaponRangeLight": f(),
        "WeaponRangeHeavy": f(),
        "WeaponChargeTimeLight": f(),
        "WeaponChargeTimeHeavy": f(),
        "WeaponCooldownLight": f(),
        "WeaponCooldownHeavy": f(),
        "ArmorDamageModifier": f(),
        "ArmorKnockbackModifier": f(),
        "ArmorSpeedModifier": f(),
    }

    # C# BinaryWriter.Write(bool) writes 1 byte
    equipment["HasConsumable"] = struct.unpack_from("<?", payload, offset)[0]
    offset += 1

    equipment["ConsumableEffectDuration"] = f()
    equipment["ConsumableHealAmount"] = f()
    equipment["ConsumableChargeAmount"] = f()

    room = {
        "MinX": f(),
        "MaxX": f(),
        "MinY": f(),
        "MaxY": f(),
    }

    return {"equipment": equipment, "room": room}


def decode_dynamic_state(payload: bytes) -> Dict[str, Any]:
    if len(payload) != DYNAMIC_STATE_SIZE:
        raise ValueError(f"DynamicState payload must be {DYNAMIC_STATE_SIZE} bytes, got {len(payload)}")

    offset = 0

    def f():
        nonlocal offset
        val = struct.unpack_from("<f", payload, offset)[0]
        offset += 4
        return val

    def i32():
        nonlocal offset
        val = struct.unpack_from("<i", payload, offset)[0]
        offset += 4
        return val

    def i16():
        nonlocal offset
        val = struct.unpack_from("<h", payload, offset)[0]
        offset += 2
        return val

    def vec2():
        return (f(), f())

    # PlayerState
    player = {
        "Position": vec2(),
        "MovementDirection": vec2(),
        "CurrentAction": i32(),
        "Health": f(),
        "MaxHealth": f(),
        "CurrentLightAttackCooldown": f(),
        "CurrentHeavyAttackCooldown": f(),
        "CurrentLightAttackChargeTime": f(),
        "CurrentHeavyAttackChargeTime": f(),
        "InvulnerabilityTimeRemaining": f(),
        "DamageTaken": f(),
        "CurrentConsumableChargeTime": f(),
    }

    # BossState
    boss = {
        "Position": vec2(),
        "Health": f(),
        "MaxHealth": f(),
        "AttackCooldowns": tuple(f() for _ in range(4)),
        "AttackCooldownTimers": tuple(f() for _ in range(4)),
        "CurrentAttackId": i16(),
        "InvulnerabilityTimeRemaining": f(),
        "DamageTaken": f(),
        "DistanceToPlayer": f(),
        "AngleToPlayer": f(),
    }

    # ProjectileState
    projectiles = {
        "ActiveProjectileCount": i32(),
        "NearestProjectileDistance": f(),
        "NearestProjectileAngle": f(),
        "NearestProjectileVelocity": vec2(),
        "NearestProjectileDamage": f(),
    }

    return {
        "player": player,
        "boss": boss,
        "projectiles": projectiles,
    }
