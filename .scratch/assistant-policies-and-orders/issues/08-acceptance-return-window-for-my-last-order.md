# 08: Acceptance check: return window for my last order

**What to build:** Run the spec's manual acceptance script end to end and record the results. This is the gate before eShop.Mcp's acceptance check (eshop-mcp ticket 04), which asks the same question and expects the same answer.

**Blocked by:** 04 (Policy questions with Citations), 05 (semantic Policy search), 07 (reads one order)

**Status:** ready-for-human

- [ ] `aspire run` with `ChatProvider=Claude` and `EmbeddingProvider=Ollama`
- [ ] Signed in as alice, place an order with a Navigation item and a non-navigation item
- [ ] Before shipping, "what's the return window for my last order?": the answer says it can't be returned until it ships, with a Citation from the Returns Policy
- [ ] After shipping it through Ordering.API's ship endpoint, the same question: 30 days for the Navigation item and 60 for the rest, with end dates from the Order Date, citing *Returns Exceptions › GPS watches, navigators and bike computers* and *Returns Policy › Return window*
- [ ] The same with `EmbeddingProvider=None` (keyword mode), with the same Citations
- [ ] Signed out: order questions get a sign-in message, and a Policy question still gets a cited answer
- [ ] Results recorded in the ticket's comments, including any status-name mapping or `today` time-zone issue (see the spec's Further Notes)
