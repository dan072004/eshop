# Spec: Shopping Assistant replies survive a failing Chat Provider

Status: ready-for-agent

## Problem Statement

When the Chat Provider is slow or failing, a Buyer using the Shopping Assistant is left on "Thinking..." for as long as the provider takes. With Claude, one request can hang for up to 10 minutes per attempt, retried twice, and one turn can make several requests because of tool calls. When a failure does surface, the Buyer gets "My apologies, but I encountered an unexpected error.", and that apology is stored in the conversation as if the model had written it. On the next turn the model is sent an assistant message it never wrote.

## Solution

A Buyer waits at most about a minute for a Claude reply. Brief provider hiccups (rate limits, overload, a dropped connection, a slow attempt) are retried without the Buyer noticing. When a reply still can't be produced, the Buyer sees one plain notice: "The Shopping Assistant isn't available right now. Please try again in a moment." The notice is visibly from the system, not the assistant. It is never added to the conversation the model sees, and it goes away when the Buyer sends their next message. The Buyer's own message stays in the conversation, so their next attempt still has the context.

## User Stories

1. As a Buyer, I want the Shopping Assistant to give up within about a minute when it can't answer, so that I'm not left staring at "Thinking..." indefinitely.
2. As a Buyer, I want short provider hiccups to be retried for me, so that a momentary rate limit or overload doesn't cost me a reply.
3. As a Buyer, I want a clear, calm notice when the Shopping Assistant is unavailable, so that I know to try again instead of wondering whether my question was understood.
4. As a Buyer, I want the notice to look different from the assistant's replies, so that I don't mistake a system error for an answer.
5. As a Buyer, I want the notice to disappear when I send my next message, so that the conversation doesn't fill up with stale errors.
6. As a Buyer, I want my question to stay in the conversation after a failure, so that I can see what I asked and my next attempt keeps the context.
7. As a Buyer, I want the Shopping Assistant's next reply not to be shaped by an apology it never wrote, so that its answers stay accurate.
8. As a Buyer, I want a failed turn never to add the same product to my basket twice, so that an outage doesn't silently change my basket.
9. As a Buyer using a screen reader, I want the unavailable notice to be announced, so that I know the reply failed without having to look for it.
10. As a Buyer, I want the same notice whatever went wrong (timeout, overload, misconfiguration), so that I get one simple instruction: try again.
11. As an operator, I want each failure logged with its exception type and status, a warning for timeouts and an error for everything else, so that I can tell a provider outage from a misconfiguration.
12. As an operator running Ollama, I want the local model to keep its long time budget, so that slow first replies from a loading model are not cancelled.
13. As a developer running the evals, I want the evals to exercise the same deadline that ships, so that an eval passes only if the answer arrives in time.

## Implementation Decisions

- **Scope is the Chat Provider call.** The tool backends (Catalog, Ordering, policy-api) already get the ServiceDefaults standard resilience handler, and their failures already reach the model as tool errors. They are not changed.
- **Claude client.** The Anthropic SDK builds its own HTTP client, so the ServiceDefaults handler doesn't apply to it. The client is configured with a 30-second timeout per attempt and an explicit maximum of 2 retries. The SDK's built-in retries are kept: they cover connection errors, request timeouts, 408, 409, 429 and 5xx, and they honour `retry-after`. No Polly handler is layered on top, because two retry layers would multiply the attempts.
- **Per-turn deadline for Claude.** The Claude chat pipeline gets one more step, outermost and around function invocation, that cancels the whole turn after 60 seconds. That turn covers every model request and every tool call. The pipeline's build method takes the deadline as an optional parameter that defaults to 60 seconds, so tests can pass a short one. It is not a configuration setting. Because the eval builds the same pipeline, it runs under the same deadline.
- **The deadline caps retries.** Three timed-out 30-second attempts would take 90 seconds, so a run of timeouts ends at 60 seconds, after the second attempt. This is intended.
- **Other providers keep their own time budgets.** Ollama keeps its 5-minute HTTP timeout and its lack of a resilience handler, because local models load slowly. Foundry keeps its client's defaults. The deadline lives in each provider's pipeline, not in the Shopping Assistant's chat state, so the chat state stays the same for every provider.
- **No turn-level retry.** The chat state never retries a turn. Running a turn again would run tools such as adding to the basket a second time. A one-line code comment records this at the catch.
- **Fallback in the chat state.** The chat state gets a new public piece of state: the current unavailable notice, empty when there isn't one. On any exception during a turn, the chat state:
  - logs the exception, as a warning for a timeout or cancellation and as an error otherwise;
  - sets the notice to the single fallback text;
  - leaves the conversation's messages ending with the Buyer's message.

  Sending a new message clears the notice. The old behaviour, adding an apology as an assistant message, is removed.
- **Fallback in the chat panel.** The chat panel shows the notice under the last message, styled like the existing "Assistant isn't switched on" notice, with `role="status"`.
- **No new domain terms.** No glossary entry and no ADR: each decision is easy to reverse.

## Testing Decisions

- A good test drives the Shopping Assistant through its public entry point (sending a Buyer message) and asserts on what the Buyer and the model would see: the notice and the conversation's messages. It asserts nothing about the internals of the pipeline.
- **One seam.** The chat state's send method, running over the real Claude pipeline built with a fake inner chat client and a short deadline. The model is the only fake, because it's the boundary we don't own.
- **Tests, all in the existing Shopping Assistant tests:**
  1. The model throws: the notice is set, the messages end with the Buyer's message, and no assistant apology is added.
  2. The model never answers and the deadline is 50 ms: the same notice appears, and the call returns within the deadline instead of hanging.
  3. The Buyer sends again after a failure: the notice clears, and the request sent to the model includes the earlier Buyer message.
- **Not tested.** The SDK's per-attempt timeout and maximum retries are configuration constants. A test that asserts them would be shape-only, which ADR 0003 rules out, so they are checked in review.
- **Prior art.** The existing Shopping Assistant tests build the chat state over a recording fake chat client. The Claude pipeline tests build the pipeline around a fake inner client.

## Out of Scope

- Retries, timeouts or messages for the tool backends. They already have them.
- Changing the time budgets for Ollama or Foundry.
- Cancelling an in-flight reply when the Buyer closes the chat or the Blazor circuit drops. Open question: today the request runs on until it finishes or hits the deadline, and its tokens are paid for. The 60-second deadline caps that waste.
- Different Buyer-facing messages for different failure kinds.
- Making the deadline or the timeouts configurable.
- Streaming replies.

## Further Notes

- After a failed turn, the next request ends with two Buyer messages in a row. Microsoft.Extensions.AI's Anthropic adapter is expected to merge them, or the API to accept them. Confirm this while implementing, for example with the third test plus one manual run against Claude.
- If a tool ran before the turn failed (for example, an item was added to the basket), that action stands, but the model won't know about it next turn. This is accepted.
