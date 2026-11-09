# 11: ADR 0006 and the docs that change with it

**What to build:** The decision to call a real model from CI is on record, and the repo's agent docs know the evals exist.

- **ADR 0006, "Evals call a real model, outside the deterministic suite":**
  - **Why:** the behaviour lives in a model we don't own, reacting to our prompt and tools.
  - **What it reverses:** the policies-and-orders rule that CI calls no LLM and needs no key, for the eval project only.
  - **The models:** Haiku 4.5 chats and Sonnet 5.5 judges.
  - **The seam:** real policy-api; Ordering and Catalog canned.
  - **Caching:** cached results are reused evidence, not new samples.
  - **The baseline** from ticket 07.
  - **Rejected options:** everything canned; the whole stack; a Haiku judge.
- **The policies-and-orders spec:** a note on user story 40 that ADR 0006 supersedes it for the eval project.
- **CLAUDE.md:**
  - a Testing-table row for evals (model behaviour with our prompt and tools, tested with a real model in the eval project);
  - the opt-in eval commands in Commands.
- **The `verify` skill:** reports "evals: not run, opt-in" when the diff touches the assistant's paths, and never runs the evals itself.
- **GLOSSARY:** no new terms.

See the evals spec's "Docs".

**Blocked by:** 07 (40 cases and baseline), 09 (CI workflow)

**Status:** ready-for-agent

- [ ] ADR 0006 exists in `docs/adr/`, following the existing ADRs' format, with the measured baseline
- [ ] Story 40's note points to ADR 0006
- [ ] CLAUDE.md's Testing table and Commands mention the evals and how to opt in
- [ ] `verify` on a diff that touches the tools library reports "evals: not run, opt-in"
