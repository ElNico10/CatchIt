# Feature: Set a target price for a watchlisted product

**Labels:** `feature`, `watchlist`, `priority-high`

## User story

As a logged-in user, I want to set a target price for a watchlisted product so that I can identify when it becomes affordable.

## Acceptance criteria

```gherkin
Feature: Watchlist Target Price

Scenario: Set a valid target price
  Given I am logged in
  And a product is in my watchlist
  When I enter a valid target price
  And I save the target price
  Then the target price is saved for that watchlist entry
  And I see a success message

Scenario: Update an existing target price
  Given a watchlisted product has a target price
  When I enter a different valid target price
  And I save the change
  Then the new target price replaces the previous target price

Scenario: Reject an invalid target price
  Given a product is in my watchlist
  When I enter a zero, negative, or invalid target price
  Then I see a validation error
  And the existing target price is not changed
```