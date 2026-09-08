# Creating Animated Themes

GlueDock themes are JSON files stored in:

`GlueDock_Themes`

To create your own theme:

1. Copy an existing `.json` theme file.
2. Rename the copied file, for example `MyTheme.json`.
3. Change the `"Name"` value inside the file.
4. Edit the colors and animation settings.
5. Restart GlueDock or reopen the theme list.

The file name is used as the internal theme ID.

Example:

    {
      "Name": "My Theme"
    }

Colors use ARGB hexadecimal values:

`#AARRGGBB`

Example:

`#FFFFFFFF`

This means fully opaque white.

---

# Example 1 - Simple Animated Gradient

This is the easiest animated theme.

It uses an animated glass gradient without particles.

    {
      "Name": "Blue Motion",

      "DockBackgroundColor": "#101525",
      "ItemBackgroundColor": "#22FFFFFF",
      "ItemBorderColor": "#36FFFFFF",
      "TextColor": "#FFFFFFFF",
      "SubmenuIndicatorColor": "#FFFFFFFF",
      "DragGhostBackgroundColor": "#32FFFFFF",
      "DragGhostBorderColor": "#80FFFFFF",

      "GlassSurfaceEnabled": true,

      "GlassTopColor": "#884A90E2",
      "GlassBottomColor": "#88204080",
      "GlassHighlightColor": "#70FFFFFF",
      "GlassShadowColor": "#50000000",

      "GlassGradientStartX": 0.2,
      "GlassGradientStartY": 0.0,
      "GlassGradientEndX": 0.8,
      "GlassGradientEndY": 1.0,

      "GlassGradientAnimationEnabled": true,
      "GlassGradientAnimationDurationSeconds": 8.0,
      "GlassGradientAnimationAutoReverse": true,
      "GlassGradientAnimationEasing": "Linear",
      "GlassGradientAnimationEasingMode": "EaseInOut",
      "GlassGradientAnimationMode": "Points",

      "GlassGradientAnimationToStartX": 0.8,
      "GlassGradientAnimationToStartY": 0.0,
      "GlassGradientAnimationToEndX": 0.2,
      "GlassGradientAnimationToEndY": 1.0,

      "GlassCornerRadius": 18,
      "GlassExtraThickness": 14
    }

## Important properties

### `GlassGradientAnimationEnabled`

Enables the gradient animation.

### `GlassGradientAnimationDurationSeconds`

Controls the animation speed.

Higher values produce slower movement.

### `GlassGradientAnimationAutoReverse`

If set to `true`, the gradient moves back and forth.

### `GlassGradientAnimationMode`

`"Points"` animates the gradient coordinates.

---

# Example 2 - Rotating Gradient with Glowing Stars

This example adds generated animated particles.

GlueDock supports the particle renderer:

`GlowDot`

Example:

    {
      "Name": "Blue Stars",

      "DockBackgroundColor": "#030611",
      "ItemBackgroundColor": "#18263E6A",
      "ItemBorderColor": "#4CB8D8FF",
      "TextColor": "#FFF6FBFF",
      "SubmenuIndicatorColor": "#FFF6FBFF",
      "DragGhostBackgroundColor": "#223C5F98",
      "DragGhostBorderColor": "#80CFE4FF",

      "GlassSurfaceEnabled": true,

      "GlassTopColor": "#542A4F9E",
      "GlassBottomColor": "#B0050A1A",
      "GlassHighlightColor": "#86D9F4FF",
      "GlassShadowColor": "#A0000208",

      "GlassGradientStartX": 0.18,
      "GlassGradientStartY": 0.1,
      "GlassGradientEndX": 0.82,
      "GlassGradientEndY": 0.9,

      "GlassGradientAnimationEnabled": true,
      "GlassGradientAnimationDurationSeconds": 32.0,
      "GlassGradientAnimationAutoReverse": false,
      "GlassGradientAnimationEasing": "Linear",
      "GlassGradientAnimationEasingMode": "EaseInOut",

      "GlassGradientAnimationMode": "RotateTransform",
      "GlassGradientAnimationRotationDegrees": 360.0,
      "GlassGradientAnimationAspectCorrect": true,

      "GlassStarEffectEnabled": true,
      "GlassStarLayers": [],

      "GlassCornerRadius": 18,
      "GlassExtraThickness": 14,

      "GlassEffectSchemaVersion": 1,

      "GlassEffects": [
        {
          "Type": "Particles",
          "Renderer": "GlowDot",
          "Enabled": true,
          "Count": 45,
          "Seed": 123,

          "Parameters": {
            "PaddingXRatio": 0.04,
            "TopPaddingRatio": 0.08,
            "BottomPaddingRatio": 0.14,

            "Size": {
              "Min": 1.5,
              "Max": 5.0
            },

            "Opacity": {
              "Min": 0.20,
              "Max": 1.0
            },

            "DurationSeconds": {
              "Min": 2.5,
              "Max": 6.5
            },

            "MaximumDelaySeconds": 2.5,

            "GlowRadius": {
              "Min": 1.0,
              "Max": 4.5
            },

            "Scale": {
              "Min": 0.75,
              "Max": 1.30
            },

            "MaximumDriftXRatio": 0.003,
            "MaximumDriftYRatio": 0.002,

            "Palette": [
              "#FFFFFFFF",
              "#FFDDEBFF",
              "#FFB7D9FF",
              "#FFF8F6D8"
            ]
          }
        }
      ]
    }

