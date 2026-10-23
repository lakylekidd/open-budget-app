# Milestone 2a — Multi-User Authentication & Administration

**Objective:** Transform OpenBudget into a secure multi-user application with independent budgets, administrative controls, configurable registration, and a foundation for subscription memberships.

**Priority:** High
**Dependencies:** Existing React UI, .NET 10 API, PostgreSQL and EF Core.

## 1. Authentication & Login

Implement authentication for all application users.

**Acceptance criteria**

- [x] A dedicated login screen is available at `/login`.
- [x] Users can log in using a username or email and password.
- [x] Authentication uses ASP.NET Core Identity or an equivalent secure identity framework.
- [x] Passwords are securely hashed and never stored in plaintext.
- [x] Sessions persist securely across page refreshes.
- [x] Users can log out and invalidate their session.
- [x] Unauthenticated users cannot access budgets or protected API endpoints.
- [x] Failed login attempts are rate-limited, with configurable account lockout.
- [x] Password reset and recovery are supported through secure, expiring, single-use tokens.
- [x] Suspended accounts cannot authenticate.
- [ ] Authentication works independently for multiple concurrent users.

## 2. Permanent Administrator Accounts

Provide separate administrator bootstrap configurations for development and production.

### Development

- Username: `admin`
- Password: `admin`
- Available exclusively in the Development environment.

### Production

Administrator credentials are configured through environment variables or a secrets provider.

Example configuration keys:

- `BootstrapAdmin__Username`
- `BootstrapAdmin__Email`
- `BootstrapAdmin__Password`

**Acceptance criteria**

- [x] Development automatically provisions the `admin/admin` account.
- [x] Production provisions its administrator using configured credentials.
- [x] Production startup fails safely if required initial administrator credentials are missing.
- [x] Default development credentials cannot authenticate in production.
- [x] Administrator provisioning is idempotent and does not overwrite changed passwords on restart.
- [ ] The permanent administrator cannot be deleted, suspended or demoted through the application.
- [ ] Administrator credentials can be changed securely.
- [x] Credentials and password hashes are never exposed in logs or API responses.

## 3. Registration & User Lifecycle

Registration must be controlled by administrators.

**Registration modes**

1. Disabled — only administrators can create or invite users.
2. Approval required — users can register but require administrator approval.
3. Open — users can register and activate their accounts after email verification.

The default is **Disabled**.

**Acceptance criteria**

- [x] Administrators can change the registration mode.
- [x] Registration is unavailable when disabled, including through direct API requests.
- [x] The registration screen validates required inputs and duplicate usernames/emails.
- [x] Accounts awaiting approval cannot authenticate or access protected APIs.
- [x] Administrators can approve or reject pending registrations.
- [x] Email verification is required before new accounts become active.
- [ ] Users can update their profile and change their password.
- [ ] Account statuses support `Pending`, `Active`, `Locked` and `Suspended`.
- [ ] User registration and account status changes are audited.

## 4. Separate Administration Panel

Create an independent administrative interface at `/admin`.

The panel must have its own layout, navigation and functionality, separate from the budgeting application.

### 4.1 Dashboard

Display administrative statistics:

- Total registered users
- Active, pending, locked and suspended users
- Total budgets and active budgets
- Users by membership tier
- Recent registrations and security events

### 4.2 User Management

Administrators can:

- Search, filter and inspect users.
- Create accounts and invite users.
- Approve registrations.
- Suspend or reactivate accounts.
- Unlock accounts after failed login attempts.
- Initiate secure password resets.
- Assign membership tiers.
- Configure individual budget-limit overrides.
- View account creation, last login and status history.

### 4.3 Application Settings

Administrators can configure:

- Registration mode
- Default membership tier
- Default budget limits
- Security and lockout policies
- Application-wide settings intended for future growth

**Acceptance criteria**

- [x] `/admin` uses a separate interface with its own navigation.
- [x] The dashboard shows user status and budget counts, users by membership tier, recent registrations and security audit events without exposing financial data.
- [x] Administrative APIs require an authenticated administrator; other sessions see an access-denied page.
- [x] Ordinary users receive HTTP 403 when accessing administrative APIs.
- [x] Every implemented administrative action is enforced by backend authorization.
- [ ] Administrators can search users and perform the listed management actions.
- [x] Unlocking an account does not unintentionally reactivate a suspended account.
- [ ] Administrative password resets never reveal an existing password.
- [x] Critical implemented operations require confirmation.
- [x] Registration policy and user lifecycle changes are recorded in an audit log containing actor, action, target and timestamp.
- [ ] Administrators cannot read users' private budget information by default.

## 5. Multiple Budgets Per User

Each user can independently create and manage multiple budgets.

### Functionality

- Budget selector in the main application.
- Create a new budget.
- Rename an existing budget.
- Switch between budgets.
- Archive and restore budgets.
- Delete budgets with explicit confirmation.
- Maintain independent categories and monthly snapshots per budget.

**Acceptance criteria**

