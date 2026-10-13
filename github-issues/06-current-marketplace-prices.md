# Feature: Display marketplace prices

**Labels:** `feature`, `product-data`, `priority-high`

## User story

As a shopper, I want to see the current price on each marketplace so that I can compare products at a glance.

## Acceptance criteria

```gherkin
Feature: Current Price Display

Scenario: Product is listed on multiple marketplaces
  Given I have searched for a product listed on multiple marketplaces
  When the results load
  Then each listing shows the marketplace name
  And each listing shows its current price

Scenario: Product is listed on one marketplace
  Given I have searched for a product listed on one marketplace
  When the results load
  Then the listing shows the marketplace name
  And the listing shows its current price

Scenario: Product has no current price
  Given a product has no current price in the database
  When I view the product
  Then the system displays an appropriate unavailable-price message
  And the page does not show a misleading price
```