# Project Brief — Adaptive Boss AI Thesis

**Document version:** 0.3
**Author:** Luigi Turco
**Date created:** 2025-08-20
**Last revised:** 2026-08-16 — updated Sections 5 and 11 to reflect the implemented architecture (see notes inline); everything else unchanged from v0.2.

---

## 1. Title

**RL2 - Reinforcement Learning in Rogue-Likes**


---

## 2. Synopsis (one paragraph)

This project implements and evaluates a reinforcement learning (RL) driven boss for a compact 2D game. The game has a fixed layout of three item rooms and a boss room. The research focus is the boss: a single encounter whose behavior (combat tactics) adapts to different player builds and playstyles using PyTorch‑based Reinforcement Learning. The goal is to demonstrate that an adaptive ML policy can (1) respond to repeated player strategies, (2) counter different archetypes, and (3) produce an alternative paradigm to traditional NPCs AIs.

---

## 3. Research question & hypothesis

**Research question:** Can reinforcement learning be used effectively in the development of a video game, specifically to handle entity behaviours that feel natural and challenging? Is is feasible in terms of time, complexity and resources? 
**Hypothesis:** Reinforcement Learning can be used in the development pipeline of a video game, not requiring a big shift in the default paradigm. Time, resource and complexity requirements are manageable, and the resulting entity behaviours are natural and challenging.

---

## 4. Genre / Scope clarification

* **Genre label:** 2D roguelike‑inspired (fixed map layout).
* **Important note:** The game is *not* a full procedural roguelike — the room layout is fixed (4 item rooms + 1 boss room). What is randomized are the item rewards and the boss's learned behavior. This fixed layout reduces scope while keeping stochastic variation by randomized rewards and adaptive boss AI.

---

## 5. Objectives (SMART)

1. **Implement a compact playable game loop** (2D arena with 3 item rooms + boss room) and a boss.
2. **Design and implement a Gym‑like boss environment wrapper** with an observation vector and a multi‑discrete action space.
3. **Train DRL boss policies (PPO in PyTorch)** against scripted player archetypes and mixed opponents. *(Implemented differently than originally planned: no ONNX export. A single shared policy trains centrally across multiple concurrent Godot instances orchestrated by the training server, checkpointed as raw PyTorch `.pt` files rather than exported.)*
4. **Integrate trained policies into the game engine** (Godot) for in‑engine playtesting. *(Implemented as a live client–server bridge, not embedded in‑engine inference — see Section 11. No rule‑based fallback boss was built; the boss is always the ML policy.)*
5. **Run reproducible experiments** (fixed seeds) and report metrics: win rate, encounter duration, action entropy, and adaptation curves.
6. **Produce thesis artifacts**: reproducible code/data/models, figures, playtest videos, and written analysis defending or refuting the hypothesis.

---

## 6. Minimum Viable Product (frozen for thesis)

* **Game map:** fixed sequence of 3 item rooms and one boss room.
* **Items available (per room):** a curated set derived from these classes: Dagger (melee), Longsword (melee), Bow (ranged), Magic Staff (ranged). Elements: *Idea scrapped to avoid domain explosion*  Consumables: Potion (slow hp regeneration), Medkit(instant healing). Armors: No armor(default stats), Light Armor (low defense, high mobility), Medium Armor (slightly higher defence, slightly lower mobility), Heavy Armor (high defense, low mobility).
* **Boss:** single boss encounter with ML policy.
* **Training:** PyTorch PPO; logs and model registry.
* **Evaluation:** scripted player archetypes, composed from independent Attack/Dodge/Aggro behavior modules (e.g. `AttackSpam`, `AttackTactical`, `AttackCowardly`, `AttackEdgelord`; `DodgePreemptive`, `DodgeReactive`, `DodgeRandom`, `DodgeNever`) and mixed/randomized per training episode — a more granular system than the original melee/tank/kiter split, giving finer control over which player skill dimension is being tested.

> **MVP freeze rule:** items or mechanics outside the list above must be added only via an explicit scope change story.

---

## 7. Success criteria / Metrics

* **Primary metric:** Win rate of ML boss vs. each player archetype (mean ± std over N seeds and M evaluation episodes).
* **Secondary metrics:** mean encounter duration, action entropy, consumable usage rate, and time‑to‑adaptation curve (episodes-to-improvement against a repeated player build).
* **Qualitative evidence:** playtest notes, video clips showing emergent counterplay or strategy shifts.
* **Reproducibility:** a documented experiment that reproduces at least one published result (figure) using provided Dockerfile/scripts.

