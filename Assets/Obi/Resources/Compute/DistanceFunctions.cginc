#ifndef DISTANCEFUNCTIONS_INCLUDE
#define DISTANCEFUNCTIONS_INCLUDE

#include "SurfacePoint.cginc"
#include "Transform.cginc"
#include "Bounds.cginc"

struct Sphere : IDistanceFunction
{
    shape s;
    transform colliderToSolver;
    
    void Evaluate(in float4 pos, in float4 radii, in quaternion orientation, inout SurfacePoint projectedPoint)
    {
        float4 center = s.center * colliderToSolver.scale;
        float4 pnt = colliderToSolver.InverseTransformPointUnscaled(pos) - center;

        if (s.is2D())
           pnt[2] = 0;

        float radius = s.size.x * cmax(colliderToSolver.scale.xyz);
        float distanceToCenter = length(pnt);

        float4 normal = pnt / (distanceToCenter + EPSILON);

        projectedPoint.pos = colliderToSolver.TransformPointUnscaled(center + normal * (radius + s.contactOffset));
        projectedPoint.normal = colliderToSolver.TransformDirection(normal);
        projectedPoint.bary = float4(1,0,0,0);
    }
};

struct Box : IDistanceFunction
{
    shape s;
    transform colliderToSolver;
    
    void Evaluate(in float4 pos, in float4 radii, in quaternion orientation, inout SurfacePoint projectedPoint)
    {
        float4 center = s.center * colliderToSolver.scale;
        float4 size = s.size * colliderToSolver.scale * 0.5f;

        // clamp the point to the surface of the box:
        float4 pnt = colliderToSolver.InverseTransformPointUnscaled(pos) - center;

        if (s.is2D())
            pnt[2] = 0;

        // get minimum distance for each axis:
        float4 distances = size - abs(pnt);

        if (distances.x >= 0 && distances.y >= 0 && distances.z >= 0)
        {
            projectedPoint.normal = float4(0,0,0,0);
            projectedPoint.pos = pnt;

            // find minimum distance in all three axes and the axis index:        
            if (distances.y < distances.x && distances.y < distances.z)
            {
                projectedPoint.normal[1] = sign(pnt[1]);
                projectedPoint.pos[1] = size[1] * projectedPoint.normal[1];
            }
            else if (distances.z < distances.x && distances.z < distances.y)
            {
                projectedPoint.normal[2] = sign(pnt[2]);
                projectedPoint.pos[2] = size[2] * projectedPoint.normal[2];
            }
            else
            {
                projectedPoint.normal[0] = sign(pnt[0]);
                projectedPoint.pos[0] = size[0] * projectedPoint.normal[0];
            }
        }
        else
        {
            projectedPoint.pos = clamp(pnt, -size, size);
            projectedPoint.normal = normalizesafe(pnt - projectedPoint.pos);
        }

        projectedPoint.pos = colliderToSolver.TransformPointUnscaled(projectedPoint.pos + center + projectedPoint.normal * s.contactOffset);
        projectedPoint.normal = colliderToSolver.TransformDirection(projectedPoint.normal);
        projectedPoint.bary = float4(1,0,0,0);
    }
};

struct Capsule : IDistanceFunction
{
    shape s;
    transform colliderToSolver;
    
