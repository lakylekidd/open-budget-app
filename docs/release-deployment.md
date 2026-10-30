# GitHub Release to Coolify

This repository uses GitHub Actions to verify pull requests and deploy a Coolify Compose resource when a GitHub Release is published.

## 1. Add the GitHub workflows

Keep these workflows in `.github/workflows/`:

- `test.yml`: runs on pull requests and pushes to `main`. It restores, builds, tests, and builds the UI.
- `release.yml`: runs when a release is published. It checks out the release tag, runs the same verification, then sends an authenticated `POST` request to Coolify.

In GitHub, open **Settings → Branches → Branch protection rules** for `main` and require the check named **Build and test / verify** before merging.

## 2. Configure Coolify

Create a **Docker Compose** resource from the repository and use the root `docker-compose.yml` file.

In the Compose resource:

1. Set `POSTGRES_PASSWORD` under **Environment Variables**.
2. Set `BootstrapAdmin__Username`, `BootstrapAdmin__Email`, and `BootstrapAdmin__Password` under **Environment Variables**. Use a unique username, an administrator email address, and a strong password; store the password as a secret. The Compose file requires these values, and the API uses them to create the initial administrator after applying migrations on the first deployment. Keep the values configured for later starts; they do not overwrite a changed password.
3. The Compose file creates a persistent `openbudget-data-protection` volume for Identity tokens and encrypted SMTP credentials. Do not delete this volume during a redeploy. Configure SMTP after deployment in **Administration → Email delivery**, then send a test email before enabling registration. Runtime `Smtp__*` variables remain available as a fallback when no in-app SMTP configuration is saved.
3. Add the application domain to the `ui` service on port `80`.
4. Keep the `openbudget-data` volume.
5. Confirm the Compose file is saved and deploy once manually.

The Compose file contains health checks for PostgreSQL, the API, and the UI. The API check calls `/health`, which verifies database connectivity. The UI check reaches the same endpoint through nginx.

`deploy/nginx.conf` serves the UI as a single-page app: `/api/`, `/mcp` and `/health` go to the API, other API-shaped paths return 404, and every other path (for example `/{budget-slug}/plan/2026-11` or `/{budget-slug}/account/visa`) falls back to `index.html` so deep links and refreshes work. Keep that fallback when changing the proxy; see [URL routing](architecture.md#url-routing).

Keep the API reachable only from the Compose network behind the `ui` nginx proxy. Nginx forwards the original Host and HTTPS scheme so the API can set Secure cookies and validate Origin/Referer on authenticated writes. Do not publish the API port as a public service.

## 3. Create the Coolify webhook

In Coolify, open the resource’s **Webhooks** or deployment webhook settings and copy the deploy URL. The URL must be the resource’s deploy endpoint, for example:

```text
https://coolify.example.com/api/v1/deploy?uuid=<resource-uuid>&force=false
```

Create a Coolify team API token with permission to deploy this resource. Do not put the token in the repository or workflow file.

## 4. Add GitHub secrets

In GitHub, open **Settings → Secrets and variables → Actions** and add:

| Secret | Value |
|---|---|
| `COOLIFY_WEBHOOK_URL` | Coolify’s deploy webhook URL |
| `COOLIFY_TOKEN` | Coolify API token with deploy permission |

The release workflow sends the token as `Authorization: Bearer <token>` and uses `POST`. A missing secret fails the deploy job clearly.

## 5. Publish a release

After merging the desired changes into `main`:

1. Open **GitHub → Releases → Draft a new release**.
2. Create a new tag, such as `v0.1.1`, from `main`.
3. Add release notes and publish it.
4. GitHub runs `Release and deploy`.
5. The `verify` job must pass before the `deploy` job calls Coolify.
6. Check Coolify’s deployment output and service health after the webhook is accepted.

Use a new version for new code. To retry an existing release after fixing the workflow, use **Actions → Release and deploy → Run workflow** and enter its existing tag. This rechecks that tag before requesting deployment.

The Coolify resource is configured to deploy its selected Git ref. If it is set to `main`, the webhook deploys the current `main` branch; the release workflow still verifies the release tag before triggering it.

## Useful checks

- Application readiness: `GET https://<app-domain>/health`
- GitHub workflow: **Actions → Release and deploy**
- Coolify: open the resource and inspect deployment logs and container health
- PostgreSQL data survives redeploys through the `openbudget-data` volume
