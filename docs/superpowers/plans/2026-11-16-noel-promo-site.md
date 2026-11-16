# Noël Promo Site Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish a Korean promotional site for Noël on GitHub Pages and a public v1.0.0 GitHub Release with downloadable Windows assets.

**Architecture:** Add a buildless, accessible static site under `site/` and deploy that directory through GitHub Actions Pages artifacts. Create a dedicated public Git repository inside the Noel project folder; keep the enclosing workspace repository untouched. Upload the existing 1.0.0 `Noel.exe` and `Noel.scr` plus their checksum as release assets.

**Tech Stack:** HTML5, CSS, existing PNG/ICO artwork, GitHub Actions Pages actions, GitHub CLI, PowerShell.

**Spec:** `docs/superpowers/specs/2026-11-16-noel-promo-site-design.md`

## Global Constraints

- Site content is Korean-first and presents the Noel screensaver, not the separate desktop Christmas-tree app.
- Use the existing midnight-teal and champagne-gold artwork and all five rendered themes.
- Keep the site static, keyboard accessible, responsive, reduced-motion aware, and free of third-party runtime dependencies or visitor tracking.
- Target public repository `cybereun/noel-christmas-screensaver` and Pages URL `https://cybereun.github.io/noel-christmas-screensaver/`.
- Release tag and binary version are `v1.0.0` / `1.0.0`; assets are `Noel.exe`, `Noel.scr`, and `SHA256SUMS.txt`.
- Do not stage or commit files from the enclosing `App_Bulid` repository.

## Review Focus

- GitHub project Pages serves under `/noel-christmas-screensaver/`, not at the domain root: use relative local image URLs and verify every preview loads there.
- Small screens can clip the gallery or hero: inspect at 375 px and confirm there is no horizontal overflow.
- Keyboard users must reach both downloads and scene links: keep semantic links, visible focus, and meaningful image alt text.
- Reduced-motion settings must quiet snow-like decorative transitions: honor `prefers-reduced-motion: reduce` and verify that branch.
- Release links must work without GitHub sign-in and match the local binaries: download `latest` assets anonymously and compare SHA-256 values.

---

### Task 1: Build the Korean landing page

**Files:**
- Create: `site/index.html`
- Create: `site/styles.css`
- Create: `tests/site-smoke.js` (dependency-free static-site regression check)
- Create: `site/assets/theme-1.png` through `site/assets/theme-5.png` by copying `docs/themes/theme-1.png` through `docs/themes/theme-5.png`
- Create: `site/assets/favicon.ico` by copying `assets/noel.ico`
- Modify: `README.md` to link to the promotional page and explain the public downloads

**Interfaces:**
- Consumes: the five approved PNG scene renders, `assets/noel.ico`, and verified saver features in `README.md` / `src/SettingsWindow.cs`.
- Produces: a static site rooted at `site/index.html`; CSS-local assets resolve from `site/assets/` and release links point at the latest GitHub Release assets.

- [x] **Step 1: Copy the original artwork into the static-site asset folder**

Create the exact five `site/assets/theme-N.png` files and `site/assets/favicon.ico`; compare copied bytes to their source files.

- [x] **Step 2: Write the semantic landing page in `site/index.html`**

Use sections for hero/download, five-scene gallery, verified feature summary, Windows apply steps, version/system requirements, and checksum. The primary button links to `https://github.com/cybereun/noel-christmas-screensaver/releases/latest/download/Noel.exe`; a second link targets `Noel.scr`. Use Korean alt text for the scene imagery, Korean document language, page metadata, and no repository-source promo link.

- [x] **Step 3: Implement the responsive visual system in `site/styles.css`**

Use midnight teal, champagne gold, warm ivory type, restrained snowfall/star motifs, clear focus rings, responsive image-led composition, and a `prefers-reduced-motion: reduce` media query. Avoid external fonts, libraries, or remote assets.

- [x] **Step 4: Add concise site/download guidance to the root `README.md`**

