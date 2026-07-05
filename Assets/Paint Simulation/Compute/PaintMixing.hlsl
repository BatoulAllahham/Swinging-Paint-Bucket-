// Convert RGB color to K/S ratio per channel (KM remission function)
float3 RGBtoKS(float3 rgb)
{
    // Clamp to avoid division by zero
    rgb = clamp(rgb, 0.001, 0.999);
    return (1.0 - rgb) * (1.0 - rgb) / (2.0 * rgb);
}

// Convert K/S ratio back to RGB reflectance (inverse KM equation)
float3 KStoRGB(float3 ks)
{
    // R∞ = 1 + K/S - sqrt((K/S)² + 2(K/S))
    return 1.0 + ks - sqrt(ks * ks + 2.0 * ks);
}

// Mix two paint colors using Kubelka-Munk
// c1, c2 = concentrations ,sums to 1
float3 KubelkaMunkMix(float3 colorA, float3 colorB, float t)
{
    float3 ksA = RGBtoKS(colorA);
    float3 ksB = RGBtoKS(colorB);

    float3 ksMixed = (1.0 - t) * ksA + t * ksB;

    return KStoRGB(ksMixed);
}