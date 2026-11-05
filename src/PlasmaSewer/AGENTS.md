# Plasma sewer event

- This is an independent additive mod. Preserve the base game, all other mods, and existing saves; never stop the game without task-specific permission.
- Affect only the sewer faucet, including both gutterflow and the later rustwater upgrade; preserve vanilla source priority, filters, container capacity and pre-existing contents.
- Event timing is deterministic per run and in-game day; identical saved runs must not reroll on reload. Contamination already collected remains after the tap recovers.
- Represent toxic material using the native zyanide liquid plus a namespaced visual marker, so volume, toxicity and filtering remain recognized even if the mod is disabled. Preserve markers across fluid transfer.
- FX must not block pointer input, must be scoped to the faucet, and disappear after the event; preserve other visual state.
- Validate timing, source isolation, capacity, mixtures, filtering and reload with focused tests. Distinguish stub simulations, actual game loading, visual acceptance and live toxic effects.
- Do not put personal game saves or game binaries in a public release. Keep rollback sources before installation.
