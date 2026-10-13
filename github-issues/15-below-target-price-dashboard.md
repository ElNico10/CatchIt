# Feature: Show watchlist products below target price

**Labels:** `feature`, `dashboard`, `watchlist`, `priority-high`

## User story

As a logged-in user, I want to see which watchlisted products are currently below my target price when I open my dashboard so that I do not need email or push notifications.

## Acceptance criteria

```gherkin
Feature: Price Drop Visibility

Scenario: Product is below its target price
  Given I am logged in
  And I have a watchlisted product with a target price of $50
  And the product's current price is $45
  When I open my dashboard
  Then the product appears in a "Below Target Price" section

Scenario: Product is above its target price
  Given I am logged in
  And I have a watchlisted product with a target price of $50
  And the product's current price is $55
  When I open my dashboard
  Then the product does not appear in the "Below Target Price" section

Scenario: No push or email notifications are required
  Given a watchlisted product falls below its target price
  When the user does not open the dashboard
  Then the system does not need to send a push notification or email
```