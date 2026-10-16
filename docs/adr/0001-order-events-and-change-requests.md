# Order history in one OrderEvents table, with pending change requests in their own table

Floor staff can change orders after they are sent, and changes to In progress orders need Kitchen approval. That means storing pending proposals as well as a history of everything that happened to an order. We store each proposal as a `ChangeRequest` row (Pending, Approved, Denied or Withdrawn, with its proposed lines as child rows), and record every status change, applied order change and change request decision as a row in a single `OrderEvents` table with a `Kind` field. This replaces the `OrderStatusChanges` table planned in Version 3 of the production readiness plan.

## Considered Options

- **Keep `OrderStatusChanges` and add `ChangeRequests` beside it.** Rejected: changes to New orders apply immediately and would leave no record, and the history of an order would be split across two tables.
