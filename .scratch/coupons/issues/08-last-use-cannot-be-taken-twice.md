# 08: The last use of a Coupon can't be taken twice

**What to build:** When two Buyers place Orders at the same moment for the last remaining use of a Coupon, exactly one succeeds. The other is rejected with the "used up" reason, and the Redemption Limit is never exceeded.

**Blocked by:** 07

**Status:** ready-for-agent

- [ ] The Coupon carries an optimistic concurrency token that is checked whenever a Redemption is added
- [ ] On a concurrency conflict, the Coupon is reloaded and checked again; if it is used up, the Order is rejected with that reason and no Order is created
- [ ] A conflict that leaves capacity (for example, a concurrent admin edit) does not wrongly reject the Order
- [ ] A functional test fires two concurrent Orders at a Coupon with one remaining use and asserts exactly one Redemption and one rejection
