# Feature: View product price history

**Labels:** `feature`, `product-data`, `priority-medium`

## User story

As a shopper, I want to view a product's price history so that I can judge whether the current price is a good deal.

## Acceptance criteria

```gherkin
Feature: Price History Display

Scenario: Product has sufficient price history
  Given I am viewing a product with at least 12 months of price data
  When I open the price history view
  And I select a 12-month range
  Then I see a chart showing the product's price over that period

Scenario: Product has limited price history
  Given I am viewing a product with less than one week of price data
  When I open the price history view
  Then I see a message "Not enough price history available"

Scenario: User selects a different time range
  Given price history exists for the selected period
  When I select an available time range
  Then the chart updates to show only that period
```