# CatchIt

CatchIt is a deal-finder and price-history desktop app. Search for a product, compare its price across
marketplaces, see how that price has moved over time, spot sales, find cheaper or better-reviewed alternatives,
and keep a watchlist with target prices.

It is a Windows Forms app written in C# on .NET 10, with a local SQLite database. It was built by Group 13
(Nicholas Cruz and Daniel Ramirez) for WSU CPTS 322.

> ## Important: the prices are simulated
>
> **Every price, price history and seller rating in CatchIt is simulated demonstration data.** The app makes no
> calls to any marketplace or other live service, and nothing here reflects what any real store charges.
> Product names, brands and review ratings come from the local seeded catalog (see
> [Where the data comes from](#where-the-data-comes-from)). The app says so on the search results, the product
> detail window, the watchlist and the dashboard.

## What you can do

| Feature | What it does |
| --- | --- |
| **Search** | Type a product name, brand or keyword; results update as you type. Every word you type must match the start of a word in the product's name or keywords, ignoring case ("wireless head" finds "Apex Wireless Headphones"). Results are listings, cheapest first, with the marketplace, seller rating and price. Press Search for the full, exact search. |
| **Filters** | Narrow by brand quality (Budget, Standard, Premium) and by minimum seller rating. With no text, the filters browse the whole catalog. |
| **On Sale flag** | A listing is marked *On Sale* when its price is at least 10% below its 90-day average. It is never marked without enough history to compute an average. |
| **Product detail** | Double-click a result for the price, lowest and highest recorded price with dates, the 90-day average, ratings, and a price-history chart you can switch between 3 months, 6 months, 12 months and all history. A product with under a week of history shows "Not enough price history available". |
| **Alternatives** | Up to three suggestions on the detail window: the cheapest option (cheaper and a lower brand tier), a better-reviewed one, and one that may drop soon (its biggest seasonal price drop last year, named by sale event). A product that qualifies for two slots is shown once with both reasons. |
| **Accounts** | Register with an email and password, log in and out, and change your name, email and password on the profile screen. Passwords need at least 8 characters with a letter and a number and are stored only as BCrypt hashes. |
| **Account confirmation** | New accounts show a banner and can still use the app. Confirm with a 6-digit code (valid for 15 minutes). The code is delivered by a **simulated email**: a window that appears on screen, nothing is sent. |
| **Guest mode** | Search, prices, history, sale flags, filters and alternatives work without an account. The watchlist, dashboard and profile need one, because they save data. |
| **Watchlist** | Add a product from its detail window, remove it, and set an optional target price. Duplicates are not allowed and a target must be above $0. |
| **Dashboard** | Opens with a *Below Target Price* list of watched products whose lowest current price has reached the target, and a *Not at target yet* list sorted closest first. Each row shows the gap to the target and how the price compares with its 90-day average. There are no emails or push notifications. |

## Requirements

- Windows 10 or 11
- The [.NET 10 SDK](https://dotnet.microsoft.com/download) (to build and run from source)
- Git

No database server, account, API key or internet connection is needed to run the app.

## Setup and run

```bash
git clone https://github.com/ElNico10/CatchIt.git
cd CatchIt
dotnet build CatchIt.slnx
```

**1. Create the demo data** (do this once, before the first run):

```bash
src/CatchIt.App/bin/Debug/net10.0-windows/CatchIt.App.exe --seed
```

There is no console output. A message box appears and the program exits:

```
Database seeded with simulated demo data in X.Xs.
C:\Users\<you>\AppData\Local\CatchIt\catchit.db
```

(The seconds shown depend on your machine.) Seeding is safe to repeat; it does not create duplicates. It creates a demo
account, a small catalog of headphones, monitors, keyboards and speakers sold on four simulated marketplaces
(ShopMart, BargainBay, MegaStore, QuickBuy), about 400 days of simulated price history for each listing, and
alternative-product links.

**2. Start the app:**

```bash
src/CatchIt.App/bin/Debug/net10.0-windows/CatchIt.App.exe
```

or press F5 in Visual Studio, or run `dotnet run --project src/CatchIt.App`. Choose **Continue as guest**,
register a new account, or log in with the demo account:

| Email | Password |
| --- | --- |
| `demo@catchit.test` | `Demo1234!` |

The demo account is already confirmed. A newly registered account is not; its confirmation code appears in the
simulated email window.

To start over, close the app, delete `%LocalAppData%\CatchIt\catchit.db`, and seed again.

## Where the data comes from

- **The demo catalog** comes from the seeder (`--seed`), which writes fixed sample products and generates
  prices and seller ratings from a fixed random seed, so the same data is created every time.
- **A larger catalog (optional).** `--import` loads real product names, brands, review ratings and base prices
  from an [Amazon Reviews 2023](https://amazon-reviews-2023.github.io/) item-metadata file
  (`meta_<Category>.jsonl.gz`) and builds the rest (marketplace listings, seller ratings, price history, brand
  quality tiers and alternatives) as simulated data:

  ```bash
  src/CatchIt.App/bin/Debug/net10.0-windows/CatchIt.App.exe --import path\to\meta_Electronics.jsonl.gz --limit 100000
  ```

  `--limit` is optional; without it the whole file is loaded. The full Electronics file is about 1.3 GB,
  takes a few minutes and produces a database of several GB. **The dataset is not included in this repository**
  and is not needed to run the app. Check the dataset's own terms before using it.
- **Prices and ratings are never live.** Marketplace prices, price history and seller ratings are always
  generated by the app.

## Your data and privacy

Everything stays on your machine, in one SQLite file at `%LocalAppData%\CatchIt\catchit.db`. The app does not
use the network. Passwords are stored as BCrypt hashes, never in plain text, and a session ends when the app
closes.

## Tests

```bash
dotnet test CatchIt.slnx
```

The suite (about 420 tests, roughly 40 seconds) covers accounts and access rules, confirmation, profile and
password changes, search accuracy checked against an independent brute-force oracle, filters, price history,
sale detection, chart math, alternatives, the watchlist, the dashboard, and the bulk importers.

## Project layout

```
src/CatchIt.Core    Logic with no UI: Auth, Data (database, seeder, importers), Pricing, Search
src/CatchIt.App     The Windows Forms screens and Program.cs
tests/CatchIt.Tests xUnit tests
github-issues/      The user stories, one file each
```

## Not included

- **Password reset** (user story #4) is not built yet. Signed-in users can change their password from the
  profile screen.
- **Admin price refresh** (requirement #19) is not built yet. Prices change only when the data is seeded or imported.
- Requirements aimed at a hosted web app (HTTPS, many concurrent users, free-host page-load times, automated
  deployment) do not apply to a local desktop app and were not built.
