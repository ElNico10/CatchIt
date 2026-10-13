# Feature: Reset a forgotten password

**Labels:** `feature`, `authentication`, `priority-high`

## User story

As a registered user, I want to reset my password so that I can regain access if I forget it.

## Acceptance criteria

```gherkin
Feature: Password Reset

Scenario: Password reset requested for a registered email
  Given I am on the password reset page
  And an account exists for the supplied email
  When I enter my registered email
  And I click the "Reset Password" button
  Then I see a confirmation that a reset link has been generated or sent
  And the reset link is made available through the configured free or simulated mechanism

Scenario: Password reset requested for an unregistered email
  Given I am on the password reset page
  When I enter an email that is not linked to an account
  And I click the "Reset Password" button
  Then I see an error message "Email not found"

Scenario: Password is changed using a valid reset link
  Given I have a valid password reset link
  When I enter a new valid password
  And I submit the new password
  Then my password is updated
  And I can log in using the new password
```