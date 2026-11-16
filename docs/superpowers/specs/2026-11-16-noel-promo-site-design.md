# Noël Christmas Screensaver Promo Site — Design

## Intent

Present Noël as a calm, premium-feeling Windows Christmas screensaver and help Korean Windows users preview the five scenes, understand how to apply one, and download the app.

## Goals

- Publish the app source, promotional site, and versioned release in one dedicated public repository: `cybereun/noel-christmas-screensaver`.
- Publish a responsive Korean landing page on GitHub Pages, with a direct link to the latest Windows download.
- Use the app's original champagne-tree artwork and five rendered scene previews as the visual identity.
- Offer the settings app (`Noel.exe`), screensaver file (`Noel.scr`), and SHA-256 checksum as release assets.
- Match release `v1.0.0` to the current app's 1.0.0 binary version.
- Keep the existing private `void-harvest` repository and separate public `christmas-tree-desktop` product untouched.

## Non-goals and anti-goals

- Do not change the screensaver's implementation or its current Windows selection as part of the web work.
- Do not add accounts, analytics, visitor counts, testimonials, invented customer claims, or unrelated site features.
- Do not expose personal contact details or add a separate repository/source-code promotion link to the page. The requested public GitHub release download links will resolve to the new repository's release assets.

## Approaches considered

1. **A dedicated public Noël repository (chosen).** Keep this screensaver's source, static Pages site, Pages workflow, and releases together. This gives the product a stable latest-release download URL and avoids disturbing other repos.
2. **Add Noël to `christmas-tree-desktop`.** It is already public, but contains a separate desktop-tree product and existing releases; combining them would blur product scope and release identity.
3. **Push to `void-harvest`.** This is the current local Git remote and is private. It is a mixed workspace repo, so it is not an appropriate home for the requested public Pages site and anonymous release download.

## Experience and content

1. **Hero:** midnight teal background, restrained champagne-gold accents, original tree artwork, Korean headline, and a primary Windows download button.
2. **Five scenes:** an image-led gallery for the champagne-gold tree, snowy village, aurora and Santa, winter train, and gingerbread village. Each image uses the existing full-resolution scene render; the current champagne-gold tree remains identified as the default.
3. **A quiet screensaver:** explain only verified behavior: gentle snowfall, optional greeting/clock, three snow levels, three frame-rate options, and silent operation.
4. **Apply in Windows:** a short three-step guide: run `Noel.exe`, select a scene, then choose **선택한 장면 Windows에 적용**. State that Windows lists the selected saver as **Noel** and that wait/lock preferences are set in Windows.
5. **Download details:** buttons for `Noel.exe` and `Noel.scr`, SHA-256 link, Windows 10/11 x64 and .NET Framework 4.8 requirements, and the latest release link.

The site is Korean-first, keyboard accessible, reduced-motion aware, and usable on desktop and mobile. It uses buildless HTML/CSS/JS with no third-party runtime dependencies or tracking.

## Delivery and verification

- Commit only the Noel project to the dedicated repository; do not stage files from the enclosing workspace repository.
- Deploy `site/` through a GitHub Actions Pages workflow using the official Pages artifact/deploy actions.
- Publish tag and release `v1.0.0` with `Noel.exe`, `Noel.scr`, and `SHA256SUMS.txt`.
- Verify the pushed commit and tag, successful Pages workflow and live URL, public latest-release metadata, and anonymous downloads whose SHA-256 matches the local binaries.
- Promote the landing page and release asset links from the site; do not claim deployment or download success until checked live.

## Remaining implementation choices

- The dedicated repository slug, Korean copy details, and exact gallery layout are implementation choices following this direction.
- The public repository will contain the source as the user requested; the page itself will focus on the product and downloads.