- [x] Every newly created budget has a globally unique identifier and an owner; the legacy budget is assigned to the configured bootstrap administrator.
- [x] A user can own multiple budgets subject to the assigned membership limit.
- [ ] Each budget maintains independent months, groups, categories, ledger and planning data. (Plan snapshots are scoped; ledger/planning remain shared.)
- [x] Users can switch budgets without losing existing data.
- [x] Budget selection persists across page refreshes.
- [x] Users can create and rename owned budgets from the main UI.
- [x] Users can archive, restore and explicitly delete owned budgets from the main UI; the legacy budget is protected.
- [x] Budget-management API reads and writes enforce ownership on the server.
- [x] Users cannot access another user's budget by manipulating budget-management API requests.
- [x] Concurrent budget creation cannot bypass the active-budget limit.
- [x] Archiving a budget does not destroy its data.
- [x] Archived budgets do not count toward the active-budget limit.
- [x] Decreasing a user's limit does not delete existing budgets; it only prevents additional active budgets.
- [x] Existing default-budget data is assigned to the configured bootstrap administrator without changing legacy account or transaction rows.
- [ ] Production users start with an empty budget unless they explicitly select a template.

## 6. Membership & Budget Limits

Introduce membership tiers now so payment integration can be added later.

### Initial configurable tiers

| Tier | Default active budgets | Purpose |
|---|---:|---|
| Free | 1 | Basic membership |
| Plus | 3 | Future paid membership |
| Pro | Unlimited | Future premium membership |

These are initial suggested defaults, editable through the admin panel.

Memberships are separate from authentication roles. An administrator may have a membership, but administrative permissions are not determined by the membership tier.

**Acceptance criteria**

- [x] Membership tiers are stored in the database rather than hard-coded in the UI.
- [x] Each registered user has an assigned membership tier.
- [x] Membership tiers define maximum active budgets.
- [x] Administrators can assign or change memberships.
- [x] Administrators can override an individual user's budget limit.
- [x] The API enforces the effective budget limit for creation and restoration.
- [x] When a limit is reached, the application displays the server's explanation.
- [ ] Future subscription metadata can support start date, expiration date, billing provider and external subscription ID.
- [ ] An entitlement model supports future restrictions on additional features without redesigning authorization.
- [ ] No payment processor integration is required for Milestone 2a.

## 7. Database & API Changes

### Proposed entities

| Entity | Responsibility |
|---|---|
| `Users` | Authentication and user profiles |
| `Roles` | Administrator and user permissions |
| `BudgetPlans` | Budget identity, owner and metadata |
| `BudgetMonths` | Monthly records belonging to a budget |
| `MembershipPlans` | Available tiers and entitlements |
| `UserMemberships` | User's assigned membership |
| `UserEntitlementOverrides` | Individual quota exceptions |
| `ApplicationSettings` | Registration and global settings |
| `AdminAuditLogs` | Administrative activity |

Existing categories and groups must be updated to reference the appropriate budget and month.

### API requirements

- Authentication and account-management endpoints.
- Authenticated budget CRUD endpoints.
- Budget-specific monthly read/write endpoints.
- Administrator-only user and settings endpoints.
- Membership management endpoints.
- Server-side authorization and input validation.
- Structured API error responses.

**Acceptance criteria**

- [ ] EF Core migrations introduce the new model while preserving existing development budget data.
- [x] Monthly records are uniquely identified within a budget rather than globally by month.
- [ ] Existing monthly budget APIs are migrated to budget-scoped routes.
- [ ] All protected endpoints enforce identity, role and resource ownership.
- [ ] Budget write operations have optimistic concurrency protection against stale updates.
- [ ] Startup and migrations remain compatible with the existing Aspire development environment.

## 8. Security & Quality Assurance

**Acceptance criteria**

- [ ] Authentication uses secure HttpOnly cookies with appropriate SameSite, CSRF and HTTPS protections.
- [ ] Passwords and bootstrap credentials are never committed to source control.
- [ ] Session invalidation works after logout, suspension and security-sensitive account changes.
- [ ] Automated integration tests verify login, registration controls, lockout, account suspension and administrative authorization.
- [ ] Automated tests verify cross-user budget access is denied for read and write operations.
- [ ] Automated tests verify membership limits, concurrent creation and budget restoration.
- [ ] Production and development bootstrap configurations are tested separately.
- [ ] Security-sensitive actions are logged without exposing passwords or financial data.
- [ ] Existing planning functionality continues to work after the migration.

## 9. Out of Scope

The following capabilities are intentionally deferred:

- Payment processing and checkout
- Subscription renewals and invoices
- Automated billing and payment webhooks
- Family/household budget sharing
- Social login and SSO
- Administrator impersonation of users
- Advanced analytics and revenue reporting
- Self-service subscription upgrades and downgrades

## 10. Definition of Done

Milestone 2a is complete when:

1. Multiple users can register under administrator-controlled policies and authenticate securely.
2. Development and production each have their correctly configured permanent administrator.
3. Administrators can manage users, account status, registration and memberships through a separate admin panel.
4. Users can own and switch between multiple independent budgets.
5. Membership-based budget limits are configurable and enforced by the backend.
6. Users' budgets and financial information are securely isolated.
7. Existing data and planning functionality survive the database migration.
8. All acceptance tests pass in development and production-equivalent environments.
