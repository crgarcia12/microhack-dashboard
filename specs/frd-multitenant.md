# FRD: Multi-Tenant Microhack Model

**Feature ID:** multitenant  
**PRD Section:** Extension — Multi-tenant Microhack Operation Model  
**Status:** Draft  
**Dependencies:** `frd-auth` (identity and role resolution), downstream feature FRDs that consume scoped content/environment data

---

## 1. Overview

This feature defines a multi-tenant operating model where a single platform can host multiple microhacks in parallel. A tech lead can create and manage many microhacks, and each microhack behaves as an isolated tenant with its own users, environment context, content set, schedule, and lifecycle state (enabled/disabled).

The model ensures that a hacker can participate in exactly one microhack at a time, while tech leads can oversee all microhacks.

---

## 2. User Stories

### US-MT-01: Tech lead creates and manages microhacks

**As a** tech lead,  
**I want to** create and manage multiple microhacks,  
**so that** I can run multiple events from one platform.

**Acceptance criteria:**
- Given I am a tech lead, when I create a new microhack with valid required fields, then the microhack is available for management independently of other microhacks.
- Given I already manage microhack A, when I create microhack B, then both microhacks exist concurrently and are managed independently.
- Given I am a tech lead, when I update one microhack's settings, then only that microhack is affected.
- Given I am not a tech lead, when I attempt to create or manage microhacks, then access is denied.

### US-MT-02: Isolated users and environment per microhack

**As a** hacker,  
**I want to** access only my assigned microhack's users and environment,  
**so that** activity and data stay isolated between microhacks.

**Acceptance criteria:**
- Given I am assigned to microhack A, when I access user-scoped data, then I only receive data for microhack A.
- Given I am assigned to microhack A, when I attempt to access microhack B data directly or indirectly, then access is denied.
- Given two microhacks exist, when one microhack's environment settings change, then the other microhack's environment is unchanged.

### US-MT-03: Distinct content per microhack

**As a** tech lead,  
**I want to** configure different content for each microhack,  
**so that** each event can run its own challenge journey.

**Acceptance criteria:**
- Given multiple microhacks exist, when content is assigned to each microhack, then each microhack resolves only its own content.
- Given content is updated for one microhack, when users in another microhack access content, then they do not see those updates.

### US-MT-04: Independent run dates and schedules

**As a** tech lead,  
**I want to** set run dates and schedule windows per microhack,  
**so that** each microhack can run on its own timeline.

**Acceptance criteria:**
- Given two microhacks have different schedules, when current time falls inside one schedule and outside the other, then availability reflects each microhack's own schedule.
- Given a microhack schedule is changed, when the change is saved, then only that microhack's run window is updated.

### US-MT-05: Independent on/off control

**As a** tech lead,  
**I want to** turn each microhack on or off independently,  
**so that** I can pause or activate specific events without affecting others.

**Acceptance criteria:**
- Given multiple microhacks exist, when I disable one microhack, then other microhacks remain in their prior state.
- Given a microhack is disabled, when a hacker assigned to that microhack attempts to access event functionality, then access is blocked with a clear unavailable message.

### US-MT-06: Single active microhack membership per hacker

**As a** tech lead,  
**I want** each hacker to belong to only one microhack at a time,  
**so that** identity context is unambiguous.

**Acceptance criteria:**
- Given a hacker is already assigned to microhack A, when I assign the same hacker to microhack B without removing the existing assignment, then the assignment is rejected.
- Given I move a hacker from microhack A to microhack B through an explicit reassignment flow, then the hacker has exactly one active membership after the operation.

---

## 3. Functional Requirements

### Microhack Lifecycle Management

| ID | Requirement |
|----|-------------|
| MT-001 | The system SHALL support multiple microhacks, each with a unique identifier and name. |
| MT-002 | Only tech leads SHALL be allowed to create, update, enable, or disable microhacks. |
| MT-003 | Each microhack SHALL maintain independent lifecycle state (`enabled` or `disabled`). |
| MT-004 | Enabling, disabling, or updating one microhack SHALL NOT change any other microhack. |

### Tenant Isolation

