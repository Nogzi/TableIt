# TableIt

Table ordering for a single restaurant: floor staff take orders from guests at the tables on their phones, and the kitchen works through them on a shared screen.

## People and roles

**Staff member**:
Anyone with a TableIt account, whatever their role.
_Avoid_: User, employee, "staff" on its own

**Floor**:
The role for staff members who serve the tables and take orders.
_Avoid_: Staff (as a role name), waiter

**Kitchen**:
The role for staff members who prepare orders and move them through their statuses.
_Avoid_: Chef, cook

**Manager**:
The role for staff members who run the restaurant's setup: the team, the menu and the floor plan. A Manager can also do everything Floor and Kitchen can do.
_Avoid_: Admin, owner

**Guest**:
A person seated at a table, who the floor staff take orders from.
_Avoid_: Customer, diner

**Team**:
All the restaurant's staff members, active and deactivated, as the Manager administers them.
_Avoid_: Staff list, users

**Setup code**:
A short, single-use code that a Manager hands to a staff member so they can choose their own password.
_Avoid_: Temporary password, invite, reset token

## Menu

**Menu item**:
A dish or drink that can be ordered, with a name, category and price.
_Avoid_: Product, dish

**Sold out**:
A menu item that the Kitchen has withdrawn for the rest of the service day. Floor staff cannot add it to an order or an order change. It becomes available again automatically when the next service day starts.
_Avoid_: Disabled, unavailable, 86'd

**Off the menu**:
A menu item that a Manager has withdrawn until further notice. It stays withdrawn until a Manager puts it back.
_Avoid_: Disabled, hidden, deleted, sold out

## Orders

**Order**:
One set of order lines that floor staff send to the kitchen for one table at one time. A table can have several orders in one evening.
_Avoid_: Ticket, bill, check

**Order line**:
One menu item on an order, with a quantity and an optional note. The line keeps its own copy of the item's name and price as they were when the order was placed.
_Avoid_: Item, row

**Order status**:
Where an order is in the kitchen: New, In progress, Ready, Served or Cancelled. Served and Cancelled are final.
_Avoid_: State, stage

**Order change**:
A proposed difference to an order's lines or notes, including cancelling the whole order. Only New and In progress orders can be changed. Ready and Served orders cannot.
_Avoid_: Edit, amendment, modification

**Change request**:
An order change to an In progress order, which waits for the Kitchen to approve or deny it before it takes effect. An order change to a New order takes effect immediately and is not a change request. An order has at most one pending change request, and the order cannot move to Ready until the Kitchen decides on it. The staff member who sent it can withdraw it until then.
_Avoid_: Edit request, pending edit

**Floor alert**:
A message about one order that every Floor staff member sees until one of them acknowledges it after telling the guest. It is raised when the Kitchen denies a change request, or when an item in a New order is marked sold out.
_Avoid_: Notification, toast, message

**Service day**:
The restaurant's working day. It starts at a configured hour (05:00 by default), so a night that runs past midnight still counts as one service day.
_Avoid_: Tonight, today, shift
