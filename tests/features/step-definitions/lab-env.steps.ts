import { Given, When, Then, DataTable } from '@cucumber/cucumber';
import { CustomWorld } from '../support/world';

// --- Given: authentication ---

Given('the user is authenticated', async function (this: CustomWorld) {
  return 'pending';
});

Given('the user is not authenticated', async function (this: CustomWorld) {
  return 'pending';
});

// --- Given: lab configuration ---

Given('the lab configuration contains gateways:', async function (this: CustomWorld, table: DataTable) {
  return 'pending';
});

Given('the lab configuration has an empty gateways array', async function (this: CustomWorld) {
  return 'pending';
});

Given('the lab configuration has gateways set to null', async function (this: CustomWorld) {
  return 'pending';
});

Given('the lab configuration section is missing entirely', async function (this: CustomWorld) {
  return 'pending';
});

Given('the appsettings.json contains a {string} section with {int} entries', async function (this: CustomWorld, section: string, count: number) {
  return 'pending';
});

// --- Given: frontend API stubs ---

Given('the API returns lab enabled false', async function (this: CustomWorld) {
  return 'pending';
});

Given('the API returns lab enabled true with {int} gateway', async function (this: CustomWorld, count: number) {
  return 'pending';
});

Given('the API returns lab enabled true with gateways:', async function (this: CustomWorld, table: DataTable) {
  return 'pending';
});

// --- When: API calls ---

When('the user calls GET \\/api\\/lab', async function (this: CustomWorld) {
  return 'pending';
});

When('a user with role {string} calls GET \\/api\\/lab', async function (this: CustomWorld, role: string) {
  return 'pending';
});

// --- When: frontend navigation ---

When('the user views the navigation', async function (this: CustomWorld) {
  return 'pending';
});

When('the user views the Lab page', async function (this: CustomWorld) {
  return 'pending';
});

// --- Then: API response status ---

// NOTE: "the response status is {int}" is defined in common.steps.ts

// --- Then: API response body ---

Then(/^the response body has "([^"]*)" equal to (true|false)$/, async function (this: CustomWorld, field: string, value: string) {
  return 'pending';
});

Then('the {string} array contains {int} entries with matching name and url', async function (this: CustomWorld, arrayName: string, count: number) {
  return 'pending';
});

Then('the gateway named {string} is present', async function (this: CustomWorld, name: string) {
  return 'pending';
});

Then('both responses return the same gateways list', async function (this: CustomWorld) {
  return 'pending';
});

// --- Then: frontend navigation visibility ---

Then('the Lab navigation item is not visible', async function (this: CustomWorld) {
  return 'pending';
});

Then('the Lab navigation item is visible', async function (this: CustomWorld) {
  return 'pending';
});

// --- Then: frontend link rendering ---

Then('each gateway is rendered as a clickable link', async function (this: CustomWorld) {
  return 'pending';
});

Then('each link opens in a new tab', async function (this: CustomWorld) {
  return 'pending';
});

Then('each link text shows the gateway name', async function (this: CustomWorld) {
  return 'pending';
});

Then('the link text shows {string}', async function (this: CustomWorld, text: string) {
  return 'pending';
});