    void Evaluate(in float4 pos, in float4 radii, in quaternion orientation, inout SurfacePoint projectedPoint)
    {
        float4 center = s.center * colliderToSolver.scale;
        float4 pnt = colliderToSolver.InverseTransformPointUnscaled(pos) - center;

        if (s.is2D())
            pnt[2] = 0;

        int direction = (int)s.size.z;
        float height;
        float radius;
        float4 halfVector = float4(0,0,0,0);

        if (direction == 0)
        {
            radius = s.size.x * max(colliderToSolver.scale[1], colliderToSolver.scale[2]);
            height = max(radius, s.size.y * 0.5f * colliderToSolver.scale[0]);
            halfVector[0] = height - radius;
        }
        else if (direction == 1)
        {
            radius = s.size.x * max(colliderToSolver.scale[2], colliderToSolver.scale[0]);
            height = max(radius, s.size.y * 0.5f * colliderToSolver.scale[1]);
            halfVector[1] = height - radius;
        }
        else
        {
            radius = s.size.x * max(colliderToSolver.scale[0], colliderToSolver.scale[1]);
            height = max(radius, s.size.y * 0.5f * colliderToSolver.scale[2]);
            halfVector[2] = height - radius;
        }

        float mu;
        float4 centerLine = NearestPointOnEdge(-halfVector, halfVector, pnt, mu);
        float4 centerToPoint = pnt - centerLine;
        float distanceToCenter = length(centerToPoint);

        float4 normal = centerToPoint / (distanceToCenter + EPSILON);

        projectedPoint.pos = colliderToSolver.TransformPointUnscaled(center + centerLine + normal * (radius + s.contactOffset));
        projectedPoint.normal = colliderToSolver.TransformDirection(normal);
        projectedPoint.bary = float4(1,0,0,0);
    }
};

struct BIHNode
{
    int firstChild;     /**< index of the first child node. The second one is right after the first.*/
    int start;          /**< index of the first element in this node.*/
    int count;          /**< amount of elements in this node.*/

    int axis;           /**< axis of the split plane (0,1,2 = x,y,z)*/
    float min_;         /**< minimum split plane*/
    float max_;         /**< maximum split plane*/
};

struct TriangleMeshHeader 
{
    int firstNode;
    int nodeCount;
    int firstTriangle;
    int triangleCount;
    int firstVertex;
    int vertexCount;
};

struct Triangle
{
    int i1;
    int i2;
    int i3;
    aabb b;
};

struct TriangleMesh : IDistanceFunction
{
    shape s;
    transform colliderToSolver;

    CachedTri tri;
    
    void Evaluate(in float4 pos, in float4 radii, in quaternion orientation, inout SurfacePoint projectedPoint)
    {
       float4 pnt = colliderToSolver.InverseTransformPointUnscaled(pos);

        if (s.is2D())
            pnt[2] = 0;

        float4 bary = FLOAT4_ZERO;
        float4 nearestPoint = NearestPointOnTri(tri, pnt, bary);
        float4 normal = normalizesafe(pnt - nearestPoint);

        projectedPoint.pos = colliderToSolver.TransformPointUnscaled(nearestPoint + normal * s.contactOffset);
        projectedPoint.normal = colliderToSolver.TransformDirection(normal);
        projectedPoint.bary = float4(1,0,0,0);
    }

};

struct HeightFieldHeader
{
    int firstSample;
    int sampleCount;
};

struct Heightfield : IDistanceFunction
{
    shape s;
    transform colliderToSolver;

    CachedTri tri;
    float4 triNormal;

    void Evaluate(in float4 pos, in float4 radii, in quaternion orientation, inout SurfacePoint projectedPoint)
    {
        float4 pnt = colliderToSolver.InverseTransformPoint(pos);

        float4 bary;
        float4 nearestPoint = NearestPointOnTri(tri, pnt, bary);
        float4 normal = normalizesafe(pnt - nearestPoint);

        // flip the contact normal if it points below ground: (doesn't work with holes)
        //OneSidedNormal(triNormal, normal);

        projectedPoint.pos = colliderToSolver.TransformPoint(nearestPoint + normal * s.contactOffset);
        projectedPoint.normal = colliderToSolver.TransformDirection(normal);
        projectedPoint.bary = float4(1,0,0,0);
    }

};

struct DistanceFieldHeader 
{
    int firstNode;
    int nodeCount;
};

struct DFNode
{
    float4 distancesA;
    float4 distancesB;
    float4 center;
    int firstChild;

    // add 12 bytes of padding to ensure correct memory alignment:
    int pad0;
    int pad1;
    int pad2;

    float4 GetNormalizedPos(float4 position)
    {
        float4 corner = center - float4(center[3],center[3],center[3],center[3]);
        return (position - corner) / (center[3] * 2);
    }

