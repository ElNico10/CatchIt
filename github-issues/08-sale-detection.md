# Feature: Identify products on sale

**Labels:** `feature`, `pricing`, `priority-medium`

## User story

As a shopper, I want products below their historical average to be flagged so that I can spot potential bargains quickly.

## Acceptance criteria

```gherkin
Feature: Sale Detection

Scenario: Current price is below the historical average
  Given a product's current price is below its 90-day average
  When I view the product in search results
  Then the product is flagged as "On Sale"

Scenario: Current price equals the historical average
  Given a product's current price equals its 90-day average
  When I view the product in search results
  Then the product is not flagged as "On Sale"

Scenario: Insufficient history exists
  Given a product does not have enough price history to calculate an average
  When I view the product
  Then the system does not incorrectly flag it as "On Sale"
```