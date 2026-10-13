# Feature: Search the seeded product catalog

**Labels:** `feature`, `search`, `priority-high`

## User story

As a shopper, I want to search for a product by name or keyword so that I can compare available listings.

## Acceptance criteria

```gherkin
Feature: Product Search

Scenario: Search returns matching products
  Given I am on the search page
  And the product exists in the seeded catalog
  When I search using the product name or a matching keyword
  And I click the "Search" button
  Then I see matching product listings
  And each listing includes its marketplace and current price

Scenario: Search returns no results
  Given I am on the search page
  When I search for a product that is not in the seeded catalog
  And I click the "Search" button
  Then I see a message "No products found"

Scenario: Search uses simulated local data
  Given I perform a product search
  When the results are displayed
  Then the results come from the local seeded database
  And the page clearly identifies the data as simulated
```