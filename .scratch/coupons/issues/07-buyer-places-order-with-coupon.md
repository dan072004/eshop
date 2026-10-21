# 07: Buyer places an Order with a Coupon

**What to build:** A Buyer who applied a valid Coupon places the Order. The Coupon is redeemed in the same transaction: a Redemption records the signed-in Buyer, the Order, the moment and the Order Discount, and the Order stores the Coupon Code and Order Discount. The Redemption Limit and Per-Buyer Limit are enforced both when placing the Order and in the preview. If the Coupon stopped being valid since the preview, the Order is rejected with the reason and the basket is kept. The Buyer's Orders page shows the Coupon Code and Order Discount. One Playwright journey proves the whole feature from Admin to Buyer.

**Blocked by:** 06

**Status:** ready-for-agent

- [ ] The create-order request accepts an optional Coupon Code; without it, placing an Order behaves exactly as before
- [ ] Placing an Order with a valid Coupon creates a Redemption and stores the Coupon Code and Order Discount on the Order, all in one transaction
- [ ] The Buyer on the Redemption is the authenticated user, never the user id in the request body
- [ ] Order totals subtract the Order Discount, using the formula from ticket 01
- [ ] An Order is rejected, with the specific reason and no Order created, when the Coupon is unknown, not started, expired, used up, or the Buyer already holds an active Redemption of it
- [ ] The preview also reports the Per-Buyer Limit for the signed-in Buyer
- [ ] A retried request with the same request id does not create a second Redemption
- [ ] WebApp shows the rejection reason on Checkout and keeps the basket
- [ ] The Buyer's Orders page shows the Coupon Code and Order Discount on Orders that have one
- [ ] Functional tests cover a successful Redemption, the Redemption Limit, the Per-Buyer Limit with two different Buyers, and a rejected Order
- [ ] A Playwright journey: the seeded admin signs in and creates a Coupon, then alice applies it at checkout, sees the discounted total and places the Order
