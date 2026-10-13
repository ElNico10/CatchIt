# Feature: Display account confirmation status

**Labels:** `feature`, `authentication`, `priority-medium`

## User story

As a newly registered user, I want to know whether my account has been confirmed so that I understand its current status.

## Acceptance criteria

```gherkin
Feature: Registration Confirmation

Scenario: Unconfirmed account displays a banner
  Given I have registered with a valid email and password
  And my account is unconfirmed
  When I log in
  Then I see a banner prompting me to confirm my account
  And I can still use the application

Scenario: Confirmed account does not display a banner
  Given my account has been confirmed
  When I log in
  Then I do not see the confirmation banner

Scenario: Confirmation uses a free or simulated mechanism
  Given I am using the application in a development or demonstration environment
  When I request account confirmation
  Then the system uses the configured free or simulated confirmation mechanism
  And the application does not require a paid email provider
```