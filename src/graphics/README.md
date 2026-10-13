# Standalone graphics tooling
See E:\02_Projects\chill-with-you-web\README.md for preview and limitations.
Normal build: root build.ps1.
Full regeneration: recover-project.mjs -> prepare_project.py -> asset_pipeline.py -> generate_shaders.py -> prepare_lighting.py -> Unity build.
Do not regenerate the recovered export or prepared project for an incremental build.
Runtime contains the room, asset viewer, environment selector and CPU bone physics.
Editor repairs static batching and builds WebGL bundles/catalog/player.
link.xml preserves TMP and UGUI classes used only by bundles.
Shaders preserve recovered properties/render states but replace unavailable Shader Graph source with new WebGL implementations.
Windows bundles, compute shaders and native plugins remain source-only; WebGL bundles use LZ4 and are rebuilt for WebGL.
Recovered game assets are local input and are not uploaded by this workflow.
