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
Server Unavailable: Allow Offline Login only if supported later
```

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
