# Coupons live in the Ordering service

Coupons and their Redemptions are an aggregate inside Ordering rather than a separate Promotions service. Placing an Order and redeeming a Coupon must succeed or fail together, and enforcing the Redemption Limit under concurrent checkouts is simple inside one database transaction but would need a reservation step plus compensating integration events across services.

## Consequences

- Releasing a Redemption when an Order is cancelled is an in-process domain event handler, not an integration event.
- If promotions grow beyond whole-order Coupons (catalog-scoped offers, loyalty, campaigns), revisit this and extract a Promotions service.
