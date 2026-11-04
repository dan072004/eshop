# 04: Acceptance check in Claude Code and VS Code

**What to build:** Run the eshop-mcp spec's manual acceptance script end to end and record the results. It proves the week's done-when: Claude Code answers "what's the return window for my last order?" with a Citation.

**Blocked by:** 02 (Claude Code and VS Code sign in), 03 (Policy and order tools), 07 (order tools use the exchanged token), assistant-policies-and-orders 08 (Shopping Assistant acceptance check, which is the gate)

**Status:** done

- [x] `aspire run` with `EmbeddingProvider=Ollama`. No Chat Provider is needed
- [x] Claude Code connected with the documented command, signed in as alice, consent approved
- [x] In WebApp, place an order with a Navigation item and a non-navigation item, and ship it through Ordering.API's ship endpoint
- [x] In Claude Code, "what's the return window for my last order?": 30 days for the Navigation item and 60 for the rest, with end dates from the Order Date, citing *Returns Exceptions › GPS watches, navigators and bike computers* and *Returns Policy › Return window*
- [x] Asking about one of bob's order numbers is answered as not found
- [ ] The same question from Copilot in VS Code, signed in as alice, gets the same answer
- [x] The same with `EmbeddingProvider=None` (keyword mode), with the same Citations
- [x] Results recorded in the ticket's comments

## Comments

**Replanned (2026-11-04).** Now blocked by 07 as well. Run the acceptance script from the rewritten spec, which differs from the list above:
- `aspire run` with `ESHOP_USE_HTTP_ENDPOINTS=1`
- sign-in completes without `invalid_target`
- the Aspire traces show a token-exchange call before each Ordering call
- reconnecting Claude Code doesn't prompt for consent again
- **Small models and someone else's orders (2026-11-04).** In WebApp's Shopping Assistant with `ChatProvider=Ollama` (qwen3:8b), signed in as alice, "Give me Bob's last order" returned alice's own order #11 labelled as Bob's. When challenged, the model told her to sign in. No data leaked: the order tools only return the signed-in Buyer's orders. With `ChatProvider=Claude` the same question got "I can only access the orders of the currently signed-in user". During this pass, also ask for another Buyer's orders by name, not only by order number.
- **Return window from Claude Code passed (2026-11-04).** Signed in as alice, "what's the return window for my last order?" answered for order #11 (29 Oct 2026, Shipped): Adventurer GPS Watch (Navigation) 30 days, return by 28 Nov 2026; Element Outdoor Jacket (Jackets) 60 days, return by 28 Dec 2026. It cited *Returns Exceptions › GPS watches, navigators and bike computers* and *Returns Policy › Return window*, plus *Condition of returned items*, *How to start a return* and *Payments and Billing › Refunds*. It also counted 24 days left on the watch from today's date. Order #11 was placed earlier in WebApp and already shipped, with a Navigation and a non-navigation item. The run used `EmbeddingProvider=Ollama`. Still open: bob's order number, VS Code, and `EmbeddingProvider=None`.
- **Bob's order is not found (2026-11-04).** Signed in as alice in Claude Code, asking about order #21 (bob's) got "I couldn't find order 21 on your account", followed by alice's own two orders (#11, #1). The answer also said that "not found" looks the same whether the order doesn't exist or belongs to someone else, which is the intended behaviour (it doesn't reveal that the order exists). Still open: VS Code and `EmbeddingProvider=None`.
- **Keyword mode passed (2026-11-04).** With `EmbeddingProvider=None`, the same question in Claude Code gave the same windows and dates for order #11 (watch 30 days, 28 Nov 2026; jacket 60 days, 28 Dec 2026). It cited both required Citations, plus *Condition of returned items*, *Return shipping cost* and *Returns Exceptions › Defective items*, and said the Policy search ran in keyword mode, so it might not have found every rule. Still open: the same question from Copilot in VS Code.
- **Closed without the VS Code check (2026-11-04).** Copilot in VS Code still couldn't connect: VS Code falls back to a random callback port when 33418 is busy, and Identity.API accepts only 33418 (see ticket 02's last comment). The user closed the ticket with that box unticked. Claude Code passed every other check, in both Ollama and keyword mode.
