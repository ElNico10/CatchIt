# Feature: Add and remove products from a watchlist

**Labels:** `feature`, `watchlist`, `priority-high`

## User story

As a logged-in user, I want to save products to a watchlist so that I can track them over time.

## Acceptance criteria

```gherkin
Feature: Watchlist

Scenario: Product is added to the watchlist
  Given I am logged in
  And I am viewing a product
  When I click "Add to Watchlist"
  Then the product appears in my watchlist

Scenario: Product is removed from the watchlist
  Given I am logged in
  And a product is already in my watchlist
  When I click "Remove from Watchlist"
  Then the product no longer appears in my watchlist

Scenario: Unauthenticated user attempts to add a product
  Given I am not logged in
  When I attempt to add a product to my watchlist
  Then I am prompted to log in
  And the product is not added to a user watchlist

Scenario: Duplicate watchlist entries are prevented
  Given a product is already in my watchlist
  When I attempt to add the same product again
  Then only one watchlist entry exists for that product
```