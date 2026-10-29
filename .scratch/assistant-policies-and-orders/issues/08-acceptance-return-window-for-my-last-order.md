# 08: Acceptance check: return window for my last order

**What to build:** Run the spec's manual acceptance script end to end and record the results. This is the gate before eShop.Mcp's acceptance check (eshop-mcp ticket 04), which asks the same question and expects the same answer.

**Blocked by:** 04 (Policy questions with Citations), 05 (semantic Policy search), 07 (reads one order)

**Status:** ready-for-human

- [x] `aspire run` with `ChatProvider=Claude` and `EmbeddingProvider=Ollama`
- [x] Signed in as alice, place an order with a Navigation item and a non-navigation item
- [x] Before shipping, "what's the return window for my last order?": the answer says it can't be returned until it ships, with a Citation from the Returns Policy
- [x] After shipping it through Ordering.API's ship endpoint, the same question: 30 days for the Navigation item and 60 for the rest, with end dates from the Order Date, citing *Returns Exceptions › GPS watches, navigators and bike computers* and *Returns Policy › Return window*
- [x] The same with `EmbeddingProvider=None` (keyword mode), with the same Citations
- [x] Signed out: order questions get a sign-in message, and a Policy question still gets a cited answer
- [x] Results recorded in the ticket's comments, including any status-name mapping or `today` time-zone issue (see the spec's Further Notes)

## Comments

Acceptance run on 2026-10-29, with `ChatProvider=Claude`, signed in as alice.

- **Before shipping:** passed. The answer said the order can't be returned until it ships, cited from the Returns Policy.
- **After shipping, first attempt:** failed. The answer was "60 days" for the whole order, cited only from *Returns Policy › Return window*. Trace `e6b12294848d8d02055ef085ceb249da` shows `list_my_orders` → `search_policies("What is the return window for orders?")` → answer. `get_order` was never called, so the model never saw the Navigation item. That query also doesn't rank Returns Exceptions in its top 5.
- **Fix (tool contract change, recorded in both specs):** the `list_my_orders` description now says it has no items and points to `get_order`. The `get_order` description now says some Policy rules, such as Return Windows, depend on an item's type, so name the types when searching the Policies. The descriptions are now the constants `ListMyOrdersDescription` and `GetOrderDescription`, and their contract tests check the full text.
- **After shipping, after the fix:** passed. Order #11 (Adventurer GPS Watch, Element Outdoor Jacket, Order Date Oct 29): 30 days to Nov 28 for the watch, cited from *Returns Exceptions › GPS watches, navigators and bike computers*, and 60 days to Dec 28 for the jacket, cited from *Returns Policy › Return window*. An earlier order with trail shoes got 60 days cited from *Returns Exceptions › Footwear*, which is correct. Use a non-exception item, such as a jacket or bag, when comparing with eshop-mcp ticket 04.
- **`EmbeddingProvider=None`:** passed, with the same answer and Citations. policy-api reported `"mode":"keyword"`, and "return window Navigation" ranks the GPS section first.
- **Signed out:** passed. The order question got a sign-in message with no made-up order. "How long do I have to return a GPS watch?" got 30 days, cited from the GPS section. Minor: that answer also gave the $6.95 return label fee without a Citation (*Returns Policy › Return shipping cost*). Not fixed; raise it as a ticket if eshop-mcp's acceptance check shows it too.
- **Status names:** didn't come up, because no answer quoted a status. The order tools don't map status names.
- **`today` time zone:** no issue seen. Every end date was right; the questions were asked during the day on Oct 29 (UTC).
