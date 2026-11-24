# 09: Policy search for an order misses its item types, so the answer is the generic rule

**What's wrong:** When a Buyer asks about a Policy for one of their orders (e.g. "can I still return the watch from my last order?"), the Shopping Assistant sometimes calls `search_policies` with a generic question that names no item types. The search then finds only the standard rule and misses the type-specific Policy Sections (such as the Navigation Return Window), so the reply is generic or wrong. Seen on 2026-11-23 in a Development trace with `ChatProvider=Claude` on `claude-haiku-4-5`.

The only instruction to name the types is the last sentence of `get_order`'s description (`AssistantTools.GetOrderDescription`, `src/eShop.Assistant.Tools/AssistantTools.cs:43`): "Some Policy rules, such as Return Windows, depend on an item's type: name the types when searching the Policies." It sits in the tool descriptions rather than WebApp's system prompt so MCP clients get it too. But the model has to recall it from another tool's description while writing `search_policies`' free-text `question`, and nothing in `search_policies` (its description or its single `question` parameter) asks for the types.

**Blocked by:** None (can start immediately)

**Status:** done

**To triage:**
- Find out which step fails, from the trace's `execute_tool` arguments (captured in Development): `get_order` never called; called, but the `question` has no types; or the types are there and Policy search still returns the generic rule.
- Measure before changing anything: run `returns-gps-watch-last-order` several times on Haiku (`ESHOP_EVALS=1 ESHOP_EVALS_FRESH=1 ESHOP_EVALS_CASE=returns-gps-watch-last-order dotnet test --project tests/ShoppingAssistant.Evals`) and record the pass rate.
- Choose the fix. Options, smallest first:
  1. Say it in `search_policies`' own description too ("for a question about a specific order, include its items' catalog types"). No contract change.
  2. An optional `itemTypes` parameter on `search_policies` that the server adds to the search. A schema field is filled more reliably than prose in another tool's description, it works the same over MCP, and Policy stays independent of Ordering. It changes the tool contract WebApp and the MCP server share. Proposed: 2 with 1.
  3. An optional `orderNumber` parameter, with the server looking up the item types itself. Most reliable, but Policy search would then depend on Ordering.

- [x] The failing step is identified and recorded in Comments
- [x] Haiku's pass rate on `returns-gps-watch-last-order` is recorded before and after the fix
- [x] The chosen fix works for MCP clients as well as WebApp (nothing that relies only on WebApp's system prompt)
- [x] If the tool contract changes, the MCP server and the assistant tool tests' expected JSON change with it

## Comments

**2026-11-24, triage from the fresh eval run on main (2026-11-23, Haiku, keyword search, `returns-gps-watch-last-order`, 3 samples).** Read from the stored samples' tool calls, so it cost nothing extra:

| Sample | `get_order` called | `search_policies` question | GPS section returned | Passed |
|---|---|---|---|---|
| 1 | yes (order 101) | "What is the return window for Navigation and Jackets products?" | yes, first | yes |
| 2 | yes (order 101) | "What is the return window for products?" | yes, third of five | yes |
| 3 | yes (order 101) | "What is the return window for Navigation and Jackets products?" | yes, first | yes |

- **Failing step:** `get_order` is called, but the `question` sometimes names no types (1 of 3 samples). Policy search isn't at fault: when the types are named, it ranks the Navigation section first.
- **Why the eval passes anyway:** the generic question still matched "Returns Exceptions › GPS watches…" by keyword, lower down the results, and Haiku used it. In the 2026-11-23 Development trace, the generic question returned only the standard rule.
- **Pass rate before the fix:** 3/3. That's too coarse to show whether a fix helps, so measure "types named in the `search_policies` question" across samples as well as pass/fail.
- **Fix chosen:** option 2 with 1. `search_policies` gets an optional `itemTypes` parameter (catalog type names), which the server adds to the search, and its description asks the model to fill it in for a question about a specific order. Measure after the fix the same way: pass rate plus how many samples fill `itemTypes`.

**2026-11-24, implemented option 2 with 1.**

- `search_policies(question, itemTypes?)`: the optional `itemTypes` (catalog type names) are appended to the question before policy-api's search, so policy-api is unchanged and Policy stays independent of Ordering.
- `SearchPoliciesDescription` now ends "For a question about a specific order, pass its items' catalog types as itemTypes." `GetOrderDescription` now ends "…depend on an item's type: pass the types as search_policies' itemTypes."
- The fix lives in the shared tools library, so eShop.Mcp serves the same parameter and descriptions. `McpToolsTests` now checks `search_policies`' parameters (`question`, `itemTypes`) and required list (`question`) separately. `SearchPoliciesToolTests` pins `search_policies`' parameter schema as a JSON literal. Both specs' tool contracts are updated.
- **Pass rate after the fix: not measured yet.** The fresh run of `returns-gps-watch-last-order` failed with `invalid x-api-key` (the `ANTHROPIC_API_KEY` in the shell is rejected; $0 spent). Re-run with a valid key: `ESHOP_EVALS=1 ESHOP_EVALS_FRESH=1 ESHOP_EVALS_CASE=returns-gps-watch-last-order dotnet test --project tests/ShoppingAssistant.Evals`, and record pass rate plus how many samples fill `itemTypes`. Locally the build recorded no `ArtifactsPath`, so `ESHOP_EVALS_STORE` had to be set (e.g. `artifacts/evals-store`).

**2026-11-24, measured after the fix.** Read from the fresh full eval run on main at `f9b6a39` (GitHub Actions run 45408966392, manual run for the prompt-caching ticket; 120 fresh samples, 38/40 cases, $1.10), so it cost nothing extra:

- **Pass rate after the fix:** `returns-gps-watch-last-order` passed (not among the run's failing cases). Before: 3/3.
- **`itemTypes` filled:** 3/3 samples passed `["Navigation", "Jackets"]`. Before the fix, 1 of 3 samples' questions named no types.
- Other order cases fill `itemTypes` too (e.g. `Footwear`, `Cycling`, `Helmets`).
