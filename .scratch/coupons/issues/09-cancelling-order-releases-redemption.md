# 09: Cancelling an Order releases its Redemption

**What to build:** Whenever an Order with a Coupon is cancelled (by the Buyer, because stock was rejected, or because payment failed), its Redemption becomes a Released Redemption: it keeps its history with a released-at time but stops counting toward the Redemption Limit and the Per-Buyer Limit. The Buyer can then use the Coupon again. Redemptions on Paid or Shipped Orders are permanent.

**Blocked by:** 02, 07

**Status:** ready-for-agent

- [ ] A handler for the Order-cancelled domain event releases the Order's active Redemption in the same transaction as the cancellation
- [ ] Released Redemptions keep all their data and gain a released-at time
- [ ] Released Redemptions are excluded from both limits and from the active Redemption count in the admin list
- [ ] Cancelling an Order without a Coupon is unchanged
- [ ] A functional test: a Buyer redeems, cancels the Order, then redeems the same Coupon again on a new Order
- [ ] A functional test: a Coupon at its Redemption Limit gets capacity back after a cancellation
- [ ] Coupon unit tests cover release freeing capacity, and that releasing twice has no extra effect
