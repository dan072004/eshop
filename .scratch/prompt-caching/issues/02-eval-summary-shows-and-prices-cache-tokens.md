# 02: The eval run summary shows and prices cache tokens

**What to build:** A developer running the evals sees cache-read and cache-write tokens per model in the run summary's token and cost table. The estimated cost prices them at their own rates from the shared Claude price table, so it reflects what the Prompt Cache saves or costs. The evals' token usage record gains the two counts, filled from each chat call's usage the same way input and output are. The judge isn't cached, so its calls report zero. Docs and code keep the evals' **Cached Answers** and Anthropic's **Prompt Cache** under distinct names. See `.scratch/prompt-caching/spec.md` (stories 7, 8, 15).

Remember the adapter's input count already includes cached tokens (assistant-telemetry ticket 02): uncached input is input minus cache reads minus cache writes. Don't price cache tokens twice.

**Blocked by:** None (can start immediately)

**Status:** done

- [x] A fast test next to the existing gate tests: given case results whose usage includes known cache-read and cache-write tokens on a priced model, the summary table shows those counts and the cache-aware estimated cost, written as literals
- [x] Judge usage shows zero cache tokens
- [x] Without `ESHOP_EVALS=1`, the eval project's fast tests still pass with no API key
- [x] Nothing in docs or code calls the Prompt Cache "cached answers" or vice versa
