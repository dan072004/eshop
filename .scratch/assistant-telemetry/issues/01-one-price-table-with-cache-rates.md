# 01: One price table for WebApp and the evals, with cache rates

**What to build:** Anthropic's list prices live in one table in WebApp, next to the Claude chat pipeline, and the evals' run summary uses it from there. The table prices input, output, cache-read and cache-write tokens, so the Shopping Assistant's dashboard (ticket 03) and the evals can never disagree about what a call cost. The eval run summary shows the same numbers it does today. It keeps pricing cache tokens as zero until the prompt-caching feature adds them to the evals' usage record.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] The table lists `claude-haiku-4-5` and the evals' judge model, keyed by model id prefix, with input and output prices per million tokens
- [ ] Cache reads are priced at 0.1× and cache writes at 1.25× the input price (the 5-minute cache lifetime). A comment records that assumption and the date the prices were taken
- [ ] A model with no listed price has no cost (not $0)
- [ ] The evals' own copy of the table is deleted; the run summary uses the shared one, and the existing run summary test passes unchanged
- [ ] A plain fast test covers input, output, cache-read and cache-write pricing and an unlisted model, with expected costs as literals
