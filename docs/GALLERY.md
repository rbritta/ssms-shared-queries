# Private extension gallery (optional)

There is no official Microsoft marketplace for SSMS extensions, but Visual-Studio-shell
products can be served from a **private extension gallery**: an Atom feed (an XML file)
that lists your `.vsix` builds and their download URLs. Registering that one URL in the
IDE gives users in-product **install** and **update notifications**, which is the closest
thing to "install from an official repo".

> **SSMS caveat.** Private galleries are a first-class, documented feature in Visual
> Studio. SSMS 21/22 runs on a trimmed VS shell, and whether it surfaces the gallery UI
> (`Tools > Options > Environment > Extensions`) is not guaranteed across builds. Treat
> this as a bonus path; the [one-command installer](../README.md#quick-start) is the
> reliable option.

## How it works

```
GitHub Release (.vsix)  <-----  atom.xml feed  ----->  Tools > Options > Extensions
   the actual download          lists each version        users add the feed URL once
```

The feed is just metadata; the `.vsix` itself can stay on GitHub Releases. You host the
small `atom.xml` anywhere static - GitHub Pages is convenient because it is free and
already next to the code.

## Set it up

1. **Generate the feed.** Point a generator at a folder containing your `.vsix`:
   - [PrivateGalleryCreator](https://github.com/madskristensen/PrivateGalleryCreator) - drop
     `PrivateGalleryCreator.exe` next to the `.vsix` files and run it; it writes `feed.xml`.
   - or [VSGallery.AtomGenerator](https://github.com/garrettpauls/VSGallery.AtomGenerator).

   Edit each entry's `<content src="...">` to point at the GitHub Release asset URL, e.g.
   `https://github.com/rbritta/ssms-shared-queries/releases/download/v1.0.0/SsmsSharedQueries.vsix`.

2. **Host it.** Commit the feed to a `gh-pages` branch or a `/docs` folder and enable
   GitHub Pages, so it is served at e.g.
   `https://rbritta.github.io/ssms-shared-queries/atom.xml`.

3. **Register it (per user).** In SSMS (or Visual Studio):
   `Tools > Options > Environment > Extensions` (older shells: *Extensions and Updates*)
   `> Add` an additional gallery, name it "SSMS Shared Queries", and paste the feed URL.

4. **Install / update.** The gallery now appears under *Manage Extensions > Online*; new
   releases show up as available updates.

## Minimal feed shape

An Atom feed with one `<entry>` per version. The IDE reads the `Vsix` metadata (Id,
Version) and downloads the `<content>` URL:

```xml
<?xml version="1.0" encoding="utf-8"?>
<feed xmlns="http://www.w3.org/2005/Atom">
  <title type="text">SSMS Shared Queries</title>
  <id>https://rbritta.github.io/ssms-shared-queries/atom.xml</id>
  <entry>
    <id>SsmsSharedQueries.B50C448E-F6D4-49B3-8D15-CABE06BC75CB</id>
    <title type="text">SSMS Shared Queries</title>
    <content type="application/octet-stream"
             src="https://github.com/rbritta/ssms-shared-queries/releases/download/v1.0.0/SsmsSharedQueries.vsix" />
    <Vsix xmlns="http://schemas.microsoft.com/developer/vsx-syndication-schema/2010">
      <Id>SsmsSharedQueries.B50C448E-F6D4-49B3-8D15-CABE06BC75CB</Id>
      <Version>1.0.0</Version>
    </Vsix>
  </entry>
</feed>
```

See Microsoft's reference for the full schema:
[Create an Atom feed for a private gallery](https://learn.microsoft.com/en-us/visualstudio/extensibility/how-to-create-an-atom-feed-for-a-private-gallery).
