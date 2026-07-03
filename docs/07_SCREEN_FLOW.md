# Screen Flow

## Main Flow

```text
App Start
↓
Login
↓
POS Main
↓
Product Search or Barcode Scan
↓
Cart Update
↓
Discount Optional
↓
Payment
↓
Order Complete
↓
Receipt
↓
Local Save
↓
Sync Queue
↓
Background Sync
```

## Login Flow

```text
Enter Employee Code
↓
Enter Password
↓
Call Login API
↓
Success: Open POS Main
Failure: Show Error
Server Unavailable: Validate cached employee credentials
```

The first login on a terminal requires the server. A successful online login refreshes the local employee credential cache and permission snapshot. Offline login is allowed only for cached employees for 7 days after their most recent successful online authentication.

## Product Scan Flow

```text
Barcode Input
↓
Find Product in Local DB
↓
If Found: Add to Cart
If Not Found: Show Not Found Message
```

## Checkout Flow

```text
Validate Cart
↓
Open Payment Dialog
↓
Simulate Payment
↓
If Approved:
    Create Order
    Save Order Locally
    Update Local Stock
    Create Receipt
    Add Sync Queue Item
    Clear Cart
If Failed:
    Keep Cart
    Show Payment Failure
```

## Checkout Persistence and Recovery

Before opening the payment approval flow, save a `PendingCheckout` containing the confirmed cart snapshot. After approval, persist the approval result, create and save the order, add its sync queue item, and mark the pending checkout completed.

```text
App Start
-> Find PendingCheckout records in ApprovedButOrderNotCreated
-> If any exist, open Checkout Recovery
-> Recreate Order idempotently or leave for Manager Review
-> After successful order save, add Sync Queue item
-> Mark PendingCheckout Completed
```

## Offline Sync Flow

```text
Network Unavailable
↓
Orders Completed Locally
↓
SyncQueue Status = Pending
↓
Network Available
↓
Background Sync Service Runs
↓
Upload Pending Orders
↓
Mark Synced or Failed
```

## Admin Flow

```text
Open Admin Dashboard
↓
View Sales Summary
↓
View Orders
↓
View Sync Status
↓
Retry Failed Sync If Needed
```

## Device Simulator Flow

### Barcode Scanner

```text
Keyboard Input or Simulator Window
↓
Barcode Entered
↓
Product Lookup
```

### Receipt Printer

```text
Receipt Text Generated
↓
Send to IReceiptPrinter
↓
Simulator Displays Printed Receipt
```

### Card Reader

```text
Payment Request
↓
ICardPaymentTerminal.AuthorizeAsync
↓
Simulator Returns Approved or Failed
```
