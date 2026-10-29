# eShop

An online store where buyers browse a catalog, fill a basket and place orders that move through validation, payment and shipping.

## Language

### People

**Buyer**:
The person who places an Order.
_Avoid_: Customer, user, client

### Shopping Assistant

**Shopping Assistant**:
The conversational helper that answers a Buyer's questions about the catalog, their account and their basket, and can add items to the basket.
_Avoid_: Concierge, chatbot, AI agent

**Chat Provider**:
The AI service that generates the Shopping Assistant's replies (Ollama, Microsoft Foundry or Claude). Exactly one is chosen, or none, in which case the Shopping Assistant is switched off.
_Avoid_: Model, backend, "the AI"

### Policies

**Policy**:
A published document of AdventureWorks' rules for Buyers on one topic, such as returns, shipping, warranty, sizing or payments. Each Policy has a title that other Policies refer to by name.
_Avoid_: FAQ, help article, knowledge base, doc

**Policy Section**:
One headed part of a Policy, such as "Returns Exceptions › Helmets". It is the smallest unit the Shopping Assistant quotes from.
_Avoid_: Chunk, passage, snippet

**Citation**:
The Policy title and Policy Section name that the Shopping Assistant gives next to a rule it states, so the Buyer can find the source.
_Avoid_: Reference, source link, footnote

**Return Window**:
The period after the Order Date during which an item can be returned. It is 60 days unless a Policy sets a different period for that kind of item.
_Avoid_: Return period, return deadline

**Order Date**:
The date the Buyer placed the Order. Return Windows and warranty periods are counted from it.
_Avoid_: Purchase date, ship date

**Final Sale**:
An item that a Policy says cannot be returned or exchanged, although it is still covered by warranty.
_Avoid_: Non-returnable, clearance