---

## 8. Key deliverables

1. Playable game prototype with four item rooms + boss room (engine build).
2. Gym‑style boss environment and training scripts in PyTorch.
3. Trained policy artifacts (ONNX) and model registry metadata.
4. Evaluation scripts and experiment reports (figures + tables).
5. Playtest questionnaires.
6. Thesis write‑up and reproducibility package.

---

## 9. High‑level timeline

* **Month 0 (setup):** scaffold repo, stack docs, MVP item freeze.
* **Weeks 1–4 (foundation):** baseline playable boss + item rooms + logging.
* **Month 5 (env):** Gym wrapper, observation/action mapping, scripted archetypes.
* **Month 6 (ML prototyping):** PPO training vs single archetype → mixed opponents, small experiments.
* **Month 6-7 (integration):** export ONNX, integrate in engine, hot‑swap, playtesting.
* **Month 7 (experiments):** run final experiments, collect metrics, produce figures.
* **Month 7 (write‑up & polish):** thesis writing, reproducibility checks, final playtests.

---

## 10. Risks & Mitigations (top 5)

1. **Risk:** Training takes too long / compute bottleneck.
   **Mitigation:** use small models, vectorized envs, frame‑skip, limit obs/action complexity.
2. **Risk:** RL policy exploits game physics / produces degenerate behaviour.
   **Mitigation:** implement hard guardrails in environment, use reward penalties for illegal/out‑of‑bounds behavior, run randomized seeds.
3. **Risk:** Scope creep (adding too many items/mechanics).
   **Mitigation:** keep MVP freeze; any additions require explicit scope change story.
4. **Risk:** Non‑reproducible experiments.
   **Mitigation:** log seeds, env configs, package versions, and provide Dockerfile.
5. **Risk:** Limited time for thesis writing and experiments.
   **Mitigation:** prioritize experiments that directly support or invalidate the hypothesis; automate evaluation and logging.

---

## 11. Resources & Tech Stack (as implemented)

* **Engine:** Godot 4.6 (Mono build, C#/.NET) — the Unity/Godot choice from v0.2 is resolved; Unity was not used.
* **Training:** PyTorch, custom PPO with GAE (4 epochs, minibatch 64, lr=3e-4). A single policy trains centrally: the training server can spawn and coordinate multiple concurrent Godot instances via a `TrainingOrchestrator`, pooling their experience into one shared model rather than training per-instance.
* **Model architecture:** 56‑dim observation → shared MLP trunk (256→256→128) → three heads: continuous movement (Gaussian, learned std, tanh‑squashed) + discrete action (7‑way categorical) + value. Checkpointed as raw PyTorch `.pt` files (`Game/server/checkpoints/`, promoted models in `Game/server/policies/`) — no ONNX export in the pipeline.
* **Game ↔ AI integration:** *not* embedded in‑engine inference. The Godot client and a separate Python process communicate live over a WebSocket (`ws://localhost:7000`) using a custom fixed‑size binary protocol (`STATIC_STATE` once per episode, `DYNAMIC_STATE` every frame, `ACTION` responses). For distribution, the inference server is packaged into a standalone executable (PyInstaller, CPU‑only) that the exported game auto‑launches as a companion process — end users don't need Python installed.
* **Experiment tracking:** local JSON metrics logs (no Weights & Biases integration currently).
* **Versioning:** git. Trained checkpoints are not currently in Git LFS — worth revisiting given `.pt` file sizes.
* **Reproducibility:** `requirements.txt` exists (`Game/server/requirements.txt`); no Dockerfile yet — outstanding relative to the v0.2 plan.

---

## 12. Assumptions

* The boss fight can be abstracted into a Gym‑style environment amenable to RL training.
* The limited set of items and fixed room layout provides enough behavioral variety for meaningful ML adaptation.
* Training resources (local GPU or CPU) will be available for prototyping and final runs.

---

## 13. Definitions (short)

* **Archetype:** a scripted player style (e.g., melee spammer).
* **NPC:** Non Playable Character.
* **Episode:** a single boss encounter run from entering boss room to win/lose or timeout.
* **Policy:** trained ML agent (boss).
