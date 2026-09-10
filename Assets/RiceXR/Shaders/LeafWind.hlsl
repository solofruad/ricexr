#ifndef LEAF_WIND_INCLUDED
#define LEAF_WIND_INCLUDED

// Deforma la hoja en espacio mundial para que la amplitud se exprese en metros,
// independientemente de la escala del prefab.
float3 ApplyLeafWind(float3 positionOS, float4 vertexColor)
{
    float mask = pow(saturate(vertexColor.r), max(_WindMaskPower, 0.001));
    float3 positionWS = TransformObjectToWorld(positionOS);

    float3 direction = _WindDirection.xyz;
    float directionLength = max(length(direction), 0.001);
    direction /= directionLength;

    float phase = _Time.y * _WindSpeed
                + dot(positionWS, direction) * _WindFrequency
                + _WindPhase;

    // Dos ondas con distinta frecuencia evitan que el movimiento parezca mecanico.
    float wave = sin(phase) + sin(phase * 0.63 + 1.7) * 0.35;
    float displacement = wave * _WindAmplitude * mask * _WindWeight;

    positionWS += direction * displacement;
    return TransformWorldToObject(positionWS);
}

#endif
