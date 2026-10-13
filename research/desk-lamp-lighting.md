# Original desk lamp lighting

Source scene and settings are read from `Recovered/ExportedProject/Assets`. The left task lamp is `map_model/model/Platform_PC/m_standlight_mobile`, with a second light under the sibling `m_standlight_mobile (1)`. Both are real-time point lights with range 1; their intensities are 0.13 and 0.24. The second light is warm yellow, RGB (0.8627451, 0.7581753, 0.3490196). Their original transforms and colors remain in the converted scene.

The yellow lamp on the right is a separate fixture under `Paremt/EnviromentObject/DateTime/{preset}/m_Lamp_table`. Its point light has intensity 0.25, range 0.8 (1 at night), and a different yellow color. It must not be used as the test target for the left desktop light.

The port's ChillURP had additional lighting disabled (mode 0). The recovered PC pipelines enable additional lighting with four lights per object; HighFidelity uses PerPixel, while Balanced/Performant use PerVertex. Restore PerPixel using the enum rather than a numeric literal. Keep four lights per object and additional-light shadows disabled for this browser build.

No new lights or brighter replacement materials are added. On/off Editor renders isolate the two original PC desk point lights to check illumination on the desktop. See `desk-lamp-source-lights.json`, `desk-lamp-render-check.json`, and `preview-native-desk-lamp-on/off.png`.

The 55 toon profiles also compile their additional-light GLES3 vertex/fragment stages: 110 successful stages, at most nine texture bindings against the limit of 16. See `toon-additional-light-budget.json`. Rebuild both player and existing asset bundles with the enabled pipeline to retain lighting shader variants in cached materials. Shader feature generation and original recovered files are unchanged.

Editor render comparisons establish illumination, not pixel-perfect browser/Windows color parity.
