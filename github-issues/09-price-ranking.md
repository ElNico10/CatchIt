# Feature: Rank listings by lowest price

**Labels:** `feature`, `search`, `priority-high`

## User story

As a shopper, I want search results ranked by lowest current price so that I can find the cheapest option first.

## Acceptance criteria

```gherkin
Feature: Price Ranking

Scenario: Results are sorted by lowest price
  Given I have searched for a product sold on multiple marketplaces
  When the results load
  Then the marketplace with the lowest current price appears first
  And the remaining results are ordered from lowest to highest price

Scenario: Two listings have the same lowest price
  Given two marketplaces list a product at the same lowest price
  When the results load
  Then both listings appear at the top of the results
  And neither listing is incorrectly ranked below the other
```