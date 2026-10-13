# Feature: Filter by brand quality tier

**Labels:** `feature`, `search`, `priority-medium`

## User story

As a shopper, I want to filter products by brand quality tier so that I can avoid brands below my standards.

## Acceptance criteria

```gherkin
Feature: Brand Quality Filter

Scenario: Filter returns matching results
  Given I have searched for a product
  When I select the "Premium" brand quality tier
  Then only products from premium-tier brands are shown

Scenario: Filter returns no matching results
  Given I have searched for a product with no premium-tier listings
  When I select the "Premium" brand quality tier
  Then I see a message "No products match your filters"

Scenario: Filter is cleared
  Given a brand quality filter is applied
  When I clear the filter
  Then listings from all available quality tiers are shown
```