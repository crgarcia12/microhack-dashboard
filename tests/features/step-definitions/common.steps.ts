import { Given, When, Then } from '@cucumber/cucumber';
import { CustomWorld } from '../support/world';

// Shared step: click a button (used by auth, dashboard, and other features)
When('I click the {string} button', async function(this: CustomWorld, buttonLabel: string) {
  return 'pending';
});

// Shared step: assert response status code (used by challenges, lab-env, solutions, and other features)
Then('the response status is {int}', async function(this: CustomWorld, status: number) {
  return 'pending';
});

// Shared step: logged in as a participant on a team
Given('I am logged in as a participant on team {string}', async function(this: CustomWorld, teamName: string) {
  return 'pending';
});

// Shared step: logged in as a coach on a team
Given('I am logged in as a coach on team {string}', async function(this: CustomWorld, teamName: string) {
  return 'pending';
});

// Shared step: logged in as an organizer on a team
Given('I am logged in as an organizer on team {string}', async function(this: CustomWorld, teamName: string) {
  return 'pending';
});
