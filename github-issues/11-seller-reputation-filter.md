# Feature: Filter by seller reputation

**Labels:** `feature`, `search`, `priority-medium`

## User story

As a shopper, I want to filter products by seller reputation so that I can avoid unreliable sellers.

## Acceptance criteria

```gherkin
Feature: Seller Reputation Filter

Scenario: Filter returns matching results
  Given I have searched for a product
  When I set a minimum seller reputation of 4 out of 5
  Then only listings from sellers rated 4 or higher are shown

Scenario: Filter returns no matching results
  Given I have searched for a product with no sellers rated 4 or higher
  When I set a minimum seller reputation of 4 out of 5
  Then I see a message "No products match your filters"

Scenario: Seller ratings are identified as simulated
  Given I am viewing filtered search results
  Then the page clearly states that seller ratings are simulated
```