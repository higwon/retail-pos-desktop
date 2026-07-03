# ADR-003: Use PendingCheckout for Payment Recovery

## Status

Accepted

## Context

A POS checkout flow can fail at a dangerous point:

```text
Payment approved -> Application crashes before order is saved
```

If the system only creates an order after payment approval, an approved payment may be lost when local persistence fails or the application terminates unexpectedly.

## Decision

Persist a `PendingCheckout` record before requesting payment approval.

The checkout flow should be:

```text
Validate cart
-> Save PendingCheckout
-> Request payment approval
-> Persist payment approval result
-> Create and save order
-> Add sync queue item
-> Mark PendingCheckout completed
```

## Reasoning

This protects the system from losing approved payment state.

The application can detect incomplete approved checkouts on startup and recover them idempotently.

## Consequences

Positive:

- Approved payment state is recoverable.
- Checkout can survive process termination.
- Recovery logic can prevent duplicate orders.
- The design demonstrates POS-grade reliability thinking.

Trade-offs:

- Checkout flow becomes more complex.
- More local persistence states are required.
- Recovery UI and manager review rules are needed.
