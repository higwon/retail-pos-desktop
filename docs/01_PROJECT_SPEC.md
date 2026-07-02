# Project Specification

## Project Name

Retail POS Desktop

## Concept

A Windows desktop POS system for retail store operations.

The system should demonstrate realistic Windows application engineering, including local data persistence, offline-first sales processing, server synchronization, peripheral device simulation, and maintainable MVVM architecture.

## Why This Project Exists

The goal is not to build a simple CRUD application.
The goal is to build a portfolio-quality desktop system that shows practical engineering decisions for Windows-based business software.

## Main Users

### Cashier

- Logs into the POS client.
- Scans or searches products.
- Creates a cart.
- Applies discounts.
- Processes payment.
- Prints receipts.
- Continues work during network outages.

### Store Manager

- Reviews sales history.
- Checks stock.
- Manages products.
- Reviews synchronization status.

### System Administrator

- Manages users.
- Checks device status.
- Reviews failed synchronization records.

## Core Features

- Employee login
- Product search
- Barcode input
- Cart management
- Discount calculation
- Payment simulation
- Receipt printing simulation
- Local order storage
- Offline mode
- Pending synchronization queue
- Server synchronization
- Sales history
- Stock management
- Admin dashboard
- Device simulator

## Important Engineering Themes

- WPF desktop application design
- MVVM architecture
- Local database design
- Offline-first workflow
- Synchronization and retry
- REST API integration
- Socket/device communication simulation
- Background processing
- Multi-threading and async programming
- Error handling and logging
- Testable business logic

## Non-Goals

The project does not need to integrate with real payment providers.
The project does not need to use real POS hardware.
The project does not need to support every retail edge case from day one.
The project should not contain company-specific branding.

## MVP Definition

The MVP is complete when:

1. The WPF client runs.
2. Products can be searched and added to a cart.
3. An order can be completed.
4. The order is stored in local SQLite.
5. The order can be synchronized to the API.
6. The app can queue orders while offline.
7. Basic receipt text can be generated.
8. The core business logic has tests.
