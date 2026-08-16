# Gameplay & Interaction Document

**Project:** Adaptive Boss AI Thesis Game
**Author:** Luigi Turco
**Version:** 0.3
**Date:** 2025-08-24
**Last revised:** 2026-08-16 — corrected player Max HP (Section 3) and the ML integration description (Section 11) to match the implemented game; everything else unchanged from v0.2 and not independently re-verified against current content/balance.

---

## 1. High-Level Concept

A compact 2D roguelike-inspired game designed to test adaptive boss AI. The player progresses through four fixed rooms (Weapon, Armor, Heal, Spawning) before facing a dynamically adaptive Boss. Rewards in the first three rooms are fixed, while basic non-AI enemies provide light combat encounters. The Boss uses reinforcement learning to adapt its behavior based on the player’s build and playstyle.

---

## 2. Core Gameplay Loop

1. **Start Game** → Player begins with a longsword and no armor.
2. **Room Progression:**

   * **Weapon Room:** Defeat basic enemies → choose one weapon reward.
   * **Armor Room:** Defeat basic enemies → choose one armor reward.
   * **Heal Room:** Defeat basic enemies → receive one consumable reward.
   * **Spawning Room:** Narrative/transition room (no combat, sets up boss).
3. **Boss Room:** Player faces the adaptive boss. Boss behavior changes according to player build and past actions.
4. **End:** Victory → session summary. Defeat → retry.

---

## 3. Player Character Data

### Core Stats (base values)

