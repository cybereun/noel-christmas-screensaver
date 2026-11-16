# Noël design

The user requested a Christmas screensaver and selected a calm, luxurious tree and snow over the village alternatives. This is a separate application; the existing `christmas` desktop widget is preserved.

The composition uses a deep teal winter night, detailed evergreen boughs, champagne lights, subdued red/gold presents, slow snowfall and restrained serif lettering. Sound is off by design. The scene adapts to landscape, portrait and ultrawide windows. Small periodic offsets reduce stationary artwork; this is decorative software, not a guarantee against panel retention.

Implementation decision: native C# 5 / .NET Framework 4.8 WPF, built with the Windows compiler, with no downloaded packages. Native `.scr` protocol offers `/s`, `/c`, `/p HWND`. A settings executable shares the same binary. Rendering keeps the static scene cached and redraws particles. Multiple monitors receive independent windows. Input closes all saver windows; preview child windows follow their parent's lifetime and dimensions.

The five scenes are champagne-gold tree, snowy village, aurora/sleigh, winter train, and gingerbread house. The settings UI provides scene selection, snow amount, greeting, optional clock, frame rate, window/fullscreen previews, and an explicit Windows apply action. Applying copies to a stable per-user folder, selects that `.scr` in the current user's Windows setting, and opens the real Screen Saver Settings dialog; it preserves the existing timeout and secure-lock options. No registration occurs during ordinary launch or development validation.

Verification covers parser/settings behavior, true HWND child embedding and resize under aware/unaware hosts, render quality at different aspect ratios, and direct native-window inspection for fullscreen and input exit.
