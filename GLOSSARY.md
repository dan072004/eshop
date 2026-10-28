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