Link to the Pages URL and public release downloads; preserve build/run documentation and keep the unrelated enclosing README untouched.

- [x] **Step 5: Inspect the local static site**

Serve `site/` locally and check desktop and 375 px mobile layouts. Verify all five images (including when deployed under the repository subpath), favicon, anchors, keyboard focus, release-link targets, and reduced-motion styling in the page.

### Task 2: Configure GitHub Pages publishing

**Files:**
- Create: `.github/workflows/pages.yml`

**Interfaces:**
- Consumes: `site/` from Task 1 as the deployable static artifact.
- Produces: automatic GitHub Pages deployments for pushes to `main` and manual `workflow_dispatch` runs.

- [ ] **Step 1: Add the least-privilege Pages workflow**

Use `actions/checkout@v6`, `actions/configure-pages@v5`, `actions/upload-pages-artifact@v4` with `path: site/`, and `actions/deploy-pages@v4`. Grant `contents: read`, `pages: write`, and `id-token: write` only as required; use the `github-pages` deployment environment.

- [ ] **Step 2: Review the workflow for path and permission accuracy**

Confirm the artifact contains `index.html` plus every file it references and that the Windows application binaries are not embedded in the Pages artifact.

### Task 3: Create and push the Noel repository

**Files:**
- Modify: `.gitignore` to exclude `dist/`, `tests/bin/`, `tests/*.exe`, and `tests/renders/`
- Git repository: initialize only inside `ChristmasScreensaver/`

**Interfaces:**
- Consumes: the completed project source, documentation, site, and Pages workflow.
- Produces: public `origin` at `https://github.com/cybereun/noel-christmas-screensaver` with branch `main` containing only Noel project files.

- [ ] **Step 1: Ignore generated verification binaries and render captures**

Preserve existing ignore rules and add only generated executable and test-render patterns.

- [ ] **Step 2: Initialize and commit the dedicated repository**

Run `git init -b main` from `ChristmasScreensaver/`. Stage this folder's intended source, existing artwork/docs, specs, plan, site, and workflow only. Check `git status --short` before the first commit; generated `dist/` and test binaries must be absent.

- [ ] **Step 3: Create the public GitHub repository and push `main`**

Create `cybereun/noel-christmas-screensaver`, set it as `origin`, push `main`, and verify the remote HEAD equals the local commit. Enable GitHub Pages with workflow publishing before the deployment run.

### Task 4: Publish and verify v1.0.0 downloads

**Files:**
- Create: `dist/SHA256SUMS.txt` for release upload (kept out of Git because `dist/` is ignored)
- Git tag: `v1.0.0`
- GitHub Release: `v1.0.0`

**Interfaces:**
- Consumes: the pushed `main` and the existing `dist/Noel.exe`, `dist/Noel.scr` builds.
- Produces: public release metadata plus three anonymously downloadable assets and a successful Pages deployment.

- [ ] **Step 1: Calculate release hashes for the exact shipped files**

Write SHA-256 entries for `Noel.exe` and `Noel.scr` to `dist/SHA256SUMS.txt`; confirm the two binaries are byte-identical if expected and record sizes.

- [ ] **Step 2: Create and push the annotated release tag**

Tag the verified `main` commit as `v1.0.0` and push the tag.

- [ ] **Step 3: Create the public GitHub Release with all assets**

Publish release `v1.0.0` using `--verify-tag`, with concise Korean notes and exactly `Noel.exe`, `Noel.scr`, and `SHA256SUMS.txt` attached.

- [ ] **Step 4: Verify public deployment and downloads**

Wait for the Pages workflow to succeed; open the deployed URL and inspect the Korean hero, five loaded scene previews, and download links. Fetch both `releases/latest/download/` binary URLs without authentication into a temporary local folder; compare SHA-256 with `dist/SHA256SUMS.txt`. Confirm remote branch, tag, release asset names/sizes, and latest-release status.

---
