# Feature: Update profile information

**Labels:** `feature`, `profile`, `priority-medium`

## User story

As a logged-in user, I want to update my profile so that my account information stays current.

## Acceptance criteria

```gherkin
Feature: Profile Update

Scenario: Update profile with valid data
  Given I am logged in and on my profile page
  When I enter a new name and valid email
  And I click the "Save" button
  Then my profile is updated
  And I see a success message

Scenario: Update profile with an invalid email
  Given I am logged in and on my profile page
  When I enter an invalid email format
  And I click the "Save" button
  Then I see an error message "Please enter a valid email address"
  And my profile is not updated

Scenario: Change password with valid data
  Given I am logged in and on my profile page
  When I enter my current password
  And I enter a valid new password
  And I save the changes
  Then my password is updated
  And I can log in using the new password
```