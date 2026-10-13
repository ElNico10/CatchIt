# Feature: Log out securely

**Labels:** `feature`, `authentication`, `priority-high`

## User story

As a logged-in user, I want to log out so that I can secure my account on shared devices.

## Acceptance criteria

```gherkin
Feature: User Logout

Scenario: Successful logout
  Given I am logged in
  When I click the "Logout" button
  Then I am redirected to the login page
  And my session is ended

Scenario: Dashboard cannot be accessed after logout
  Given I have logged out
  When I try to open the dashboard URL directly
  Then I am redirected to the login page

Scenario: Watchlist cannot be accessed after logout
  Given I have logged out
  When I try to open my watchlist URL directly
  Then I am redirected to the login page
```