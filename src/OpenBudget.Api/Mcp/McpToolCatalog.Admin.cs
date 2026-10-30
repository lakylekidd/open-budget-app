using System.Text.Json;
using static OpenBudget.Api.Mcp.S;

namespace OpenBudget.Api.Mcp;

public static partial class McpToolCatalog
{
    private static IEnumerable<McpToolDefinition> SharingTools()
    {
        yield return Tool("sharing_get", "Get budget sharing", "Lists the budget manager, members, pending invitations and remaining invitation slots.",
            McpPermissions.AdminOnly, Effect.Read, Obj(),
            async c => Done(await c.Get("/api/budgets/{id}/sharing", Route.Of(("id", c.BudgetId!))), "Sharing details loaded."));

        yield return Tool("sharing_invite", "Invite budget member", "Invites someone by email to edit this budget (owner only). Existing verified users are added immediately.",
            McpPermissions.AdminOnly, Effect.Write, Obj(R("email", Str("Email address to invite.", 256, 3))),
            async c => Done(await c.Send("POST", "/api/budgets/{id}/invitations", new { email = c.Str("email") }, Route.Of(("id", c.BudgetId!))), "Invitation processed."));

        yield return Tool("sharing_remove_member", "Remove budget member", "Revokes a member's access to this budget (owner only).",
            McpPermissions.AdminOnly, Effect.Destructive, Obj(R("memberUserId", Id("Member user ID from sharing_get."))),
            async c => Done(await c.Send("DELETE", "/api/budgets/{id}/members/{memberUserId}", null, Route.Of(("id", c.BudgetId!), ("memberUserId", c.Str("memberUserId")))), "Member removed."));

        yield return Tool("sharing_cancel_invitation", "Cancel budget invitation", "Revokes a pending invitation (owner only).",
            McpPermissions.AdminOnly, Effect.Destructive, Obj(R("invitationId", Uuid("Invitation ID from sharing_get."))),
            async c => Done(await c.Send("DELETE", "/api/budgets/{id}/invitations/{invitationId}", null, Route.Of(("id", c.BudgetId!), ("invitationId", c.Str("invitationId")))), "Invitation cancelled."));
    }

