# Feature: Suggest cheaper alternative products

**Labels:** `feature`, `recommendations`, `priority-medium`

## User story

As a bargain hunter, I want to see cheaper alternative products with lower brand quality so that I can save money if I am willing to compromise on quality.

## Acceptance criteria

```gherkin
Feature: Alternative Product Suggestions

Scenario: A cheaper mapped alternative exists
  Given I am viewing a product
  And a manually mapped similar product exists
  And the alternative has a lower price
  And the alternative has a lower brand quality tier
  When the product details load
  Then the alternative is shown as a suggestion
  And its price is displayed
  And its brand quality tier is displayed

Scenario: No cheaper alternative exists
  Given I am viewing a product
  And no manually mapped alternative exists at a lower price
  When the product details load
  Then no alternative suggestion is displayed

Scenario: Similarity comes from curated mappings
  Given an alternative product is displayed
  Then the suggestion is based on the configured product mapping
  And the system does not need to infer product similarity automatically
```