## Important particle properties

### `Count`

Number of generated particles.

Higher values create denser effects but require more rendering work.

### `Seed`

Controls the generated particle layout.

The same seed produces the same generated pattern.

Change the seed to create a different arrangement.

### `Size`

Controls the minimum and maximum particle size.

Example:

    "Size": {
      "Min": 1.5,
      "Max": 5.0
    }

### `Opacity`

Controls the minimum and maximum particle opacity.

### `DurationSeconds`

Controls the animation speed of individual particles.

Using a range prevents all particles from moving identically.

### `GlowRadius`

Controls the softness or glow around particles.

### `Scale`

Controls animated particle scaling.

### `Palette`

Defines the available particle colors.

Example:

    "Palette": [
      "#FFFFFFFF",
      "#FFDDEBFF",
      "#FFB7D9FF"
    ]

### `MaximumDriftXRatio`

Controls subtle horizontal particle movement.

### `MaximumDriftYRatio`

Controls subtle vertical particle movement.

For calm themes, keep the drift values small.

---

# Example 3 - Animated Fireplace

The most advanced built-in particle renderer is:

`Flame`

A fireplace theme can contain many individually generated flames.

You do not need to define every flame manually.

GlueDock can generate them from parameter ranges.

Example:

    {
      "Name": "My Fireplace",

      "DockBackgroundColor": "#140806",
      "ItemBackgroundColor": "#18FFF3E0",
      "ItemBorderColor": "#2EFFB96A",
      "TextColor": "#FFFFFFFF",
      "SubmenuIndicatorColor": "#FFFFE8C8",
      "DragGhostBackgroundColor": "#32FF8A24",
      "DragGhostBorderColor": "#90FFC05A",

      "GlassSurfaceEnabled": true,

      "GlassTopColor": "#00000000",
      "GlassBottomColor": "#281A0603",
      "GlassHighlightColor": "#20FFD0A0",
      "GlassShadowColor": "#78200603",

      "GlassGradientAnimationEnabled": false,

      "GlassFlameEffectEnabled": true,

      "GlassFlameBaseGlowEnabled": true,
      "GlassFlameBaseGlowColor": "#A8C92100",
      "GlassFlameBaseGlowHeightRatio": 0.14,
      "GlassFlameBaseGlowOpacity": 0.22,

      "GlassFlameTopFadeStartRatio": 0.03,
      "GlassFlameTopFadeEndRatio": 0.34,

      "GlassFlameLayers": [],

      "GlassCornerRadius": 18,
      "GlassExtraThickness": 14,

      "GlassEffectSchemaVersion": 1,

      "GlassEffects": [
        {
          "Type": "Particles",
          "Renderer": "Flame",
          "Enabled": true,
          "Count": 60,
          "Seed": 42,

          "Parameters": {
            "MotionMode": "NaturalFire",

            "BaseYRatio": 1.0,

            "WidthRatio": {
              "Min": 0.026,
              "Max": 0.046
            },

            "HeightRatio": {
              "Min": 0.34,
              "Max": 0.86
            },

            "RiseRatio": {
              "Min": 0.20,
              "Max": 0.52
            },

            "DriftRatio": {
              "Min": 0.001,
              "Max": 0.003
            },

            "DurationSeconds": {
              "Min": 2.2,
              "Max": 3.8
            },

            "MaximumDelaySeconds": 2.8,

            "Opacity": {
              "Min": 0.36,
              "Max": 0.64
            },

            "ScaleX": {
              "Min": 0.58,
              "Max": 1.08
            },

            "ScaleY": {
              "Min": 0.66,
              "Max": 1.14
            },

            "SwayDegrees": {
              "Min": 1.4,
              "Max": 3.4
            },

            "FlickerRatio": {
              "Min": 0.05,
              "Max": 0.13
            },

            "BlurRadius": {
              "Min": 0.18,
              "Max": 0.50
            },

            "TipStop": 0.0,
            "MidStop": 0.43,
            "CoreStop": 1.0,

            "TipPalette": [
              "#00B91000",
              "#00D91800",
              "#00E81C00"
            ],

            "MidPalette": [
              "#C8E32200",
              "#D8FF2B00",
              "#D0FF3400"
            ],

            "CorePalette": [
              "#E8FF580B",
              "#F8FF8216",
              "#FFFF9B33"
            ]
          }
        }
      ]
    }