    // Administrator operations. Admin tokens can only be created by, and only authenticate while their creator remains
    // in, the Administrator role; each endpoint's own role policy is evaluated again for every call.
    private static IEnumerable<McpToolDefinition> AdminTools()
    {
        static (string, System.Text.Json.Nodes.JsonObject, bool) UserIdArg() => R("userId", Id("User ID from users_list."));
        static McpToolDefinition Admin(string name, string title, string description, Effect effect, System.Text.Json.Nodes.JsonObject schema,
            Func<McpToolContext, Task<McpToolOutcome>> handler) => Tool(name, title, description, McpPermissions.AdminOnly, effect, schema, handler, budgetScoped: false);

        yield return Admin("admin_dashboard_get", "Admin dashboard", "Reads user/budget counts by status and membership, recent registrations and recent security events.",
            Effect.Read, Obj(), async c => Done(await c.Get("/api/admin/dashboard"), "Admin dashboard loaded."));

        yield return Admin("admin_audit_list", "Admin audit log", "Lists the 100 most recent administrative/security audit events.",
            Effect.Read, Obj(), async c => { var r = await c.Get("/api/admin/audit"); return Done(r, $"{Count(r.Body)} audit event(s)."); });

        yield return Admin("users_list", "List users", "Lists up to 100 users (newest first) with status and membership. Optional search text and status filter.",
            Effect.Read, Obj(O("q", Str("Username/email search.", 100)), O("status", Enum("Status filter.", "All", "Active", "Pending", "Locked", "Suspended"))),
            async c => { var r = await c.Get("/api/admin/users", query: Query(("q", c.OptStr("q")), ("status", c.OptStr("status")))); return Done(r, $"{Count(r.Body)} user(s)."); });

        yield return Admin("users_history", "User account history", "Lists the most recent lifecycle/security events for one user.",
            Effect.Read, Obj(UserIdArg()), async c => Done(await c.Get("/api/admin/users/{id}/history", Route.Of(("id", c.Str("userId")))), "User history loaded."));

        yield return Admin("users_invite", "Invite user", "Creates a user account and emails a verification link (no password is ever returned).",
            Effect.Write, Obj(R("userName", Str("Username.", 64, 1)), R("email", Str("Email address.", 256, 3)), O("membershipPlanId", Id("Membership tier, default free."))),
            async c => Done(await c.Send("POST", "/api/admin/users/invite", new { userName = c.Str("userName"), email = c.Str("email"), membershipPlanId = c.OptStr("membershipPlanId") ?? "free" }), "User invited."));

        yield return Admin("users_set_membership", "Set user membership", "Sets a user's membership tier, optional budget limit override, dates and billing references.",
            Effect.Write, Obj(R("userId", Id("User ID.")), R("membershipPlanId", Id("Membership tier ID from membership_plans_list.")),
                O("budgetLimitOverride", Nullable(Int("Active budget limit override (1-1000); null uses the tier limit.", 1, 1000))),
                O("startsOn", Date("Membership start date.")), O("expiresOn", Date("Membership end date.")),
                O("billingProvider", Str("Billing provider name.", 80)), O("externalSubscriptionId", Str("External subscription reference.", 160))),
            async c => Done(await c.Send("PUT", "/api/admin/users/{id}/membership", new
            {
                membershipPlanId = c.Str("membershipPlanId"), budgetLimitOverride = c.OptInt("budgetLimitOverride"), startsOn = c.OptStr("startsOn"),
                expiresOn = c.OptStr("expiresOn"), billingProvider = c.OptStr("billingProvider"), externalSubscriptionId = c.OptStr("externalSubscriptionId")
            }, Route.Of(("id", c.Str("userId")))), "Membership updated."));

        yield return Admin("users_approve", "Approve registration", "Approves a pending, email-verified registration.",
            Effect.Write, Obj(UserIdArg()), async c => Done(await c.Send("POST", "/api/admin/users/{id}/approve", null, Route.Of(("id", c.Str("userId")))), "Registration approved."));

        yield return Admin("users_reject", "Reject registration", "Rejects a pending registration (the account is suspended).",
            Effect.Destructive, Obj(UserIdArg()), async c => Done(await c.Send("POST", "/api/admin/users/{id}/reject", null, Route.Of(("id", c.Str("userId")))), "Registration rejected."));

        yield return Admin("users_suspend", "Suspend user", "Suspends a user: their sessions and agent tokens stop working immediately.",
            Effect.Destructive, Obj(UserIdArg()), async c => Done(await c.Send("POST", "/api/admin/users/{id}/suspend", null, Route.Of(("id", c.Str("userId")))), "User suspended."));

        yield return Admin("users_reactivate", "Reactivate user", "Reactivates a suspended user.",
            Effect.Write, Obj(UserIdArg()), async c => Done(await c.Send("POST", "/api/admin/users/{id}/reactivate", null, Route.Of(("id", c.Str("userId")))), "User reactivated."));

        yield return Admin("users_unlock", "Unlock user", "Clears a user's sign-in lockout.",
            Effect.Write, Obj(UserIdArg()), async c => Done(await c.Send("POST", "/api/admin/users/{id}/unlock", null, Route.Of(("id", c.Str("userId")))), "User unlocked."));

        yield return Admin("users_send_password_reset", "Send password reset", "Emails the user an expiring password reset link (the link/token is never returned).",
            Effect.Write, Obj(UserIdArg()), async c => Done(await c.Send("POST", "/api/admin/users/{id}/password-reset", null, Route.Of(("id", c.Str("userId")))), "Password reset email sent."));

        yield return Admin("users_set_entitlement", "Override user entitlement", "Enables or disables one entitlement for a user, overriding their membership tier.",
            Effect.Write, Obj(R("userId", Id("User ID.")), R("entitlementKey", Str("Entitlement key.", 80, 1, "^[a-z][a-z0-9._-]{0,79}$")), R("isEnabled", Bool("Enable or disable."))),
            async c => Done(await c.Send("PUT", "/api/admin/users/{id}/entitlements/{key}", new { isEnabled = c.Bool("isEnabled") },
                Route.Of(("id", c.Str("userId")), ("key", c.Str("entitlementKey")))), "Entitlement override saved."));

        yield return Admin("users_clear_entitlement", "Clear user entitlement override", "Removes a user's entitlement override so the membership tier applies again.",
            Effect.Write, Obj(R("userId", Id("User ID.")), R("entitlementKey", Str("Entitlement key.", 80, 1, "^[a-z][a-z0-9._-]{0,79}$"))),
            async c => Done(await c.Send("DELETE", "/api/admin/users/{id}/entitlements/{key}", null, Route.Of(("id", c.Str("userId")), ("key", c.Str("entitlementKey")))), "Entitlement override cleared."));

        yield return Admin("membership_plans_list", "List membership tiers", "Lists membership tiers and their budget limits.",
            Effect.Read, Obj(), async c => { var r = await c.Get("/api/admin/membership-plans"); return Done(r, $"{Count(r.Body)} membership tier(s)."); });

        yield return Admin("membership_plan_entitlements_get", "Get tier entitlements", "Lists the entitlements configured for a membership tier.",
            Effect.Read, Obj(R("membershipPlanId", Id("Membership tier ID."))),
            async c => Done(await c.Get("/api/admin/membership-plans/{id}/entitlements", Route.Of(("id", c.Str("membershipPlanId")))), "Tier entitlements loaded."));

        yield return Admin("membership_plan_entitlements_save", "Save tier entitlements", "Replaces the entitlements configured for a membership tier.",
            Effect.Write, Obj(R("membershipPlanId", Id("Membership tier ID.")), R("entitlements", Array("Entitlements.", Obj(
                R("entitlementKey", Str("Entitlement key.", 80, 1, "^[a-z][a-z0-9._-]{0,79}$")), R("isEnabled", Bool("Enabled."))), 200))),
            async c => Done(await c.Send("PUT", "/api/admin/membership-plans/{id}/entitlements", new { entitlements = c.Element("entitlements") },
                Route.Of(("id", c.Str("membershipPlanId")))), "Tier entitlements saved."));

        yield return Admin("admin_settings_registration_mode_get", "Get registration mode", "Reads whether self-registration is Disabled, ApprovalRequired or Open.",
            Effect.Read, Obj(), async c => Done(await c.Get("/api/auth/registration-mode"), "Registration mode loaded."));

        yield return Admin("admin_settings_registration_mode_set", "Set registration mode", "Sets self-registration to Disabled, ApprovalRequired or Open.",
            Effect.Write, Obj(R("mode", Enum("Registration mode.", "Disabled", "ApprovalRequired", "Open"))),
            async c => Done(await c.Send("PUT", "/api/admin/settings/registration-mode", new { mode = c.Str("mode") }), $"Registration mode set to {c.Str("mode")}."));

        yield return Admin("admin_settings_email_get", "Get email delivery settings", "Reads SMTP settings. The password is never returned; hasPassword says whether one is stored.",
            Effect.Read, Obj(), async c => Done(await c.Get("/api/admin/settings/email"), "Email settings loaded."));

        yield return Admin("admin_settings_email_update", "Update email delivery settings",
            "Updates SMTP settings. Omit password to keep the stored one. The password is encrypted at rest and never returned or logged.",
            Effect.Write, Obj(R("host", Str("SMTP host.", 255, 1)), R("port", Int("SMTP port.", 1, 65535)), R("from", Str("From address.", 256, 3)),
                O("username", Nullable(Str("SMTP username.", 256))), O("password", Str("SMTP password; omit to keep the stored one.", 512)), R("enableSsl", Bool("Use TLS."))),
            async c => Done(await c.Send("PUT", "/api/admin/settings/email", new
            {
                host = c.Str("host"), port = c.OptInt("port"), from = c.Str("from"), username = c.OptStr("username"), password = c.OptStr("password"), enableSsl = c.Bool("enableSsl")
            }), "Email settings updated."));

        yield return Admin("admin_settings_email_send_test", "Send test email", "Sends a delivery test message to the given address using the saved SMTP settings.",
            Effect.Write, Obj(R("to", Str("Recipient email address.", 256, 3))),
            async c => Done(await c.Send("POST", "/api/admin/settings/email/test", new { to = c.Str("to") }), "Test email sent."));
    }
}
