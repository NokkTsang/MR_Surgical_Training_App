using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace Obi
{

    /**
     * Generates a sparse distance field from a voxel representation of a mesh.
     */
    public class VoxelDistanceField
    {
        public Vector3[,,] distanceField; // for each coordinate, stores coordinates of closest surface voxel.

        private MeshVoxelizer voxelizer;

        public VoxelDistanceField(MeshVoxelizer voxelizer)
        {
            this.voxelizer = voxelizer;
        }

        public Vector4 SampleUnfiltered(int x, int y, int z)
        {
            x = Mathf.Clamp(x, 0, voxelizer.resolution.x - 1);
            y = Mathf.Clamp(y, 0, voxelizer.resolution.y - 1);
            z = Mathf.Clamp(z, 0, voxelizer.resolution.z - 1);

            var grad = distanceField[x, y, z];
            float dist = grad.magnitude;
            grad.Normalize();

            return new Vector4(grad.x, grad.y, grad.z, -dist);
        }

        public Vector4 SampleFiltered(float x, float y, float z, Vector3Int axisMask)
        {
            var pos = new Vector3(x, y, z);

            // clamp position inside the distance field:
            var min = voxelizer.GetVoxelCenter(new Vector3Int(0, 0, 0));
            var max = voxelizer.GetVoxelCenter(new Vector3Int(voxelizer.resolution.x - 1, voxelizer.resolution.y - 1, voxelizer.resolution.z - 1));
            pos.x = Mathf.Clamp(pos.x, min.x, max.x);
            pos.y = Mathf.Clamp(pos.y, min.y, max.y);
            pos.z = Mathf.Clamp(pos.z, min.z, max.z);

            var voxel = voxelizer.GetPointVoxel(pos - (Vector3)axisMask * voxelizer.voxelSize * 0.5f) - voxelizer.Origin;
            var voxelCenter = voxelizer.GetVoxelCenter(voxel);
            var norm = Vector3.Scale((pos - voxelCenter) / voxelizer.voxelSize, axisMask);

            var xz00 = SampleUnfiltered(voxel.x, voxel.y, voxel.z);
            var xz01 = SampleUnfiltered(voxel.x, voxel.y, voxel.z + 1);
            var xz10 = SampleUnfiltered(voxel.x + 1, voxel.y, voxel.z);
            var xz11 = SampleUnfiltered(voxel.x + 1, voxel.y, voxel.z + 1);

            var yz00 = SampleUnfiltered(voxel.x, voxel.y + 1, voxel.z);
            var yz01 = SampleUnfiltered(voxel.x, voxel.y + 1, voxel.z + 1);
            var yz10 = SampleUnfiltered(voxel.x + 1, voxel.y + 1, voxel.z);
            var yz11 = SampleUnfiltered(voxel.x + 1, voxel.y + 1, voxel.z + 1);

            var X1 = Vector4.Lerp(xz00, xz10, norm.x);
            var X2 = Vector4.Lerp(xz01, xz11, norm.x);
            var X3 = Vector4.Lerp(yz00, yz10, norm.x);
            var X4 = Vector4.Lerp(yz01, yz11, norm.x);

            var Y1 = Vector4.Lerp(X1, X2, norm.z);
            var Y2 = Vector4.Lerp(X3, X4, norm.z);

            return Vector4.Lerp(Y1, Y2, norm.y);
        }

        public void Smooth()
        {
            // create output buffer for ping-pong.
            Vector3[,,] smoothed = new Vector3[voxelizer.resolution.x,
                                               voxelizer.resolution.y,
                                               voxelizer.resolution.z];

            for (int x = 0; x < distanceField.GetLength(0); ++x)
                for (int y = 0; y < distanceField.GetLength(1); ++y)
                    for (int z = 0; z < distanceField.GetLength(2); ++z)
                    {
                        if (voxelizer[x, y, z] != MeshVoxelizer.Voxel.Outside)
                        {
                            var p = new Vector3Int(x, y, z);
                            Vector3 df = distanceField[x, y, z];
                            int count = 1;
                            foreach (var o in MeshVoxelizer.faceNeighborhood)
                            {
                                // offset voxel to get neighbor:
                                var n = p + o;
                                if (voxelizer.VoxelExists(n.x, n.y, n.z) && voxelizer[n.x, n.y, n.z] != MeshVoxelizer.Voxel.Outside)
                                {
                                    df += distanceField[n.x, n.y, n.z];
                                    count++;
                                }
                            }
                            df /= count;
                            smoothed[x, y, z] = df;
                        }
                    }

            distanceField = smoothed;
        }

        private void CalculateGradientsAndDistances(Vector3Int[,,] buffer1)
        {
            distanceField = new Vector3[voxelizer.resolution.x,
                                        voxelizer.resolution.y,
                                        voxelizer.resolution.z];

            for (int x = 0; x < buffer1.GetLength(0); ++x)
                for (int y = 0; y < buffer1.GetLength(1); ++y)
                    for (int z = 0; z < buffer1.GetLength(2); ++z)
                    {
                        if (voxelizer[x, y, z] != MeshVoxelizer.Voxel.Outside)
                        {
                            distanceField[x, y, z] = voxelizer.GetVoxelCenter(buffer1[x, y, z]) -
                                                     voxelizer.GetVoxelCenter(new Vector3Int(x, y, z));
                        }
                        else
                            distanceField[x, y, z] = Vector3.zero;
                    }
        }

        public IEnumerator JumpFlood()
        {
            // create two buffers for ping-ponging:
            Vector3Int[,,] buffer1 = new Vector3Int[voxelizer.resolution.x,
                                                    voxelizer.resolution.y,
                                                    voxelizer.resolution.z];

            Vector3Int[,,] buffer2 = new Vector3Int[voxelizer.resolution.x,
                                                      voxelizer.resolution.y,
                                                      voxelizer.resolution.z];

            // initialize distance field:
            for (int x = 0; x < buffer1.GetLength(0); ++x)
                for (int y = 0; y < buffer1.GetLength(1); ++y)
                    for (int z = 0; z < buffer1.GetLength(2); ++z)
                    {
                        if (voxelizer[x, y, z] == MeshVoxelizer.Voxel.Outside)
                            buffer1[x, y, z] = new Vector3Int(x, y, z);
                        else
                            buffer1[x, y, z] = new Vector3Int(-1, -1, -1);
                    }

            // calculate the maximum size of the buffer:
            int size = Mathf.Max(buffer1.GetLength(0),
                                 buffer1.GetLength(1),
                                 buffer1.GetLength(2));
            int step = (int)(size / 2.0f);

            yield return new CoroutineJob.ProgressInfo("Generating voxel distance field...", 0);

            float numPasses = (int)Mathf.Log(size, 2);
            int i = 0;

            // jump flood passes:
            while (step >= 1)
            {
                JumpFloodPass(step, buffer1, buffer2);

                // halve step:
                step /= 2;

                // swap buffers:
                Vector3Int[,,] temp = buffer1;
                buffer1 = buffer2;
                buffer2 = temp;

                yield return new CoroutineJob.ProgressInfo("Generating voxel distance field...", ++i / numPasses);
            }

            CalculateGradientsAndDistances(buffer1);
        }

        private void JumpFloodPass(int stride, Vector3Int[,,] input, Vector3Int[,,] output)
        {
            // for each voxel:
            for (int x = 0; x < input.GetLength(0); ++x)
                for (int y = 0; y < input.GetLength(1); ++y)
                    for (int z = 0; z < input.GetLength(2); ++z)
                    {
                        // our position:
                        Vector3Int p = new Vector3Int(x, y, z);

                        // our seed:
                        Vector3Int s = input[x, y, z];

                        // copy the closest seed to the output, in case we do not update it this pass:
                        output[x, y, z] = s;

                        // this voxel is a seed, skip it.
                        if (s.x == x && s.y == y && s.z == z)
                            continue;

                        // distance to our closest seed:
                        float dist = float.MaxValue;
                        if (s.x >= 0)
                            dist = (s - p).sqrMagnitude;

                        // for each neighbor voxel:
                        foreach (var o in MeshVoxelizer.fullNeighborhood)
                        {
                            // offset voxel to get neighbor:
                            var n = p + o * stride;

                            if (voxelizer.VoxelExists(n.x, n.y, n.z))
                            {
                                // neighbors' closest seed.
                                Vector3Int nc = input[n.x, n.y, n.z];

                                if (nc.x >= 0)
                                {
                                    // distance to neighbor's closest seed:
                                    float newDist = (nc - p).sqrMagnitude;

                                    // if the distance to the neighbor's closest seed is smaller than the distance to ours:
                                    if (newDist < dist)
                                    {
                                        output[x, y, z] = nc;
                                        dist = newDist;
                                    }
                                }
                            }
                        }
                    }

        }
    }
}