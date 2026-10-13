# Feature: Register an account

**Labels:** `feature`, `authentication`, `priority-high`

## User story

As a new user, I want to register with my email and password so that I can create an account.

## Acceptance criteria

```gherkin
Feature: User Registration

Scenario: Successful registration with valid details
  Given I am on the registration page
  When I enter a valid email address
  And I enter a password meeting the minimum requirements
  And I click the "Register" button
  Then my account is created
  And my account is marked as unconfirmed
  And I see a message explaining how to confirm my account

Scenario: Registration with an invalid email
  Given I am on the registration page
  When I enter an invalid email address
  And I click the "Register" button
  Then I see an error message "Please enter a valid email address"
  And my account is not created

Scenario: Registration with an existing email
  Given an account already exists for my email address
  When I attempt to register with the same email
  Then I see an error explaining that the email is already registered
  And no duplicate account is created

Scenario: Registration with an invalid password
  Given I am on the registration page
  When I enter a password that does not meet the minimum requirements
  And I click the "Register" button
  Then I see an error explaining the password requirements
  And my account is not created
```

## Definition of done

- Passwords are stored using bcrypt with a cost factor of at least 10.
- Validation errors are displayed within one second of submission.
- Automated tests cover successful and failed registration.
