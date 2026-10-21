# eShop

An online store where buyers browse a catalog, fill a basket and place orders that move through validation, payment and shipping.

## Language

### Promotions

**Coupon**:
An offer, created by an Admin, that reduces the price of a whole Order when its Coupon Code is applied at checkout.
_Avoid_: Voucher, promo, discount code

**Coupon Code**:
The human-readable text a Buyer enters to apply a Coupon, unique across all Coupons ever created and matched without regard to case.
_Avoid_: Promo code, token

**Coupon Value**:
What a Coupon takes off an Order: either a percentage of the Order total or a fixed amount, never more than the total itself.
_Avoid_: Discount rate, amount off

**Validity Period**:
The window in which a Coupon can be redeemed, from its start (inclusive) to its end (exclusive).
_Avoid_: Valability, expiry window, active dates

**Redemption Limit**:
The maximum number of active Redemptions a Coupon may have across all Buyers.
_Avoid_: Quota, max uses, stock

**Per-Buyer Limit**:
The rule that a Buyer may hold at most one active Redemption of a given Coupon.
_Avoid_: Per-user limit, once-per-customer

**Redemption**:
The record that a Coupon was applied to a specific Order placed by a specific Buyer, at a specific moment, for a specific Order Discount.
_Avoid_: Use, usage, claim

**Released Redemption**:
A Redemption whose Order was cancelled; it keeps its history but no longer counts toward the Redemption Limit or the Per-Buyer Limit.
_Avoid_: Refunded coupon, voided redemption

**Order Discount**:
The money amount a Redemption actually took off its Order's total.
_Avoid_: Savings, reduction

**Deactivated Coupon**:
A Coupon an Admin has switched off before its Validity Period ends; it accepts no new Redemptions but keeps its existing ones.
_Avoid_: Deleted coupon, disabled coupon, expired coupon

### People

**Admin**:
A store operator allowed to create and manage Coupons and see their Redemptions.
_Avoid_: Administrator, staff, back-office user

**Buyer**:
The person who places an Order.
_Avoid_: Customer, user, client
