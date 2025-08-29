# AGENTS.md — Guidance for OpenAI Codex on TactForge game (game logic scripts-only repo)

## Context
This repository contains only the game logic (C# Scripts) for TactForge (Unity 6.2 project), not a full Unity project. Treat this repo as a **source package** that will be copied into a Unity project at build time.

Key tech used in this codebase:
- Unity DOTS / Entities 1.3+
- Hybrid approach: MonoBehaviours + ECS bridge (e.g., `UnitBrain`)
- TopDown Engine from MoreMountains (forked TopDown logic into lightweight EnigmaEngine), Behavior Designer PRO, Agents Navigation (Project Dawn)
- GPU Instancer PRO (+ Crowd Animations PRO)
- ScriptableObject definitions

## What you MAY do
- Pure C# edits inside **this repo** only.
- Perform refactors refactors (split classes, extract methods, rename with safety).
- Introduce new systems, components, or ScriptableObjects
- Consolidate duplicated or legacy logic into cleaner structure.
- Rename methods/fields/classes for naming conventions consistency

## What you MUST NOT do
- Do **not** edit or add Unity `ProjectSettings/`, `Packages/`, or `manifest.json`.
- Do **not** add or bump Unity packages, assets, or third-party dependencies.
- Do **not** assume this repo compiles standalone.
- Do **not** add unit tests (pre-production phase).

## Repo Layout
- ECS: `/ECS/**`
- Definitions: `Definition/**` (ScriptableObjects)
- AI/Units: `AI/**`

## Style & Conventions
- Follow existing namespaces (`OneBitRob`, `OneBitRob.ECS`, `OneBitRob.AI`, `OneBitRob.EnigmaEngine`).
- Prefer **small, composable classes**.
- Unity ECS best practices: no GC allocs in hot systems.
- Private fields → `_camelCase`.
- Public/protected fields → `PascalCase`.
- Comments only when logic is non-obvious.

## TactForge-specific guardrails
- **UnitBrain (Mono ↔ ECS bridge):**  
  New features should enter ECS via components/buffers, not deep Mono calls.
- **Spawner / Target subsystems:**  
  Keep logic data-oriented; avoid hidden Mono side effects.
- **Performance:**
  I want to have possibility to have 200-300 UnitBrains on scene, so performance is not main, but important topic.

## Linting / Validation (best-effort)
- Ensure naming consistency (`PascalCase`, `_camelCase`).
- Avoid methods > 100 lines unless necessary (some methods with long switch() is OK).
- No unreferenced fields/methods.
- Suggest Odin Inspector attributes for ScriptableObjects (e.g., `[BoxGroup]`).

## Pull Request Requirements
- **Large diffs are acceptable** (500+ lines) if scoped to a subsystem.
- PR description must include:
  - **What changed** and **why**.
  - **Risks / assumptions** (e.g., "affects UnitBrain targeting").
  - **Migration notes** (search/replace guidance for renamed APIs).


## Safe Task Examples

### ✅ Refactor / Cleanup
- Split `UnitBrain.cs` into partials (`UnitBrain.Targeting`, `UnitBrain.Navigation`).
- Remove unused legacy code from `SpellDefinition.cs`.
- Normalize subsystem names (`*Subsystem` → `*System`).

### ✅ Feature Additions
- Add `WeaponAttackSystem` ECS system with pooled hit feedbacks.
- Add `RaceSynergyDefinition` ScriptableObject to drive buffs.
- Implement `RangedAttackSystem` connected via `UnitBrain`.

### ✅ Migrations
- Replace direct spell Mono calls with ECS `SpellCastRequest`.
- Centralize pooling into `FeedbackPoolManager`.
- Migrate `TargetSubsystem` from Mono overlap checks to ECS spatial hash.  