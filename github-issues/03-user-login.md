# Feature: Log in to the application

**Labels:** `feature`, `authentication`, `priority-high`

## User story

As a registered user, I want to log in with my email and password so that I can access my dashboard and watchlist.

## Acceptance criteria

```gherkin
Feature: User Login

Scenario: Successful login
  Given I am on the login page
  And I have a registered account
  When I enter my registered email
  And I enter the correct password
  And I click the "Login" button
  Then I am redirected to my dashboard
  And my authenticated session is established

Scenario: Login with invalid credentials
  Given I am on the login page
  When I enter an incorrect email or password
  And I click the "Login" button
  Then I see an error message "Invalid email or password"
  And I remain on the login page
```