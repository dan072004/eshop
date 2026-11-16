Week 3, feature 1: Evals for the Shopping Assistant, with a CI gate.

Source: the "Evals" section and the Week 3 milestone of the project spec,
https://claude.ai/artifact/E3kDbvMo78G2isqjDHGmTD#647489b0-5cda.mjwqtx42afr.5484
(read it through the Claude Docs connector). Week 3 is done when CI shows eval
scores on every PR and the README tells the full story.

What the spec proposes (Oct 2, treat it as a starting point, not a decision):
- 40 questions in a JSON file: 15 Policy answers (correct per the Policy, cited),
  10 order lookups (calls the tool, reports the real status), 5 catalog
  questions (uses search_catalog, invents no products), 5 out of scope (politely
  declines), 5 adversarial (never leaks another Buyer's order).
- Scoring: deterministic checks first (right tool called, Citation present, no
  other Buyer's order id in the output), then an LLM judge for correctness and
  groundedness via Microsoft.Extensions.AI.Evaluation or our own rubric.
- Responses cached between runs, so unchanged cases cost nothing.
- Separate test project in `dotnet test`; the build fails below a 90% pass rate
  or on any adversarial failure; the report is a CI artifact and the README
  shows the latest score.

What has changed since then:
- The chat model defaults to claude-haiku-4-5 (ChatProvider=Claude), so a
  "Haiku-class judge" would grade its own model.
- The pipeline under test is ChatState (src/WebApp/Components/Chatbot), with
  its inline system prompt, the shared tools in src/eShop.Assistant.Tools
  (search_policies, search_catalog, list_my_orders, get_order) and the
  WebApp-only tools (GetUserInfo, AddToCart, GetCartContents).
- The 15 Policies are in policies/; Citation rules are in the tool
  descriptions and results (see .scratch/assistant-policies-and-orders/spec.md).
- eShop.Mcp exposes the same shared tools to Claude Code.

Read first: CLAUDE.md (especially Testing), GLOSSARY.md, docs/adr/,
.scratch/assistant-policies-and-orders/spec.md, ChatState.cs,
tests/WebApp.UnitTests/ShoppingAssistantTests.cs, .github/workflows/pr-validation.yml.

Decisions I expect the grilling to settle (with your recommendation and its
trade-offs for each):
1. What is under test: the model + system prompt + tool descriptions only (tool
   backends faked with canned answers), or the whole stack with real
   Catalog/Ordering/policy-api through Aspire like the functional tests? Which
   seam does this sit at under our Testing rules, and does it need an ADR?
2. Where the order data comes from: seeded Buyers (alice, bob) with known
   orders, so "the real status" and "another Buyer's order" are literals in the
   case file.
3. What an eval case looks like: question, signed-in or not, expected tool
   calls, expected Citations, forbidden strings, and a rubric or reference
   answer for the judge. What can be checked without a judge?
4. The judge: which model, which evaluators (built-in Relevance/Groundedness vs
   our own rubric), and how we know the judge itself is right.
5. Non-determinism: runs per case, temperature, and what "pass rate" means
   when a case passes 2 of 3 times. Is 90% of 40 the right gate?
6. CI: the API key as a GitHub secret, PRs that can't see secrets, cost per
   run, whether every PR runs the suite or only PRs that touch the assistant,
   and how response caching works on a fresh CI runner.
7. Reporting: where scores show up on the PR (artifact, job summary, comment)
   and how the README gets the latest score.
8. Scope: Claude only, or Ollama too? Is the MCP host in scope, or is it covered
   because it shares the tools?

Stop when the grilling is done. I'll run the spec and tickets steps myself.