## Important flame properties

### `Count`

Number of generated flames.

More flames produce a denser fire effect but require more rendering work.

### `MotionMode`

The natural fireplace renderer uses:

`NaturalFire`

### `BaseYRatio`

Controls the vertical origin of the flames.

A value around `1.0` places the flame base near the bottom of the dock surface.

### `WidthRatio`

Controls flame width relative to the available dock surface.

### `HeightRatio`

Controls flame height relative to the available dock surface.

### `RiseRatio`

Controls how far flames move upward.

### `DriftRatio`

Controls horizontal flame movement.

### `DurationSeconds`

Controls individual flame animation speed.

Using different minimum and maximum durations prevents synchronized movement.

### `MaximumDelaySeconds`

Adds different animation start delays to the generated flames.

### `Opacity`

Controls flame transparency.

### `ScaleX`

Controls horizontal flame scaling.

### `ScaleY`

Controls vertical flame scaling.

### `SwayDegrees`

Controls flame rotation and swaying.

### `FlickerRatio`

Controls flame intensity variation.

### `BlurRadius`

Adds softness to the flames.

### `TipPalette`

Defines the colors used at the flame tips.

### `MidPalette`

Defines the middle flame colors.

### `CorePalette`

Defines the hottest flame colors near the base.

ARGB transparency is especially useful for flame tips.

Example:

`#00B91000`

The first byte is `00`, so the color is fully transparent.

---

# Useful Base Properties

These properties can be used in every theme:

    {
      "DockBackgroundColor": "#12161C",
      "ItemBackgroundColor": "#22FFFFFF",
      "ItemBorderColor": "#22FFFFFF",
      "TextColor": "#FFFFFFFF",
      "SubmenuIndicatorColor": "#FFFFFFFF",
      "DragGhostBackgroundColor": "#22FFFFFF",
      "DragGhostBorderColor": "#66FFFFFF",
      "GlassCornerRadius": 20,
      "GlassExtraThickness": 0
    }

---

# Glass Surface

A simple glass surface can use:

    {
      "GlassSurfaceEnabled": true,
      "GlassTopColor": "#40FFFFFF",
      "GlassBottomColor": "#10101020",
      "GlassHighlightColor": "#60FFFFFF",
      "GlassShadowColor": "#50000000"
    }

---

# Gradient Animation Modes

## Point Animation

Use:

`"GlassGradientAnimationMode": "Points"`

This moves the gradient start and end coordinates.

Typical properties:

    "GlassGradientAnimationToStartX": 0.8,
    "GlassGradientAnimationToStartY": 0.0,
    "GlassGradientAnimationToEndX": 0.2,
    "GlassGradientAnimationToEndY": 1.0

## Rotating Gradient

Use:

`"GlassGradientAnimationMode": "RotateTransform"`

Example:

    "GlassGradientAnimationMode": "RotateTransform",
    "GlassGradientAnimationRotationDegrees": 360.0,
    "GlassGradientAnimationAspectCorrect": true

This rotates the gradient instead of moving its points.

---

# Animation Speed

For most animation duration properties:

`smaller value = faster`

`larger value = slower`

Example:

`2.0` seconds is much faster than `20.0` seconds.

For subtle background animations, longer durations usually look better.

---

# Relative Values

Many effect properties end with:

`Ratio`

These values are normally relative to the available dock surface.

