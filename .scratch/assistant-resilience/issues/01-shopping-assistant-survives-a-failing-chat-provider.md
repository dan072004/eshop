# 01: The Shopping Assistant survives a failing Chat Provider

**What to build:** When Claude is slow or failing, a Buyer gets a Shopping Assistant reply or a clear "unavailable" notice within about a minute, instead of waiting on "Thinking..." for many minutes.

- Short provider hiccups (rate limits, overload, a dropped connection, a slow attempt) are retried without the Buyer noticing.
- A turn that still fails shows one system notice: "The Shopping Assistant isn't available right now. Please try again in a moment."
- The notice is never added to the conversation the model sees, and it clears when the Buyer sends their next message.
- The Buyer's own message stays in the conversation.
- Ollama and Foundry keep their current time budgets.

See the spec, `.scratch/assistant-resilience/spec.md`, for the decisions and their reasons.

**Blocked by:** None (can start immediately)

**Status:** done

- [x] The Claude client times out each attempt after 30 seconds and retries at most 2 times, using the SDK's built-in retries. No extra retry layer is added. Checked by hand against a fake server: a hanging one got a retry at 30.4 s and the notice; a failing one got 3 requests in 1.3 s, then the notice.
- [x] The Claude chat pipeline cancels a whole turn after 60 seconds. The turn covers every model request and every tool call. The deadline is an optional build parameter that defaults to 60 seconds. It is not a configuration setting, and the evals run under it.
- [x] The chat state never retries a turn. A one-line comment says why: a retry would run tools such as adding to the basket twice.
- [x] On any failure, the chat state:
  - logs it, as a warning for a timeout or cancellation and as an error otherwise;
  - sets a public "unavailable" notice;
  - leaves the messages ending with the Buyer's message;
  - does not add an assistant apology.

  The old apology message is removed.
- [x] Sending a new message clears the notice.
- [x] The chat panel shows the notice under the last message, styled like the existing "Assistant isn't switched on" notice, with `role="status"`.
- [x] Ollama's 5-minute timeout and Foundry's client defaults are unchanged.
- [x] The Shopping Assistant tests drive the chat state over the real Claude pipeline, with a fake inner chat client as the only fake, and cover three cases:
  - the model throws, so the notice is set, the messages end with the Buyer's message, and there's no apology;
  - the model never answers with a 50 ms deadline, so the same notice appears and the call returns instead of hanging;
  - the Buyer sends again after a failure, so the notice clears and the request includes the earlier Buyer message.
- [x] Confirm that a request ending in two Buyer messages in a row works against Claude. Either the adapter merges them or the API accepts them. The API accepts them: "Consecutive `user` or `assistant` turns in your request will be combined into a single turn." (Messages API reference, platform.claude.com/docs/en/api/messages)
