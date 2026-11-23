# 09: Policy search for an order misses its item types, so the answer is the generic rule

**What's wrong:** When a Buyer asks about a Policy for one of their orders (e.g. "can I still return the watch from my last order?"), the Shopping Assistant sometimes calls `search_policies` with a generic question that names no item types. The search then finds only the standard rule and misses the type-specific Policy Sections (such as the Navigation Return Window), so the reply is generic or wrong. Seen on 2026-11-23 in a Development trace with `ChatProvider=Claude` on `claude-haiku-4-5`.

The only instruction to name the types is the last sentence of `get_order`'s description (`AssistantTools.GetOrderDescription`, `src/eShop.Assistant.Tools/AssistantTools.cs:43`): "Some Policy rules, such as Return Windows, depend on an item's type: name the types when searching the Policies." It sits in the tool descriptions rather than WebApp's system prompt so MCP clients get it too. But the model has to recall it from another tool's description while writing `search_policies`' free-text `question`, and nothing in `search_policies` (its description or its single `question` parameter) asks for the types.

**Blocked by:** None (can start immediately)

**Status:** needs-triage

**To triage:**
- Find out which step fails, from the trace's `execute_tool` arguments (captured in Development): `get_order` never called; called, but the `question` has no types; or the types are there and Policy search still returns the generic rule.
- Measure before changing anything: run `returns-gps-watch-last-order` several times on Haiku (`ESHOP_EVALS=1 ESHOP_EVALS_FRESH=1 ESHOP_EVALS_CASE=returns-gps-watch-last-order dotnet test --project tests/ShoppingAssistant.Evals`) and record the pass rate.
- Choose the fix. Options, smallest first:
  1. Say it in `search_policies`' own description too ("for a question about a specific order, include its items' catalog types"). No contract change.
  2. An optional `itemTypes` parameter on `search_policies` that the server adds to the search. A schema field is filled more reliably than prose in another tool's description, it works the same over MCP, and Policy stays independent of Ordering. It changes the tool contract WebApp and the MCP server share. Proposed: 2 with 1.
  3. An optional `orderNumber` parameter, with the server looking up the item types itself. Most reliable, but Policy search would then depend on Ordering.

- [ ] The failing step is identified and recorded in Comments
- [ ] Haiku's pass rate on `returns-gps-watch-last-order` is recorded before and after the fix
- [ ] The chosen fix works for MCP clients as well as WebApp (nothing that relies only on WebApp's system prompt)
- [ ] If the tool contract changes, the MCP server and the assistant tool tests' expected JSON change with it
