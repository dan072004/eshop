# 05: Admin creates and lists Coupons

**What to build:** An Admin opens the Coupons page in the admin area, creates a Coupon (Coupon Code, Coupon Value as a percentage or fixed amount, Validity Period in their local time, Redemption Limit) and sees it in the list with its state. This introduces the Coupon aggregate in Ordering (ADR 0001), its storage and the admin coupon API.

**Blocked by:** 03, 04

**Status:** ready-for-agent

- [ ] The Coupon aggregate enforces: code required and trimmed; percentage above 0 and at most 100; fixed amount above 0; end after start; Redemption Limit at least 1
- [ ] Coupon Codes are unique ignoring case across all Coupons ever created; a duplicate is refused with a clear message
- [ ] The Validity Period is stored in UTC, with the start included and the end excluded; WebApp converts to and from the Admin's local time
- [ ] Coupons are stored in the Ordering database through a migration that Ordering.API runs
- [ ] Admin endpoints to create, list and get Coupons, protected by the `Admin` policy; non-admin requests are refused
- [ ] The list shows code, value, Validity Period, Redemption Limit, active Redemption count (zero for now) and state: scheduled, active, expired, Deactivated or used up
- [ ] WebApp has admin pages to list and create Coupons, with validation errors shown on the form
- [ ] Functional tests cover create, duplicate code (different case), each validation rule, list, get, and non-admin refusal
- [ ] Coupon aggregate unit tests cover the Validity Period edges using a fixed "now"
