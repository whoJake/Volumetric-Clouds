# Volumetric-Clouds
[WIP] Everything needed to author and display volumetric clouds. This includes generating the tileable noise textures needed.
Everything from the noise generation to the cloud rendering raymarcher is written from scratch.

Latest screenshot. Brownian Perlin noise generated on the GPU. Theres no combination of Perlin and Worley noise yet. TODO List is in VolumetricCloud.shader

Screenshots taken just after implementation of Henyey-Greenstein scattering which increases the intensity of the lighting in pixels that are facing the sun. The theory behind this has to do with light having more chance to scatter forwards rather than being uniformly scattered.

![progress20230408 hgscattering](https://user-images.githubusercontent.com/37589250/230717815-2c0cd6b0-9ccf-4b88-a0bb-db04dafcd164.png)

![progress20230408](https://user-images.githubusercontent.com/37589250/230717801-77db7b91-58c7-4d16-b95b-de6e72a25699.png)
