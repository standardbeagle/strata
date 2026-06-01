# Strata — Repo Conventions

## Version control

- **Commit on coherent work completed.** When a logical unit of work is done and green
  (compiles, tests pass), commit it — no need to wait to be asked. Keep commits scoped to
  one coherent change; don't bundle unrelated work.
- **Push on a meaningful integration point.** Push when the local branch reaches a state worth
  sharing — a feature set complete, a phase landed, a fix verified end-to-end — not after every
  commit. The default branch is `main`; remote is `origin`.
- Before committing: review the diff for debug code, secrets, and build artifacts. All tests
  must pass — no exceptions for pre-existing failures.
- Use conventional-commit subjects (`feat:`, `fix:`, `chore:`, `docs:`, etc.).
