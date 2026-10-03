# TableIt

A hobby project to handle table orders in restaurants, to make it easier for staff to navigate orders, and for customers to place orders.

This is currently just a side project that i am working on when i feel like it.

## What it does

TableIt is a system for restaurants to organise their seating plan and their orders.

- **Floor staff (mobile).** Staff use their phones to take orders at the tables. They can see which orders belong to each table and follow each order's status.
- **Backend server.** Every order is sent to a central server, which stores it and forwards it to the kitchen.
- **Kitchen client (PC or iPad).** The kitchen client has two main functions:
  - **Order display for the night.** New orders are shown separately from orders that are in progress. Kitchen staff move each order along as they work: New → In progress → Ready. Floor staff are notified when an order is ready to serve.
  - **Floor planner.** A graphical editor for placing the restaurant's tables and setting how many seats each table has.
- **Menu editor.** The kitchen maintains the menu items that staff pick from when they take an order.

## Architecture

```
 Phone (Staff)                 TableItWeb (ASP.NET Core 8)                 PC / iPad (Kitchen)
┌──────────────┐  REST  ┌──────────────────────────────────┐  REST  ┌─────────────────────────┐
│ /Staff       │───────►│ API controllers                  │◄───────│ /Kitchen  (orders)      │
│              │        │   api/orders  api/tables api/menu│        │ /Kitchen/Planner        │
│              │◄───────│ SignalR hub  /hubs/restaurant    │───────►│ /Kitchen/Menu           │
└──────────────┘ events │ EF Core ──► SQLite (tableit.db)  │ events └─────────────────────────┘
                        └──────────────────────────────────┘
```

- **Clients are web pages served by the backend.** Any device with a browser can use them, so there is nothing to install. The server listens on all network interfaces, so phones on the restaurant's Wi-Fi can connect.
- **Changes go through REST.** Clients create and update data with HTTP calls (place an order, change its status, save the floor plan, edit the menu). The server is the single source of truth.
- **Live updates go through SignalR.** After every change, the server pushes an event to all connected screens: `OrderCreated`, `OrderUpdated`, `TablesChanged` or `MenuChanged`. This is how the kitchen sees new orders instantly, and how staff see when an order is ready.
- **Storage is SQLite via Entity Framework Core.** The database file is created and seeded with sample tables and menu items on first run.

### Order lifecycle

`New` → `InProgress` → `Ready` → `Served` (or `Cancelled`)

- An order copies the table number and each item's name and price at the moment it's placed. Later changes to the menu or floor plan don't alter past orders.
- "Tonight" means orders placed since 05:00 local time.

### Project layout

| Path | Purpose |
|---|---|
| `src/TableItShared/Models` | Shared models: `Table`, `MenuItem`, `Order`, `OrderLine`, and the request DTOs |
| `src/TableItWeb/Controllers` | REST API: `OrderController`, `TablesController`, `MenuController` |
| `src/TableItWeb/Hubs` | `RestaurantHub`, the SignalR hub that pushes live events |
| `src/TableItWeb/Data` | `TableItDbContext` (EF Core) and `SeedData` |
| `src/TableItWeb/Services` | `ServiceDay`, which works out when "tonight" starts |
| `src/TableItWeb/Pages` | Razor pages: `Staff`, `Kitchen` (orders), `Kitchen/Planner` and `Kitchen/Menu` |
| `src/TableItWeb/wwwroot/js` | Page scripts. `api.js` holds the shared fetch and SignalR helpers |
| `tests/TableItWeb.Tests` | xUnit tests: controller unit tests (in-memory SQLite and a fake SignalR hub) and HTTP/SignalR integration tests |

### API

| Method | Route | Description |
|---|---|---|
| GET | `/api/orders?tableId=&status=&all=` | Tonight's orders (all orders with `all=true`) |
| GET | `/api/orders/{id}` | A single order |
| POST | `/api/orders` | Place an order: `{ tableId, note, lines: [{ menuItemId, quantity, note }] }` |
| PATCH | `/api/orders/{id}/status` | Change an order's status: `{ status }` |
| GET / PUT | `/api/tables` | Get the floor plan, or save the whole layout |
| GET / POST | `/api/menu` | List all menu items, or add one |
| PUT / DELETE | `/api/menu/{id}` | Edit or delete a menu item |

## Running it

Requires the .NET 8 SDK.

```bash
dotnet run --project src/TableItWeb --launch-profile http
```

Then open `http://localhost:5269`. From a phone on the same network, use `http://<your-computer's-IP>:5269/Staff`.

The SQLite database (`tableit.db`) is created automatically. Delete it to reset the data or after changing the models; there are no migrations yet.

## Running the tests

```bash
dotnet test TableIt.sln
```

The tests cover placing orders and their validation, the order status flow, the "tonight" filter (05:00 cutoff), saving the floor plan, the menu editor, the seed data, and the JSON/SignalR contract the pages depend on. Each test uses its own database, so nothing touches `tableit.db`.

## Not yet implemented

- Login and staff roles
- Customers placing orders themselves
- Several floors or rooms
- Bills and payment
- Reservations
