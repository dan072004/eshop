# 02: Stock-rejected cancellation raises the Order-cancelled event

**What to build:** When an Order is cancelled because some items are out of stock, it behaves like every other cancellation: the Order-cancelled domain event is raised, so the cancelled integration event is published and the Buyer's Orders page refreshes live. Today this path cancels the Order silently. Ticket 09 depends on this so that every cancellation can release a Redemption.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] Cancelling an Order for stock rejection raises the Order-cancelled domain event, exactly once
- [ ] The stock-rejected description listing the missing items is kept
- [ ] Stock rejection still only applies to Orders awaiting validation; other states are unchanged
- [ ] An Order aggregate unit test covers the event on the stock-rejected path
- [ ] Existing Ordering tests stay green
