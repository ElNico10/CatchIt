# Deploying the CatchIt web app

This is the documented deployment process for issue #26. The web app (`src/CatchIt.Web`) is packaged as a
Docker image. Every push to `main` runs a GitHub Actions workflow that builds the image, checks it starts,
tags it, pushes it to Docker Hub, and then tells Render (free tier) to deploy that exact tag.

```
push to main ──> GitHub Actions: build ──> start + smoke test ──> push to Docker Hub ──> Render deploys that tag
                 (.github/workflows/docker-publish.yml)            <you>/catchit:<sha>     https://<service>.onrender.com
```

## The pieces

| File | What it does |
| --- | --- |
| `Dockerfile` | Builds the production image: publishes `CatchIt.Web`, seeds the simulated demo database into the image, and runs the app as a non-root user on the port in `$PORT` (8080 by default). Its `test` stage still runs the Core tests for CI. |
| `.github/workflows/docker-publish.yml` | On every push to `main`: build, start the container and smoke-test it, push `<you>/catchit:<short-sha>` and `:latest` to Docker Hub, then deploy that tag to Render. |
| `.github/workflows/ci.yml` | Tests on every pull request and push. Its `web-image` job builds and smoke-tests the image so a broken Dockerfile is caught before it reaches `main`. |
| `scripts/smoke-test.sh` | Waits until `/health` reports the expected version, then checks the database is reachable and the search, search-results and log-in pages load. |
| `GET /health` | Returns `{"status":"ok","version":"<short-sha>","products":<n>}`. `version` is the image tag, so it shows which image is live. |

## One-time setup

### 1. Docker Hub (about 5 minutes)

1. Create a free account at <https://hub.docker.com>.
2. **Account settings > Personal access tokens > Generate new token.** Name it `github-actions`, give it
   **Read & Write** access, and copy the token. It is shown only once.
3. You do not need to create the repository: the first push creates a public `catchit` repository.

### 2. GitHub settings (about 2 minutes)

In the GitHub repository, open **Settings > Secrets and variables > Actions**:

| Tab | Name | Value |
| --- | --- | --- |
| Secrets | `DOCKERHUB_TOKEN` | The Docker Hub access token from step 1. |
| Variables | `DOCKERHUB_USERNAME` | Your Docker Hub username. It is a variable, not a secret, so the image name stays readable in the logs. |

### 3. First publish

Merge `web-app` into `main` (or, if it is already there, use **Actions > Build and publish Docker image >
Run workflow**). The run publishes the image. Its last step, "Deploy this tag to Render", is skipped for now
because Render is not set up yet. Check that the image appears at
`https://hub.docker.com/r/<your-username>/catchit/tags`.

### 4. Render (about 10 minutes)

1. Create a free account at <https://render.com>.
2. **New > Web Service > Existing Image.** Image URL: `docker.io/<your-username>/catchit:latest`. Write it
   exactly like this, with `docker.io/` in front: the workflow's deploys only work when the image URL matches.
   The image is public, so no credentials are needed.
3. Name `catchit`, region Oregon, instance type **Free**.
4. **Environment variables:** add `PORT` = `8080`.
5. Under **Advanced**, set **Health Check Path** to `/health`.
6. Create the service and wait for it to show **Live**. Open `<service-url>/health`: it should answer
   `"status":"ok"` with the image's version.
7. In the service's **Settings**, copy the **Deploy Hook** URL. Treat it like a password.

### 5. Connect GitHub to Render (about 2 minutes)

Back in **Settings > Secrets and variables > Actions**:

| Tab | Name | Value |
| --- | --- | --- |
| Secrets | `RENDER_DEPLOY_HOOK_URL` | The deploy hook URL from step 4. |
| Variables | `DEPLOY_URL` | The service URL, for example `https://catchit.onrender.com` (no trailing slash). |

From now on every push to `main` publishes and deploys automatically. Run the workflow once by hand
(**Run workflow**) to check the whole chain works: the deploy step should end with "All checks passed".

## Deploying

- **Automatically:** merge into `main`. The run's summary page shows the published tag and the live URL.
- **By hand:** **Actions > Build and publish Docker image > Run workflow** on `main`.
- **Rolling back:** in Render, open the service's **Events** and redeploy an earlier image, or revert the bad commit on
  `main` so the next run publishes the fix.

A full run takes about 5 to 10 minutes. The workflow is limited to 30 minutes, the requirement in issue #26.

## Running the production image locally

Needs Docker:

```bash
docker build -t catchit-web .
docker run --rm -p 8080:8080 catchit-web
bash scripts/smoke-test.sh http://localhost:8080
```

Or run the published image: `docker run --rm -p 8080:8080 <your-username>/catchit:latest`.
Open <http://localhost:8080> and log in with `demo@catchit.test` / `Demo1234!`.

## Things to know about the free tier

- **The data resets.** Render's free services have no persistent disk, so the SQLite database is lost whenever the
  service restarts, redeploys or goes to sleep, and each start begins from the seeded demo data. Accounts
  registered on the live site and watchlist changes do not survive. All data is simulated demo data.
- **It sleeps when idle.** After 15 minutes without visitors the service spins down, and the next visit takes about
  a minute. Open the site a minute before a demo or before taking screenshots.
- **Logins end on restart.** The keys that sign the login cookie are not kept, so people are logged out after a
  deploy or a sleep.

## Troubleshooting

| Symptom | Likely cause |
| --- | --- |
| The publish run is skipped, or says "Set the DOCKERHUB_USERNAME repository variable" | The variable is missing, or was added under Secrets instead of Variables. |
| "Log in to Docker Hub" fails | `DOCKERHUB_TOKEN` is missing or wrong, or the token is Read-only. |
| "Push the image" says access denied | `DOCKERHUB_USERNAME` does not match the account the token belongs to. |
| Deploy step: curl returns 400 | Render's image URL is not exactly `docker.io/<your-username>/catchit:latest`. |
| Deploy step: curl returns 401 or 404 | The deploy hook was regenerated; copy the new one into the secret. |
| Smoke test times out waiting for the version | Render could not start the image: open the deploy in Render and read its log. |

## Evidence for the deployment assignment

| Evidence | Where to find it |
| --- | --- |
| Workflow path | `.github/workflows/docker-publish.yml` |
| Dockerfile | `Dockerfile` in the repository root |
| Successful run | **Actions > Build and publish Docker image >** the run. Expand "Build the Docker image" and "Push the image to Docker Hub". |
| Published tag | Docker Hub repository **Tags** tab: the short SHA tag matches the run's commit. |
| Same image on the platform | Render service **Settings** (image URL) and **Events** (deployed tag), plus `<url>/health` showing the same `version`. |
| Core feature | Search the live site for `headphones`: listings appear cheapest first, with marketplace, seller rating and price. |
