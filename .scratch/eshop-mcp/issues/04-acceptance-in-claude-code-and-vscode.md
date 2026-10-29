# 04: Acceptance check in Claude Code and VS Code

**What to build:** Run the eshop-mcp spec's manual acceptance script end to end and record the results. It proves the week's done-when: Claude Code answers "what's the return window for my last order?" with a Citation.

**Blocked by:** 02 (Claude Code and VS Code sign in), 03 (Policy and order tools), assistant-policies-and-orders 08 (Shopping Assistant acceptance check, which is the gate)

**Status:** ready-for-human

- [ ] `aspire run` with `EmbeddingProvider=Ollama`. No Chat Provider is needed
- [ ] Claude Code connected with the documented command, signed in as alice, consent approved
- [ ] In WebApp, place an order with a Navigation item and a non-navigation item, and ship it through Ordering.API's ship endpoint
- [ ] In Claude Code, "what's the return window for my last order?": 30 days for the Navigation item and 60 for the rest, with end dates from the Order Date, citing *Returns Exceptions › GPS watches, navigators and bike computers* and *Returns Policy › Return window*
- [ ] Asking about one of bob's order numbers is answered as not found
- [ ] The same question from Copilot in VS Code, signed in as alice, gets the same answer
- [ ] The same with `EmbeddingProvider=None` (keyword mode), with the same Citations
- [ ] Results recorded in the ticket's comments
