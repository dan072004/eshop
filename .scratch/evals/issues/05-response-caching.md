# 05: Response caching

**What to build:** Rerunning unchanged cases costs nothing, and any change that matters forces a fresh answer.

- **Chat turns** are cached by the caching client the eval passes into WebApp's pipeline builder, below function invocation, so each model turn is cached.
- **Judge calls** are cached through the Reporting chat configuration.
- **Storage:** both live in the results store.
- **The key:** the Reporting framework's key (messages, options, scenario, iteration, provider, model id), plus a hash of:
  - every tool's name, description and parameter schema, because tool definitions are not in the default key;
  - the case file;
  - the world file.
- **Expiry:** 14 days.
- **Fresh samples:** `ESHOP_EVALS_FRESH=1` bypasses the cache for both chat and judge.
- **The summary** (ticket 06) counts cached and fresh samples, so this ticket records each sample's origin.

See the evals spec's "Caching".

**Blocked by:** 04 (judge)

**Status:** ready-for-agent

- [ ] A second run of the acceptance case makes no model calls and gives the same verdicts
- [ ] Changing one tool's description, with nothing else changed, makes the next run call the model again
- [ ] Changing the case or the world file makes the next run call the model again
- [ ] `ESHOP_EVALS_FRESH=1` calls the model even with a warm cache
- [ ] Each sample's result records whether it came from cache
