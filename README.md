# Volumetric-Clouds
[WIP] Everything needed to author and display volumetric clouds. This includes generating the tileable noise textures needed.
Everything from the noise generation to the cloud rendering raymarcher is written from scratch.

Latest showcase

Cloud shape is a combination of Perlin-Worley noise and several layers of worley noise. Coverage map is provided by a texture aswell as the max cloud height. Lots of dials to change such as density, coverage, noise values, wind speed and disturbance speed.

### Short gif of clouds rolling overhead
![showcase20230414 1](https://user-images.githubusercontent.com/37589250/232048468-074831f2-8ef0-46d1-b0ea-053f7cb74027.gif)

### Short gif of same angle but with a more noticable coverage map to show off this feature
![showcase20230414 2](https://user-images.githubusercontent.com/37589250/232048566-43e82d71-7892-4eeb-94c5-4732b2cd069f.gif)
