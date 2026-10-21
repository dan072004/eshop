# 11: Admin sees a Coupon's Redemption history

**What to build:** From the Coupons list an Admin opens a Coupon's Redemption history and sees every Redemption: when it was redeemed, on which Order, by which Buyer, for what Order Discount, and, for a Released Redemption, when it was released. The list page shows each Coupon's active Redemption count.

**Blocked by:** 07

**Status:** ready-for-agent

- [ ] An admin endpoint lists a Coupon's Redemptions, newest first, protected by the `Admin` policy
- [ ] Each entry shows redeemed-at, Order number, Buyer, Order Discount and released-at (empty unless released)
- [ ] Times are shown in the Admin's local time
- [ ] The Coupons list shows the active Redemption count, excluding Released Redemptions
- [ ] A WebApp admin page shows the history, linked from the Coupons list
- [ ] Functional tests cover the history after several Redemptions by different Buyers, and non-admin refusal