Typical reference values:

`0.0` = start

`0.5` = middle

`1.0` = end

Some effect parameters intentionally allow values outside this range.

---

# Generated Effects

Generated effects are defined inside:

`GlassEffects`

Example structure:

    "GlassEffects": [
      {
        "Type": "Particles",
        "Renderer": "GlowDot",
        "Enabled": true,
        "Count": 40,
        "Seed": 123,
        "Parameters": {
        }
      }
    ]

Important common properties:

### `Type`

For generated particles:

`Particles`

### `Renderer`

Selects the effect renderer.

Supported examples include:

`GlowDot`

`Flame`

### `Enabled`

The effect is only active when this is:

`true`

### `Count`

Controls how many generated elements are created.

### `Seed`

Controls deterministic random generation.

The same configuration and seed produce the same generated arrangement.

---

# Recommended Workflow

Start with an existing theme that is already close to the result you want.

Useful starting points include existing gradient, star and fireplace themes.

Then change only a few values at a time.

Recommended progression:

1. Colors
2. Glass surface
3. Gradient direction
4. Gradient animation
5. Particle count
6. Particle colors
7. Particle size
8. Particle opacity
9. Movement
10. Timing
11. Blur
12. Advanced flame parameters

Avoid changing many unrelated parameters at once.

This makes it much easier to understand which setting creates a particular visual effect.

---

# Performance

Animated themes require more rendering work than static themes.

For lightweight star themes, start around:

`GlowDot Count: 20-50`

For denser star themes:

`GlowDot Count: 50-100`

For fireplace themes, a useful starting range is:

`Flame Count: 40-90`

Very high particle counts can increase rendering load.

Large blur radii can also increase rendering cost.

Very short animation durations combined with many particles may increase CPU/GPU usage.

For smooth themes, prefer:

- moderate particle counts
- small blur radii
- different animation durations
- subtle movement
- longer background animation durations

---

# Troubleshooting

## Theme Does Not Appear

Make sure the theme file is located directly inside:

`GlueDock_Themes`

The file must use the `.json` extension.

---

## GlueDock Falls Back to Default

Check the JSON syntax.

Common mistakes include:

- missing comma
- extra comma
- missing quotation mark
- invalid property value
- missing closing brace
- missing closing bracket

---

## Gradient Animation Does Not Run

Make sure:

    "GlassGradientAnimationEnabled": true

is present.

Also check the selected animation mode.

For moving coordinates:

    "GlassGradientAnimationMode": "Points"

For rotation:

    "GlassGradientAnimationMode": "RotateTransform"

---

## Stars Do Not Appear

Make sure:

    "GlassStarEffectEnabled": true

is enabled.

For generated star effects, also make sure the corresponding `GlassEffects` entry contains:

    "Enabled": true

and:

    "Renderer": "GlowDot"

---

## Flames Do Not Appear

Make sure:

    "GlassFlameEffectEnabled": true

is enabled.

For generated flame effects, also make sure:

    "Enabled": true

and:

    "Renderer": "Flame"

are present inside the `GlassEffects` entry.

---

## Generated Particles Always Look the Same

Change the seed.

Example:

    "Seed": 123

Change it to another value:

    "Seed": 456

Different seeds create different generated arrangements while keeping the result reproducible.

---

## Animation Is Too Fast

Increase:

`DurationSeconds`

or:

`GlassGradientAnimationDurationSeconds`

---

## Animation Is Too Slow

Decrease:

`DurationSeconds`

or:

`GlassGradientAnimationDurationSeconds`

---

## Effect Looks Too Busy

Reduce:

- `Count`
- `Opacity`
- `GlowRadius`
- `DriftRatio`
- `SwayDegrees`
- `FlickerRatio`

---

## Effect Looks Too Static

Increase or vary:

- particle count
- movement ranges
- animation duration ranges
- drift
- scale variation
- delay variation

---

# Tips

- Start from an existing working theme.
- Keep a backup before making large changes.
- Change one group of properties at a time.
- Use longer animation durations for subtle background motion.
- Use small drift values for calm particle effects.
- Use different duration ranges to avoid synchronized particles.
- Use transparent ARGB colors for soft flame tips and glow effects.
- Use `Seed` to quickly generate a new particle arrangement.
- Keep particle counts reasonable for good performance.
- Prefer generated `GlassEffects` over manually defining large numbers of individual particles.
