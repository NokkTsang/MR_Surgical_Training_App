#ifndef INERTIALFRAME_INCLUDE
#define INERTIALFRAME_INCLUDE

#include "Transform.cginc"

struct inertialFrame
{
    transform frame;
    transform prevFrame;

    float4 velocity;
    float4 angularVelocity;

    float4 acceleration;
    float4 angularAcceleration;

    float4 velocityAtPoint(float4 pnt)
    {
        return velocity + float4(cross(angularVelocity.xyz, (pnt - prevFrame.translation).xyz), 0);
    }
};

#endif