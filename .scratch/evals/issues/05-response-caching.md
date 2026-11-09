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

**Status:** done

- [x] A second run of the acceptance case makes no model calls and gives the same verdicts
- [x] Changing one tool's description, with nothing else changed, makes the next run call the model again
- [x] Changing the case or the world file makes the next run call the model again
- [x] `ESHOP_EVALS_FRESH=1` calls the model even with a warm cache
- [x] Each sample's result records whether it came from cache

## Comments

- 2026-11-09, how it's built:
  - **Chat turns:** `CachedModel` (`Running/ResponseCache.cs`) opens a Reporting scenario run per case and iteration whose chat configuration is the Anthropic model, and hands its caching client to `ClaudeChatPipeline.Build` as the inner client. Each model turn below function invocation is one cache entry under `<store>/cache/<case id>/<iteration>/`. That run is never evaluated, so it writes no result. A counter between the caching client and the model counts the calls that reach the model.
  - **Judge calls:** `EvalReporting` now builds its `ReportingConfiguration` with the same disk cache (14-day TTL) for both the cases and the calibration set, so the calibration set is cached too.
  - **Keys** (`Running/CachingKeys.cs`), added to Reporting's key:
    - `tools:` hashes every tool's name, description and JSON schema, in the order the conversation class sends them. `CaseRunner` reads them off the conversation class's first request with a probe client, so no copy of the tool list can drift.
    - `case:` hashes this case's own entry in `cases.json`, as written, rather than the whole file. Editing one case resamples only that case, which is what spec stories 23 and 25 ask for (with 40 cases, hashing the whole file would resample every case after any edit).
    - `world:` hashes the whole world file.
    - `judge:` (`CachingKeys.Judge`, for the judge's scenario runs only) hashes the judge's model, effort, output limit and `RubricEvaluator.Instructions`, which is now public. The effort and output limit are applied inside `JudgeClient`, below Reporting's cache. The case's chat keys are added to the judge's run too.
  - **Fresh:** with `ESHOP_EVALS_FRESH=1`, every cache read misses but writes still land (`ResponseCache.Provider`). A fresh run replaces the cached answers, so the next normal run replays the fresh ones rather than the older ones.
  - **Origin, for ticket 06:** each case's stored result has the tag `sample:cached` (no chat turn reached the model) or `sample:fresh` (`SampleOrigin`, `CaseTranscript.ModelCalls`/`OriginTag`). The judge's own cache hit is Reporting's `chatDetails.turnDetails[].cacheHit`. `CaseRunner.RunAsync` takes an iteration name (default `"1"`), so ticket 06's 3 samples are cached separately.
- 2026-11-09, live runs of the acceptance case (fresh store, 6 runs; each fresh run made 4 chat calls and 1 judge call):
  1. Cold: `sample:fresh`, judge `cacheHit=false`.
  2. Warm, nothing changed: `sample:cached`, 0 model calls, judge `cacheHit=true`, the same 7 verdicts as run 1.
  3. `list_my_orders`' description changed by one word (reverted): `sample:fresh`, 4 model calls, judge `cacheHit=false`.
  4. Case changed (`"203"` added to `mustNotContain`, which doesn't change the conversation; reverted): `sample:fresh`.
  5. World changed (bob's order 201 re-dated, which alice's conversation never sees; reverted): `sample:fresh`.
  6. `ESHOP_EVALS_FRESH=1` with a warm cache: `sample:fresh`, judge `cacheHit=false`.
- Sampled misses, left as they are: in run 1 (replayed in run 2), the answer cited `returns-exceptions › …` and `returns-policy › …` in lower case, so Citations present and Citations grounded failed. In run 4 the judge failed rubric item 2. Runs 3, 5 and 6 passed everything. That variance is what ticket 06's 3 samples are for.
- With xUnit v3 on Microsoft.Testing.Platform, `--filter "FullyQualifiedName~X"` runs zero tests. Use `--filter-class "*X"` instead.
