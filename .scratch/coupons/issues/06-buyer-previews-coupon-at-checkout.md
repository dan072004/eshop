# 06: Buyer previews a Coupon at checkout

**What to build:** On the Checkout page a Buyer types a Coupon Code and clicks Apply. The server works out the Order Discount and shows the subtotal, Order Discount and new total, or a clear reason the Coupon cannot be used. The Buyer can remove the code again. Nothing is redeemed at this stage, and checkout without a code is unchanged.

**Blocked by:** 01, 05

**Status:** ready-for-agent

- [ ] The order-draft endpoint accepts an optional Coupon Code and returns subtotal, Order Discount and total
- [ ] Percentage Order Discount is rounded down to the cent; a fixed amount is capped at the subtotal, so the total is never below zero
- [ ] Specific reasons are returned for an unknown code, not started yet, expired and used up
- [ ] Codes are matched ignoring case
- [ ] The preview never creates a Redemption
- [ ] Checkout has a Coupon Code field with Apply and Remove, showing the preview or the reason next to the field
- [ ] Draft requests without a code behave exactly as before (existing draft tests stay green)
- [ ] Functional tests cover the percentage, fixed amount and capped cases and each reason; Coupon unit tests cover rounding
