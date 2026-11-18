# Development rules

- Work in this independent `moelotl-expansion` repository.
- Implement and push incremental milestones on `dev`.
- Do not merge into, commit directly to, or push changes to `main` without the user explicitly authorizing that action.
- Treat `萌螈丹药扩展设计_v0.1.md` as the accepted design; its current sections override historical alternatives.
- Keep all automatic medicine use, renewal, emergency use, and restocking disabled initially; restock quantities default to zero.
- Query runtime Def snapshots with RimSearcher and verify external C# APIs with DecompilerServer before integrating.
- Do not commit game/mod dependency binaries, runtime databases, private saves, or local absolute-path configuration.
- Build and validate each implemented milestone. Distinguish compilation/static validation from an actual in-game test.
- Update `docs/IMPLEMENTATION.md` with completed features, checks, and remaining stages.
