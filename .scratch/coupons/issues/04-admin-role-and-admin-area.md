# 04: Admin role and admin area

**What to build:** The store gets Admins. A seeded `admin` user (dev password `Pass123$`, like alice and bob) signs in to the web store, sees an Admin link and reaches an admin landing page. Buyers such as alice see no link and are refused if they open the page directly. Ordering also gets an `Admin` authorization policy, ready for the admin coupon endpoints.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] Identity seeds an `admin` role and an `admin` user in that role, idempotently like the existing user seed
- [ ] Tokens issued to WebApp and the APIs carry the user's roles as `role` claims
- [ ] WebApp has an `Admin` policy based on the role claim and an `/admin` landing page protected by it
- [ ] The Admin link appears in WebApp only for Admins
- [ ] A Buyer opening the admin page is refused, and a signed-out visitor is sent to sign in
- [ ] Ordering has an `Admin` authorization policy requiring the `admin` role (no endpoints use it yet)
- [ ] Existing sign-in for alice and bob is unchanged
