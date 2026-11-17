# 03: Estimated cost on spans and the cost counter

**What to build:** For Claude models with a listed price, the dashboard shows what each model call and each Turn cost, as an estimate in US dollars on their spans. The Metrics tab shows a running total of the estimated cost since WebApp started, by model. A model with no listed price (Ollama, Foundry, an unlisted Claude model) shows tokens but no cost anywhere.

**Blocked by:** 01 (One price table for WebApp and the evals, with cache rates), 02 (Every Chat Provider's Turns in the dashboard, with tokens)

**Status:** ready-for-agent

- [ ] Each Claude `chat` span carries `eshop.assistant.estimated_cost_usd`, priced from the shared table, including cache reads and writes, and never counting cached tokens twice (see ticket 02's finding)
- [ ] The Turn span carries the Turn's total estimated cost
- [ ] A counter `eshop.assistant.estimated_cost` (unit `USD`, tagged with the model id) goes up by each model call's estimated cost, once per model call. WebApp collects its meter
- [ ] An unlisted model gets no cost attribute and no counter increase
- [ ] Tests at ticket 02's seam (the real adapter over a fake HTTP handler): a two-call Turn on a priced model asserts each span's cost, the Turn's total, and two counter increases tagged with the model that add up to the Turn's cost; the same Turn on an unlisted model asserts no cost attribute and no increase. Expected values are literals
- [ ] Manual check: a Claude run shows cost on the spans and the counter in the Metrics tab
