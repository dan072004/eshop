# 10: Admin edits, deactivates and deletes Coupons

**What to build:** An Admin can change a Coupon freely until it is first redeemed. After that, the code and Coupon Value are locked, but the end of the Validity Period can still be extended and the Redemption Limit raised. An Admin can deactivate a Coupon at any time: it then accepts no new Redemptions and keeps existing ones. An Admin can delete a Coupon only if it was never redeemed; otherwise the delete is refused with a hint to deactivate.

**Blocked by:** 07

**Status:** ready-for-agent

- [ ] Admin endpoints to update, deactivate and delete a Coupon, protected by the `Admin` policy
- [ ] An unredeemed Coupon can change every field, under the same rules as creation (including a unique code)
- [ ] A redeemed Coupon refuses changes to its code and Coupon Value with a clear message, and allows extending the end and raising the Redemption Limit
- [ ] Shortening the Validity Period or lowering the Redemption Limit is allowed only while the Coupon has never been redeemed
- [ ] A Deactivated Coupon is refused by both the preview and placing an Order with a "Deactivated" reason; existing Redemptions and their Orders are unaffected
- [ ] Deleting a never-redeemed Coupon removes it; deleting a redeemed one returns a conflict suggesting deactivation
- [ ] WebApp admin pages to edit, deactivate (with confirmation) and delete, showing which fields are locked and why
- [ ] Functional tests cover each rule above, including non-admin refusal
