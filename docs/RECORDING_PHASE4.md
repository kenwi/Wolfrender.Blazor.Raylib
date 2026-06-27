# Recording system - Phase 4 (determinism and polish)

Phases 1-3 deliver input capture, local `.rec` files, and console-driven replay. Phase 4 makes replays reliable for regression testing and competitive verification.

## Goals

- Same `.rec` file produces the same outcome on repeated replay (path, kills, score)
- Replays stay faithful when frame pacing is close but not identical
- Clear UX when recording or replaying
- Optional server-side storage and retrieval (see recording upload API)

## 1. Fixed simulation tick

**Problem:** Events are timestamped with wall-clock `deltaTime`. Variable frame times shift when key down/up and mouse deltas are applied.

**Approach (Quake-style):**

```
accumulator += deltaTime;
while (accumulator >= FixedDt) {
    PollInputForTick();
    SimulateGameplay(FixedDt);
    accumulator -= FixedDt;
}
```

- Use a fixed step (e.g. 1/60 s or 1/125 s)
- Record events against **tick index** (primary) and optionally wall time (debug)
- Replay applies events per tick, not per render frame
- Render can interpolate or run uncoupled from sim

**Files:** `World.Update`, `RecordingSystem`, `InputRecorder`, `ReplayInputProvider`, `.rec` format v3 header (`tickHz`, `tickCount`)

## 2. RNG seeding

**Problem:** `EnemySystem` uses an unseeded `Random`. Identical inputs can yield different enemy behavior.

**Approach:**

- Seed RNG at level load from level path hash or explicit seed in level JSON
- Store `rngSeed` in `.rec` header
- On replay, re-seed before first sim tick

**Files:** `EnemySystem`, `RecFile`, `World.ResetLevelState`

## 3. Route remaining live input bypasses

**Problem:** Some code still polls Raylib directly during gameplay.

**Audit and fix:**

- `PlayerSystem` restart keys (`R`, click) - ignore during replay or record as events
- `EnemySystem` debug `C` key - exclude from recording (already excluded)
- Any new gameplay `IsKeyPressed` usage must go through `InputState` from `IInputProvider`

## 4. Validation and versioning

- Reject unknown `.rec` versions with a clear console message
- Warn when engine/build differs from recorded metadata (optional `engineVersion` in header)
- Validate level path exists before replay
- Max recording duration / event count limits for uploads

## 5. UX polish

- HUD indicator: `REC` while recording, `REPLAY` while playing back
- Block or pause replay when options menu opens (consistent behavior)
- `demo` alias for `replay` (Quake convention)
- Auto-stop recording on level load / player death (configurable)

## 6. Stretch

- Seek / scrub to timestamp in replay
- Multi-level demo chains
- Event stream compression (run-length held keys)
- Spectator camera during replay
- Leaderboard integration: verify score by replaying uploaded demo

## Suggested implementation order

1. Fixed tick accumulator in `World` + record/replay by tick index
2. RNG seed in header and level load
3. HUD indicators and replay UX
4. Validation hardening
5. Stretch items as needed

## Acceptance criteria

| Test | Expected |
|------|----------|
| Record and replay same session 5x at locked 60 FPS | Identical player path and final score |
| Record at 60 FPS, replay at 120 FPS (after fixed tick) | Same outcome |
| Change enemy HP in code, replay old `.rec` | Same inputs; outcome may differ (input-only demos) |
| Upload `.rec` via `sendrecording` | Server stores and validates file |