| ID | Requirement |
|----|-------------|
| MT-010 | Each microhack SHALL have an isolated user scope and membership roster; non-techlead users are always evaluated within exactly one microhack context. |
| MT-011 | The system SHALL prevent cross-microhack data access for non-techlead users. |
| MT-012 | Each microhack SHALL have an isolated environment context and configuration boundary. |

### Content and Schedule Scope

| ID | Requirement |
|----|-------------|
| MT-020 | Each microhack SHALL support its own content set, independent of other microhacks. |
| MT-021 | Content retrieval for hackers SHALL be resolved from the hacker's assigned microhack only. |
| MT-022 | Each microhack SHALL support its own run dates/schedule window (start, end, and time zone context). |
| MT-023 | Availability for hackers SHALL be determined per microhack using both lifecycle state (`enabled`) and schedule window. |

### Membership Rules

| ID | Requirement |
|----|-------------|
| MT-030 | A hacker SHALL have at most one active microhack membership at any time. |
| MT-031 | Attempting to assign a hacker to a second microhack while an active membership exists SHALL be rejected. |
| MT-032 | Reassignment between microhacks SHALL result in exactly one active membership after completion. |

---

## 4. Conceptual Data Model

| Entity | Required Fields | Notes |
|--------|-----------------|-------|
| Microhack | `microhackId`, `name`, `enabled`, `scheduleStart`, `scheduleEnd`, `timeZone` | Represents one isolated tenant instance. |
| Membership | `userId`, `microhackId`, `active` | Enforces single active microhack membership per hacker. |
| Microhack Content Context | `microhackId`, `contentReference` | Points to the content set for the microhack. |
| Microhack Environment Context | `microhackId`, `environmentReference` | Points to environment-specific configuration for the microhack. |

---

## 5. Edge Cases

| ID | Scenario | Expected Behavior |
|----|----------|-------------------|
| MT-E01 | Tech lead attempts to create a microhack with a duplicate identifier or name | Creation is rejected with a clear uniqueness error. |
| MT-E02 | Microhack schedule end is before or equal to start | Save is rejected with a clear schedule validation error. |
| MT-E03 | Hacker already belongs to microhack A and is assigned to microhack B | Assignment is rejected unless explicit reassignment is performed. |
| MT-E04 | Microhack is disabled during an active event | Hackers in that microhack immediately lose event availability; other microhacks are unaffected. |
| MT-E05 | Microhack is enabled but current time is outside the configured schedule | Hackers in that microhack are blocked from event functionality until within schedule. |
| MT-E06 | Content reference is missing for a microhack | That microhack shows an explicit unavailable-content state without affecting other microhacks. |
| MT-E07 | Environment reference is missing or invalid for a microhack | That microhack reports environment unavailable while preserving isolation and stability for other microhacks. |

---

## 6. Error Handling

| Failure Mode | Expected System Response |
|--------------|--------------------------|
| Non-techlead attempts microhack management action | Deny the action and return an authorization error. |
| Duplicate microhack identifier/name on create | Reject the request with a validation error indicating conflict. |
| Invalid schedule values | Reject the request with a validation error describing the invalid range. |
| Cross-microhack access attempt by hacker | Deny the action and return an isolation/authorization error. |
| Hacker assigned to more than one active microhack | Reject the operation with a membership-conflict error. |
| Microhack unavailable (disabled or outside schedule) | Return an unavailable response with a clear user-facing message. |

---

## 7. Dependencies

| Dependency | Reason |
|------------|--------|
| `frd-auth` | Provides authenticated identity and role context needed to enforce tech lead permissions and hacker tenant scoping. |
| Content-related FRDs (`frd-challenges`, `frd-solutions`, `frd-credentials`) | Must consume microhack-scoped content context rather than global content. |
| Runtime/experience FRDs (`frd-timer`, `frd-lab-env`, `frd-dashboard`) | Must evaluate schedule, availability, and environment context per microhack. |

---

## 8. Out of Scope

- Billing and commercial tenant management
- Cross-microhack collaboration features
- Automatic migration of hackers between microhacks without explicit reassignment
