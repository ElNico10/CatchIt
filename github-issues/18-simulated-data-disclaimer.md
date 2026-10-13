# Feature: Display simulated data disclaimer

**Labels:** `feature`, `documentation`, `priority-high`

## User story

As a shopper, I want to see a disclaimer that prices and seller ratings are simulated so that I understand this is a demonstration application.

## Acceptance criteria

```gherkin
Feature: Simulated Data Disclaimer

Scenario: Disclaimer is visible on search results
  Given I am on the search results page
  When the results load
  Then I see a visible disclaimer stating that prices and seller ratings are simulated

Scenario: Disclaimer is visible on product details
  Given I am on a product detail page
  When the page loads
  Then I see a visible disclaimer stating that prices and seller ratings are simulated

Scenario: Disclaimer identifies the data source
  Given I am viewing simulated product data
  Then the interface explains that data comes from the local seeded catalog
  And the application does not imply that marketplace data is live
```