    float4 SampleWithGradient(float4 position)
    {
        float4 nPos = GetNormalizedPos(position);

        // trilinear interpolation of distance:
        float4 x = distancesA + (distancesB - distancesA) * nPos[0];
        float2 y = x.xy + (x.zw - x.xy) * nPos[1];
        float dist = y[0] + (y[1] - y[0]) * nPos[2];

        // gradient estimation:
        // x == 0
        float2 a = distancesA.xy + (distancesA.zw - distancesA.xy) * nPos[1];
        float x0 = a[0] + (a[1] - a[0]) * nPos[2];

        // x == 1
        a = distancesB.xy + (distancesB.zw - distancesB.xy) * nPos[1];
        float x1 = a[0] + (a[1] - a[0]) * nPos[2];

        // y == 0
        float y0 = x[0] + (x[1] - x[0]) * nPos[2];

        // y == 1
        float y1 = x[2] + (x[3] - x[2]) * nPos[2];

        return float4(x1 - x0, y1 - y0, y[1] - y[0], dist);

    }

    int GetOctant(float4 position)
    {
        int index = 0;
        if (position[0] > center[0]) index |= 4;
        if (position[1] > center[1]) index |= 2;
        if (position[2] > center[2]) index |= 1;
        return index;
    }
};

struct DistanceField : IDistanceFunction
{
    shape s;
    transform colliderToSolver;
    
    StructuredBuffer<DistanceFieldHeader> distanceFieldHeaders;
    StructuredBuffer<DFNode> dfNodes;

    float4 DFTraverse(float4 particlePosition,
                    in DistanceFieldHeader header)
    {
        int stack[12]; 
        int stackTop = 0;

        stack[stackTop++] = 0;

        while (stackTop > 0)
        {
            // pop node index from the stack:
            int nodeIndex = stack[--stackTop];
            DFNode node = dfNodes[header.firstNode + nodeIndex];

            // if the child node exists, recurse down the df octree:
            if (node.firstChild >= 0)
                stack[stackTop++] = node.firstChild + node.GetOctant(particlePosition);
            else
                return node.SampleWithGradient(particlePosition);
        }
        return FLOAT4_ZERO;
    }
    
    void Evaluate(in float4 pos, in float4 radii, in quaternion orientation, inout SurfacePoint projectedPoint)
    {
        float4 pnt = colliderToSolver.InverseTransformPoint(pos);

        if (s.is2D())
            pnt[2] = 0;
            
        float4 sample = DFTraverse(pnt, distanceFieldHeaders[s.dataIndex]);
        float4 normal = float4(normalize(sample.xyz), 0);

        projectedPoint.pos = colliderToSolver.TransformPoint(pnt - normal * (sample[3] - s.contactOffset));
        projectedPoint.normal = colliderToSolver.TransformDirection(normal);
        projectedPoint.bary = float4(1,0,0,0);
    }
};

struct EdgeMeshHeader 
{
    int firstNode;
    int nodeCount;
    int firstEdge;
    int edgeCount;
    int firstVertex;
    int vertexCount;
};

struct Edge
{
    int i1;
    int i2;
    aabb b;
};

struct EdgeMesh : IDistanceFunction
{
    shape s;
    transform colliderToSolver;
    int dataOffset;

    CachedEdge edge;
    
    void Evaluate(in float4 pos, in float4 radii, in quaternion orientation, inout SurfacePoint projectedPoint)
    {
       float4 pnt = colliderToSolver.InverseTransformPointUnscaled(pos);

        if (s.is2D())
            pnt[2] = 0;
            
        float mu = 0;
        float4 nearestPoint = NearestPointOnEdge(edge, pnt, mu);
        float4 normal = normalizesafe(pnt - nearestPoint);
        
        projectedPoint.pos = colliderToSolver.TransformPointUnscaled(nearestPoint + normal * s.contactOffset);
        projectedPoint.normal = colliderToSolver.TransformDirection(normal);
        projectedPoint.bary = float4(1,0,0,0);
    }

};


#endif