* **Max HP:** 20 *(corrected from v0.2's 100 — matches `MAX_PLAYER_HP` in the reward/observation code. Boss Max HP is 2000, a deliberate ~100:1 scale gap, not a symmetric fight.)*
* **Move Speed:** 100% (base)
* **Attack Power:** depends on equipped weapon (see Combat System)
* **Defense:** provided by armor (see mitigation below)

### Health & Recovery

* **Potion (Instant Heal):** restores **25%** of Max HP instantly; **10s** personal cooldown.
* **Medkit (Regeneration):** restores **45%** of Max HP over **6s** (ticks every 0.5s); cannot stack (re‑use refreshes duration); **15s** personal cooldown.
* **Carry limit:** player has **1 consumable slot** with maximum **2** uses; taking a new consumable **replaces** the current one.
* **Overheal:** not allowed; HP clamps at Max HP.

### Armor & Mitigation

*(Corrected 2026-08-16 against `Armor.cs` and the armor scene resources — values are direct multipliers. `DamageModifier`/`KnockbackModifier` above ×1.0 mean **more** damage/knockback taken, not less; `SpeedModifier` above ×1.0 means faster.)*

* **None** (`shirt.tscn`): Speed ×1.0, Damage taken ×1.0, Knockback ×1.0 — no modifiers.
* **Light:** Speed ×1.25 (+25%), Damage taken ×1.5 (+50%, a penalty), Knockback ×1.2 (+20%). A glass‑cannon tradeoff, not a strict upgrade over None — faster, but meaningfully more fragile.
* **Medium:** Speed ×0.85 (−15%), Damage taken ×0.5 (−50%), Knockback ×0.8 (−20%, the class default — not explicitly overridden in `medium_armor.tscn`).
* **Heavy:** Speed ×0.65 (−35%), Damage taken ×0.25 (−75%), Knockback ×0.4 (−60%).
### Equipment & Inventory Rules

* **Weapons:** Longsword (starter), Dagger, Bow, Magic Staff — **1 equipped**; Weapon Room allows **swap**.
* **Armor:** None (starter), Light, Medium, Heavy — **1 equipped**; Armor Room allows **swap**.
* **Consumable:** Potion **or** Medkit — **1 slot**; Heal Room grants one and replaces current if taken.

---

## 4. Player Actions

* **Movement:** 8‑directional (WASD).
* **Primary Attack:** weapon‑specific (melee swing, bow shot, staff cast).
* **Secondary Attack:** weapon‑specific heavy/charged variant; each weapon may have an internal cooldown.
* **Dodge (Dash):** short burst with **0.25s** i‑frames; distance \~**3 tiles** (tunable).
* **Use Consumable:** immediate activation; respects personal cooldown and slot rules above.

**Notes**

* No ammo/mana systems in MVP; **cooldowns** regulate attack/ability frequency.
* Hitstun/knockback: knockback on hits (tunable).

---

## 5. Enemy Design

### Basic Non-AI Enemies (rooms before boss)

* **Melee Minion:** runs at player, basic melee attack.
* **Ranged Minion:** stands at distance, fires simple projectiles.

Enemies are deliberately simple (scripted behavior, no adaptation). Their role is to provide challenge before rewards.

### Boss Enemy

* **Behavior Variables:** dodging/parrying, aggression, distance from player, attack frequency, attack choice.
* **Adaptation:** Reinforcement learning (PPO in PyTorch) trained against archetypal player strategies.

---

## 6. Room Breakdown

* **Weapon Room:**

  * Contains 5 basic enemies.
  * On victory → choose one weapon reward (dagger, bow, staff; cannot re-pick longsword).

* **Armor Room:**

  * Contains 5 basic enemies.
  * On victory → choose one armor reward (light, medium, heavy).

* **Heal Room:**

  * Contains 5 basic enemies.
  * On victory → choose one consumable (potion or medkit).

* **Spawning Room:**

  * Narrative/atmospheric, no combat.
  * Prepares player for boss (visual buildup).

* **Boss Room:**

  * One adaptive boss.
  * End of run on victory or defeat.

---

## 7. Combat System Overview

* **Weapons:**

  * Longsword:
    * Primary attack: snap cut, medium damage, medium speed.
    * Secondary attack: charged cut, high damage, slow speed.
  * Dagger:
    * Primary attack: cut, low damage, high speed.
    * Secondary attack: stab, high damage, medium speed.
  * Bow:
    * Primary attack: shoot, medium damage, speed variable based on charge.
    * Secondary attack: spread attack, low damage, high speed, multiple projectiles in spread pattern.
  * Staff:
    * Primary attack: cast missile, low damage, low speed.
    * Secondary attack: cast flaming sphere, very high damage, very low speed, area damage, higher cooldown.

* **Armor:** modifies movement speed and defense.

---

## 8. Progression & Replayability

* **Boss behavior changes** between runs due to adaptive ML policy.
* **Short sessions** allow rapid iteration for testing and player adaptation.

---

## 9. Controls (Default)

* **Move:** WASD
* **Primary Attack:** Left Mouse
* **Secondary Attack:** Right Mouse
* **Dodge:** Spacebar
* **Use Consumable:** Q

---

## 10. Game Flow Diagram (textual)

Start → Weapon Room (fight → reward) → Armor Room (fight → reward) → Heal Room (fight → reward) → Spawning Room (transition) → Boss Room (adaptive fight) → End (victory/defeat)

---

## 11. Technical Assumptions

* **2D top-down** perspective.
* **Engine:** Godot 4.6 (Mono/C#).
* **ML Integration:** *not* ONNX/embedded inference as originally planned. The trained PyTorch policy runs in a separate Python process; the Godot client talks to it live over a WebSocket (custom binary protocol) every frame. For a shipped build, that process is packaged into a standalone executable (PyInstaller) and auto-launched by the game as a companion process, so players don't need Python installed.
* **Enemy AI:** simple scripted for normal enemies; DRL for boss.

---

## 12. Win/Lose Conditions

* **Win:** Defeat boss.
* **Lose:** Player HP reaches 0.

---

## 13. Out of Scope (for MVP)

* Procedural room generation.
* More than 4 weapons/armors.
* Multiplayer or co-op.
* Complex consumables or traps.

---

**End of GDD Draft**
