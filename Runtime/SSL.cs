using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SSL
{
    /// <summary>
    /// The sword graph is a directed graph representing the 
    /// elements of a sword as they connect to one another.
    /// 
    /// Liminal elements  (in the middle / transitional e.g. blade, grip)
	///     Params: subdiv(int), size(vect3), nLoops(int), deforms(vect3[nLoops]), splineParams(spParams), nextElement(element)
    ///     
    /// Union elements(branching e.g.crossguard)
    ///     Params: subdiv(int), size(vect3), nLoops(int), nextElements(element[6])
    ///     
    /// Terminal elements(at ends e.g.pommel and tip)
    ///     Params: subdiv(int), size(vect3), nLoops(int), nextElement(only relevant for first)
    ///     
    ///Note:
    ///     nLoops + 2 (where end loops are non deformable)
    /// 
    /// </summary>
    public class SwordGraph
    {
        SElement[] _nodes;
        Dictionary<int, int[]> _edges;
        int _subdiv;
        float _elementSpacing;
        bool _useFlatshade;

        //Parser?
        public void Load(int subdivisions, float elementSpacing, SElement[] nodes, Dictionary<int, int[]> edges, bool useFlatshading)
        {
            _nodes = nodes;
            _edges = edges;
            _subdiv = subdivisions;
            _elementSpacing = elementSpacing;
            _useFlatshade = useFlatshading;
        }

        public void Generate(ref Mesh mesh)
        {
            Mesh newMesh = Generate();
            mesh.Clear();
            mesh.SetVertices(newMesh.vertices);
            mesh.triangles = newMesh.triangles;
            mesh.SetUVs(0, newMesh.uv);
            UnityEngine.Rendering.SubMeshDescriptor[] submeshdesc = new UnityEngine.Rendering.SubMeshDescriptor[newMesh.subMeshCount];
            for (int i = 0; i < newMesh.subMeshCount; i++)
            {
                submeshdesc[i] = newMesh.GetSubMesh(i);
            }
            mesh.SetSubMeshes(submeshdesc);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
        }


        public Mesh Generate()
        {
            if (!(_nodes.Length > 0) || _edges.Count < _nodes.Length)
                return new Mesh();

            Mesh newMesh = new Mesh();
            Dictionary<SElement, bool> completionLookup = new Dictionary<SElement, bool>();
            Stack<SElement> stack = new Stack<SElement>();
            Stack<int> edgeStack = new Stack<int>();
            stack.Push(_nodes[0]);

            //Populate completion lookup and determine submesh count
            int submeshCount = 1;
            for (int i = 0; i < _nodes.Length; i++)
            {
                completionLookup.Add(_nodes[i], false);
                int smIndex = _nodes[i].GetSubmeshIndex();
                if (smIndex >= submeshCount)
                    submeshCount = smIndex + 1;
            }

            List<int>[] subMeshTriangleSets = new List<int>[submeshCount];
            for (int i = 0; i < subMeshTriangleSets.Length; i++)
            {
                subMeshTriangleSets[i] = new List<int>();
            }

            //Iterate over all edges and merge all meshes
            while (stack.Count > 0) 
            {
                SElement currentNode = stack.Peek();
                int[] currentNodeConnections = _edges[Array.IndexOf(_nodes, currentNode)];
                if (completionLookup[currentNode] == false)
                {
                    //Get mesh
                    Mesh subMesh = currentNode.GetMesh();

                    SElement[] currentStackState = stack.ToArray();
                    int[] currentEdgeStackState = edgeStack.ToArray();
                    Matrix4x4 transformationMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);
                    Vector3[] connectingLoop = new Vector3[0];

                    List<int> prevSubmeshTriSet = null;

                    for (int i = currentEdgeStackState.Length - 1; i >= 0; i--)
                    {
                        if (i == 0)
                        {
                            connectingLoop = currentStackState[i+1].GetConnectionData(currentEdgeStackState[i]).GetEdgeLoop(transformationMatrix);
                            prevSubmeshTriSet = subMeshTriangleSets[currentStackState[i+1].GetSubmeshIndex()];
                        }
                        transformationMatrix *= currentStackState[i+1].GetConnectionData(currentEdgeStackState[i]).transformationMatrix;
                    }

                    newMesh = JoinMeshes(newMesh, subMesh, _subdiv, prevSubmeshTriSet, subMeshTriangleSets[currentNode.GetSubmeshIndex()], transformationMatrix, connectingLoop);
                    completionLookup[currentNode] = true;
                }
                
                bool nextFound = false;
                for (int i = 0; i < currentNodeConnections.Length; i++)
                {
                    if (currentNodeConnections[i] == -1)
                        continue;
                    if (!completionLookup[_nodes[currentNodeConnections[i]]])
                    {
                        edgeStack.Push(i);
                        stack.Push(_nodes[currentNodeConnections[i]]);
                        nextFound = true;
                        break;
                    }
                }

                if (!nextFound) 
                {
                    stack.Pop();
                    if(edgeStack.Count > 0)
                        edgeStack.Pop();
                }
            }
            newMesh.subMeshCount = submeshCount;
            //Debug.Log($"new mesh subs: {newMesh.subMeshCount}");
            List<Vector3> newVerts = new List<Vector3>(newMesh.vertices);
            List<Vector2> newUVs = new List<Vector2>(newMesh.uv);
            newMesh.RecalculateBounds();
            for (int i = 0; i < subMeshTriangleSets.Length; i++)
            {
                (newVerts, newUVs, subMeshTriangleSets[i]) = Optimise(newVerts, newUVs, subMeshTriangleSets[i], newMesh.bounds);
                newMesh.vertices = newVerts.ToArray();
                newMesh.SetTriangles(subMeshTriangleSets[i], i);
                //Debug.Log("Setting striangles");
            }
            newMesh.RecalculateBounds();
            newMesh.RecalculateNormals();
            newMesh.MarkDynamic();
            //newMesh.RecalculateTangents();
            return newMesh;
        }

        (List<Vector3> verts, List<Vector2> uv, List<int> tris) Optimise(List<Vector3> verts, List<Vector2> uv, List<int> tris, Bounds bounds)
        {
            //Some optimisation
            if(_useFlatshade)
                FlatShade(verts, uv, tris, bounds);
            int oldVerts = verts.Count;
            RemoveRedundantVerts(verts, uv, tris);
            //Debug.Log($"Verts culled: {oldVerts - verts.Count}");
            return (verts, uv, tris);
        }

        (List<Vector3> verts, List<Vector2> uv, List<int> tris) FlatShade(List<Vector3> verts, List<Vector2> uv, List<int> tris, Bounds bounds)
        {
            List<int> seen = new List<int>();
            int originalCount = tris.Count;

            for (int i = 0; i < originalCount; i++)
            {
                if (seen.Contains(tris[i]))
                {
                    verts.Add(verts[tris[i]]);
                    uv.Add(uv[tris[i]]);
                    tris[i] = verts.Count - 1;
                }
                seen.Add(tris[i]);
            }
            //for (int i = 0; i < uv.Count; i++)
            //{
            //    uv[i] = new Vector2(
            //        Mathf.InverseLerp(bounds.min.x, bounds.max.x, verts[i].x),
            //        Mathf.InverseLerp(bounds.min.y, bounds.max.y, verts[i].y));
            //}
            //Debug.Log($"verts {verts.Count}, uvs {uv.Count}");
            return (verts, uv, tris);
        }

        (List<Vector3> verts, List<Vector2> uv, List<int> tris) RemoveRedundantVerts(List<Vector3> verts, List<Vector2> uv, List<int> tris)
        {
            List<int> unseen = new List<int>(verts.Count);
            //Build unseen list
            for (int i = 0; i < unseen.Count; i++)
            {
                unseen[i] = i;
            }

            //Remove seen
            for (int i = 0; i < tris.Count; i++)
            {
                if (unseen.Contains(tris[i]))
                    unseen.Remove(tris[i]);
            }

            //Put in ascending order
            unseen.Sort();

            //Remove redundant verts
            for (int i = verts.Count - 1; i >= 0; i--)
            {
                if (unseen.Contains(i))
                {
                    verts.RemoveAt(i);
                    uv.RemoveAt(i);
                }
            }

            //Offset triangles
            for (int i = 0; i < tris.Count; i++)
            {
                int offset = 0;
                for (int j = 0; j < unseen.Count; j++)
                {
                    if (tris[i] > unseen[j])
                        offset++;
                    else
                        break;
                }
                tris[i] -= offset;
            }

            return (verts, uv, tris);
        }



        Mesh JoinMeshes(Mesh meshA, Mesh meshB, int subdiv, List<int> submeshA, List<int> submeshB, Matrix4x4 transformationMatrix, Vector3[] connectingLoop)
        {
            int vertCount = meshA.vertexCount;

            //Debug.Log($"connecting loop: {connectingLoop.Length} ");

            if (vertCount == 0)
            {
                submeshB.AddRange(meshB.triangles);
                return meshB;
            }

            //Merge into one mesh and reposition mb

            int loopLen = 4 * (int)Math.Pow(2, subdiv);

            //UVS
            List<Vector2> uvsA = new List<Vector2>();
            List<Vector2> uvsB = new List<Vector2>();
            meshA.GetUVs(0, uvsA);
            meshB.GetUVs(0, uvsB);
            uvsA.AddRange(uvsB);

            //VERTS
            List<Vector3> newVerts = new List<Vector3>(meshA.vertices);
            if (connectingLoop != null)
            {
                newVerts.AddRange(connectingLoop);
                uvsA.AddRange(new Vector2[connectingLoop.Length]);
            }
            int vertCountPostConLoop = newVerts.Count;

            Vector3[] bVerts = meshB.vertices;
            for (int i = 0; i < bVerts.Length; i++)
            {
                bVerts[i] = transformationMatrix.MultiplyPoint3x4(bVerts[i]);
                //bVerts[i].x -= 1f;
            }

            //TRIS
            int[] bTriangs = meshB.triangles;
            for (int i = 0; i < bTriangs.Length; i++)
            {
                bTriangs[i] += vertCountPostConLoop;
            }
            newVerts.AddRange(bVerts);
            meshA.vertices = newVerts.ToArray();

            List<int> newTriangs = new List<int>();
            for (int i = 0; i < loopLen; i++)
            {
                int nxt1 = (vertCountPostConLoop) + (i + 1);
                if (nxt1 >= vertCountPostConLoop + loopLen)
                    nxt1 = vertCountPostConLoop;

                int nxt2 = (vertCountPostConLoop - loopLen) + (i + 1);
                if (nxt2 >= vertCountPostConLoop)
                    nxt2 = vertCountPostConLoop - loopLen;

                newTriangs.AddRange(new int[6] { (vertCountPostConLoop) + i, nxt1, nxt2,
                                                 (vertCountPostConLoop) + i, nxt2, (vertCountPostConLoop - loopLen) + i });
            }
            //newTriangs.InsertRange(0, meshA.triangles);
            //meshA.triangles = newTriangs.ToArray();
            if(submeshA != null)
                submeshA.AddRange(newTriangs);
            submeshB.AddRange(bTriangs);
            //var subMeshDesc = new UnityEngine.Rendering.SubMeshDescriptor(){firstVertex = meshA.vertexCount,
            //                                                                vertexCount = newVerts.Count - meshA.vertexCount - 1};
            //outMesh.SetSubMesh(submeshIndex, subMeshDesc);

            //Debug.Log(newMesh.vertexCount);
            meshA.SetUVs(0, uvsA);
            return meshA;
        }


        public List<SElement.BezierPoint> GetBezierPoints()
        {
            List<SElement.BezierPoint> points = new List<SElement.BezierPoint>();
            for (int i = 0; i < _nodes.Length; i++)
            {
                points.AddRange(_nodes[i].GetBezierPoints());
            }
            return points;
        }
    }

    [Serializable]
    public struct NodeParams
    {
        [Min(0)]
        public int nLoops;
        [Min(0f)]
        public float rounding;
        public Vector3 size;
        [Range(0f,1f)]
        public float relativeForwardTaper;
        [Range(0f,1f)]
        public float relativeBackwardTaper;
        public Vector3[] deforms;
        public BezierParams curveParams;
        public int subMeshIndex;
        [HideInInspector]
        public VisibleFaces visibleFaces;
        [HideInInspector]
        public bool curveNotAffectNormal;

        public NodeParams(
            int loops, 
            float rounding, 
            Vector3 size, 
            float relForwardTaper, 
            float relBackwardTaper, 
            Vector3[] deformations, 
            BezierParams splineParams, 
            int subMeshIndex, 
            VisibleFaces visibleFaces,
            bool curveNotAffectNormal)
        {
            this.nLoops = loops;
            this.rounding = rounding;
            this.size = size;
            this.relativeForwardTaper = relForwardTaper;
            this.relativeBackwardTaper = relBackwardTaper;
            this.deforms = deformations;
            this.curveParams = splineParams;
            this.subMeshIndex = subMeshIndex;
            this.visibleFaces = visibleFaces;
            this.curveNotAffectNormal = curveNotAffectNormal;
        }

        public static NodeParams defaultParams
        {
            get
            {
                return new NodeParams(0, 0f, new Vector3(1f,1f,1f), 1f, 1f, new Vector3[8], new BezierParams(), 0, new VisibleFaces(), false);
            }
        }
    }

    [Serializable]
    public struct BezierParams
    {
        public Vector2 controlPoint;
        public Vector2 tipOffset;
    }

    [Serializable]
    public struct VisibleFaces
    {
        public bool bottom;
        public bool top;
        public bool left;
        public bool front;
        public bool right;
        public bool back;
    }

    [Serializable]
    public abstract class SElement
    {
        [SerializeField] NodeParams _storedParameters;
        protected Mesh _mesh;
        public Mesh GetMesh() => _mesh;
        protected FacePlanarNormals facePlanarNormals;
        public int GetSubmeshIndex() => _storedParameters.subMeshIndex;
        protected List<BezierPoint> _bezierPoints = new List<BezierPoint>();
        public List<BezierPoint> GetBezierPoints() => _bezierPoints;

        public ConnectionData GetConnectionData(int face)
        {
            ConnectionData newConnectionData = new ConnectionData();
            switch (face)
            {
                case 0:
                    newConnectionData = new ConnectionData(
                        facePlanarNormals.t_centre, 
                        Quaternion.LookRotation(Vector3.forward, facePlanarNormals.t), 
                        facePlanarNormals.t_loop);
                    break;
                case 1:
                    newConnectionData = new ConnectionData(
                        facePlanarNormals.l_centre, 
                        Quaternion.LookRotation(Vector3.down, facePlanarNormals.l), 
                        facePlanarNormals.l_loop);
                    break;
                case 2:
                    newConnectionData = new ConnectionData(
                        facePlanarNormals.f_centre, 
                        Quaternion.LookRotation(Vector3.down, facePlanarNormals.f), 
                        facePlanarNormals.f_loop);
                    break;
                case 3:
                    newConnectionData = new ConnectionData(
                        facePlanarNormals.r_centre, 
                        Quaternion.LookRotation(Vector3.down, facePlanarNormals.r), 
                        facePlanarNormals.r_loop);
                    break;
                case 4:
                    newConnectionData = new ConnectionData(
                        facePlanarNormals.ba_centre, 
                        Quaternion.LookRotation(Vector3.down, facePlanarNormals.ba), 
                        facePlanarNormals.ba_loop);
                    break;
                case 5:
                    newConnectionData = new ConnectionData(
                        facePlanarNormals.bo_centre,
                        Quaternion.LookRotation(Vector3.down, facePlanarNormals.bo),
                        facePlanarNormals.bo_loop);
                    break;
            }

            return newConnectionData;
        }
        public struct ConnectionData
        {
            public Vector3 position;
            public Quaternion direction;
            public Matrix4x4 transformationMatrix
            {
                get
                {
                    return Matrix4x4.TRS(position, direction, Vector3.one);
                }
            }

            public Vector3[] edgeLoop;
            public Vector3[] GetEdgeLoop(Matrix4x4 transformationMatrix)
            {
                if (edgeLoop == null)
                    return new Vector3[0];
                Vector3[] transformedLoop = new Vector3[edgeLoop.Length];
                for (int i = 0; i < transformedLoop.Length; i++)
                {
                    transformedLoop[i] = transformationMatrix.MultiplyPoint3x4(edgeLoop[i]);
                }
                return transformedLoop;
            }
            public ConnectionData(Vector3 position, Quaternion direction, Vector3[] edgeLoop)
            {
                this.position = position;
                this.direction = direction;
                this.edgeLoop = edgeLoop;
            }
        }
        public virtual void Build(int subdivs)
        {
            Build(subdivs, _storedParameters);
        }
        public void Build(int subdiv, NodeParams parameters)
        {
            _storedParameters = parameters;
            Build(subdiv, parameters.rounding, parameters.size, parameters.relativeForwardTaper, parameters.relativeBackwardTaper, parameters.nLoops, parameters.deforms, parameters.curveParams, parameters.visibleFaces);
        }
        public abstract void Build(int subdiv, float rounding, Vector3 size, float relativeForwardTaper, float relativeBackwardTaper, int nLoops, Vector3[] deforms, BezierParams sParams, VisibleFaces visibleFaces);
        protected Vector3[] BuildLoop(int loopLen, int nLoops, int index, Vector3 size, Vector3[] deforms, float taperScale)
        {
            Vector3[] verts = new Vector3[loopLen];

            //Determine loop curve offset (excl first and last loop)
            BezierPoint bezierPoint;

            Vector3 origin = Vector3.zero;
            Vector3 control =   new Vector3(_storedParameters.curveParams.controlPoint.x, 
                                            _storedParameters.size.y / 2, 
                                            _storedParameters.curveParams.controlPoint.y);
            Vector3 end =       new Vector3(_storedParameters.curveParams.tipOffset.x,
                                            _storedParameters.size.y,
                                            _storedParameters.curveParams.tipOffset.y);


            float t = Mathf.InverseLerp(0, nLoops - 1, index);
            bezierPoint = SampleQuadraticBezierPoint(t, origin, control, end);

            if (index == 0)
                bezierPoint = new BezierPoint();
            
            Vector3 curvePos = bezierPoint.position;
            Vector3 curveTangent = bezierPoint.tangent;
            Vector3 curveNormal = bezierPoint.normal;

            float yOffset = (size.y / (nLoops - 1)) * index;
            int quartOfLength = verts.Length / 4;
      
            verts[0] =                 new Vector3(curvePos.x + (-size.x / 2),
                                                   curvePos.y,
                                                   curvePos.z + (-size.z / 2)) + (deforms[0] * taperScale);
            verts[quartOfLength] =     new Vector3(curvePos.x + (size.x / 2),
                                                   curvePos.y,
                                                   curvePos.z + (-size.z / 2)) + (deforms[quartOfLength] * taperScale);
            verts[quartOfLength * 2] = new Vector3(curvePos.x + (size.x / 2),
                                                   curvePos.y,
                                                   curvePos.z + (size.z / 2)) + (deforms[quartOfLength * 2] * taperScale);
            verts[quartOfLength * 3] = new Vector3(curvePos.x + (-size.x / 2),
                                                   curvePos.y,
                                                   curvePos.z + (size.z / 2)) + (deforms[quartOfLength * 3] * taperScale);


            //Debug.Log($"vertsL= {verts.Length}");
            //Between 0-1
            for (int j = 1; j < quartOfLength; j++)
            {
                Vector3 baseOffset = new Vector3(
                    curvePos.x + (-size.x / 2) + ((size.x / quartOfLength) * j),
                    curvePos.y,
                    curvePos.z + (-size.z / 2)
                    );
                verts[j] = baseOffset + (deforms[j] * taperScale);
            //Debug.Log($"j1= {j}");
            }
            //Between 1-2
            for (int j = quartOfLength + 1; j < quartOfLength * 2; j++)
            {
                Vector3 baseOffset = new Vector3(
                   curvePos.x + size.x / 2,
                   curvePos.y,
                   curvePos.z + (-size.z / 2) + ((size.z / quartOfLength) * (j - quartOfLength))
                   );
                verts[j] = baseOffset + (deforms[j] * taperScale);
            //Debug.Log($"j2= {j}");
            }
            //Between 2-3
            for (int j = (quartOfLength * 2) + 1; j < quartOfLength * 3; j++)
            {
                Vector3 baseOffset = new Vector3(
                    curvePos.x + (size.x / 2) - ((size.x / quartOfLength) * (j - (quartOfLength * 2))),
                    curvePos.y,
                    curvePos.z + (size.z / 2)
                    );
                verts[j] = baseOffset + (deforms[j] * taperScale);
            //Debug.Log($"j3= {j}");
            }
            //Between 3-0
            for (int j = (quartOfLength * 3) + 1; j < verts.Length; j++)
            {
                Vector3 baseOffset = new Vector3(
                    curvePos.x + -size.x / 2,
                    curvePos.y,
                    curvePos.z + (size.z / 2) - ((size.z / quartOfLength) * (j - (quartOfLength * 3)))
                    );
                verts[j] = baseOffset + (deforms[j] * taperScale);
            //Debug.Log($"j4= {j}");
            }


            if (bezierPoint.tangent != Vector3.zero && bezierPoint.normal != Vector3.zero)
            {
                _bezierPoints.Add(bezierPoint);

                if (!_storedParameters.curveNotAffectNormal)
                {
                    Matrix4x4 curveRotationMatrix = Matrix4x4.Rotate(Quaternion.LookRotation(bezierPoint.normal, bezierPoint.tangent));

                    for (int i = 0; i < verts.Length; i++)
                    {
                        verts[i] = curveRotationMatrix.MultiplyPoint3x4(verts[i]);
                    }
                }
            }


            return verts;
        }
        protected int[] BuildTriangles(int loopLen, int loopIndex, int vertIndex)
        {
            int nxt1 = (loopLen * loopIndex) + (vertIndex + 1);
            if (nxt1 >= loopLen * (loopIndex + 1))
                nxt1 = loopLen * loopIndex;

            int nxt2 = (loopLen * (loopIndex - 1)) + (vertIndex + 1);
            if (nxt2 >= loopLen * loopIndex)
                nxt2 = loopLen * (loopIndex - 1);

            //Clockwise!!!
            int[] tris = new int[] { (loopLen * loopIndex) + vertIndex, nxt1, nxt2,
                                             (loopLen * loopIndex) + vertIndex, nxt2, (loopLen * (loopIndex - 1)) + vertIndex };

            return tris;
        }
        protected float GetRoundingAmount(int[][] roundingRanges, int roundingRangeI, int roundingRangeJ)
        {
            float roundingAmount = 0f;
            if (roundingRangeJ > roundingRanges[roundingRangeI].Length / 2)
            {
                roundingAmount = 1f - Mathf.InverseLerp(roundingRanges[roundingRangeI].Length / 2, roundingRanges[roundingRangeI].Length, roundingRangeJ);
            }
            else
            {
                roundingAmount = Mathf.InverseLerp(-1, roundingRanges[roundingRangeI].Length / 2, roundingRangeJ);
            }
            return roundingAmount;
        }
        protected Vector3[] Redeform(Vector3[] deforms, int targetLength, out int[][] roundingRanges)
        {
            Vector3[] newD = new Vector3[targetLength];
            int factorDiff = 0;
            int dLen = deforms.Length;
            int counter = 0;
            while (dLen != targetLength)
            {
                if (counter > 10)
                {
                    dLen = targetLength;
                    throw new Exception("deforms is not an acceptable length");
                }
                if (dLen > targetLength)
                {
                    dLen /= 2;
                    factorDiff--;
                }
                else if (dLen < targetLength)
                {
                    dLen *= 2;
                    factorDiff++;
                }
                counter++;
            }
            //Reduce
            roundingRanges = new int[0][];
            if (factorDiff < 0)
            {
                int factorDiffPositive = factorDiff * -1;
                int interval = (int)Math.Pow(2, factorDiffPositive);
                int index = 0;
                for (int i = 0; i < deforms.Length; i += interval)
                {
                    newD[index] = deforms[i];
                    index++;
                }
            }
            //Interpolate
            else if (factorDiff > 0)
            {
                int index = 0;
                int interval = (int)Math.Pow(2, factorDiff);
                int[] keyVerts = new int[deforms.Length];
                roundingRanges = new int[deforms.Length][];
                for (int i = 0; i < newD.Length; i++)
                {
                    if (i % interval == 0)
                    {
                        keyVerts[index] = i;
                        newD[i] = deforms[index];
                        index++;
                    }
                }

                for (int i = 0; i < keyVerts.Length; i++)
                {
                    int nextKV = i + 1;
                    int roundingCounter = 0;
                    roundingRanges[i] = new int[interval - 1];
                    if (nextKV == keyVerts.Length)
                    {
                        nextKV = 0;
                        for (int j = 1; j < keyVerts[1]; j++)
                        {
                            roundingRanges[i][roundingCounter] = keyVerts[i] + j;
                            newD[keyVerts[i] + j] = new Vector3(
                                Mathf.Lerp(newD[keyVerts[i]].x, newD[keyVerts[nextKV]].x, (1f / interval) * (j)),
                                Mathf.Lerp(newD[keyVerts[i]].y, newD[keyVerts[nextKV]].y, (1f / interval) * (j)),
                                Mathf.Lerp(newD[keyVerts[i]].z, newD[keyVerts[nextKV]].z, (1f / interval) * (j)));
                            roundingCounter++;
                        }
                    }
                    else
                    {
                        for (int j = keyVerts[i] + 1; j < keyVerts[nextKV]; j++)
                        {
                            roundingRanges[i][roundingCounter] = j;
                            newD[j] = new Vector3(
                                Mathf.Lerp(newD[keyVerts[i]].x, newD[keyVerts[nextKV]].x, (1f / interval) * (j - keyVerts[i])),
                                Mathf.Lerp(newD[keyVerts[i]].y, newD[keyVerts[nextKV]].y, (1f / interval) * (j - keyVerts[i])),
                                Mathf.Lerp(newD[keyVerts[i]].z, newD[keyVerts[nextKV]].z, (1f / interval) * (j - keyVerts[i])));
                            roundingCounter++;
                        }
                    }
                }
            }
            return newD;
        }
        protected BezierPoint SampleQuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            BezierPoint result = new BezierPoint();
            //Get point
            float omt = 1f - t;
            float omt2 = omt * omt;
            float t2 = t * t;

            result.position = p0 * omt2 +
                              p1 * (2f * omt * t) +
                              p2 * t2;

            //Get tangent
            result.tangent = 2f * (1f - t) * (p1 - p0) + 2f * t * (p2 - p1);

            //Get normal
            Vector3 binormal = Vector3.Cross(Vector3.forward, result.tangent).normalized;
            result.normal = Vector3.Cross(result.tangent, binormal).normalized;

            return result;
        }

        public struct BezierPoint
        {
            public Vector3 position;
            public Vector3 tangent;
            public Vector3 normal;
        }

        protected FacePlanarNormals GenerateFacePlanarNormals(int nLoops, Vector3[] verts, int loopLen, int capLoopLen)
        {
            int quartOfLength = loopLen / 4;
            var a  = verts[0];
            var b  = verts[quartOfLength];
            var c  = verts[quartOfLength * 2];
            var d  = verts[quartOfLength * 3];
            var a1 = verts[(nLoops - 1) * loopLen];
            var b1 = verts[((nLoops - 1) * loopLen) + quartOfLength];
            var c1 = verts[((nLoops - 1) * loopLen) + (quartOfLength * 2)];
            var d1 = verts[((nLoops - 1) * loopLen) + (quartOfLength * 3)];


            //Cube edges
            List<Vector3> e01 = new List<Vector3>();
            List<Vector3> e02 = new List<Vector3>();
            List<Vector3> e03 = new List<Vector3>();
            List<Vector3> e04 = new List<Vector3>();
            List<Vector3> e05 = new List<Vector3>();
            List<Vector3> e06 = new List<Vector3>();
            List<Vector3> e07 = new List<Vector3>();
            List<Vector3> e08 = new List<Vector3>();
            List<Vector3> e09 = new List<Vector3>();
            List<Vector3> e10 = new List<Vector3>();
            List<Vector3> e11 = new List<Vector3>();
            List<Vector3> e12 = new List<Vector3>();

            List<Vector3> bottom_loop = new List<Vector3>();
            List<Vector3> top_loop = new List<Vector3>();
            List<Vector3> left_loop = new List<Vector3>();
            List<Vector3> front_loop = new List<Vector3>();
            List<Vector3> right_loop = new List<Vector3>();
            List<Vector3> back_loop = new List<Vector3>();

            int startOffset = 0;
            //if (storedParameters.visibleFaces.bottom)
            //    startOffset = 1;

            int endOffset = 0;
            if (_storedParameters.visibleFaces.top)
                endOffset = capLoopLen;


            //Debug.Log($"for length : {verts.Length - endOffset}, end offset : {endOffset}");

            for (int i = 0; i < verts.Length - endOffset; i++)
            {
                if(i >= 0 && i < quartOfLength)
                {
                    e01.Add(verts[i + startOffset]);
                }

                if (i >= quartOfLength && i < quartOfLength * 2)
                {
                    e02.Add(verts[i + startOffset]);
                }

                if (i >= quartOfLength * 2 && i < quartOfLength * 3)
                {
                    e03.Add(verts[i + startOffset]);
                }

                if (i >= quartOfLength * 3 && i < loopLen)
                {
                    e04.Add(verts[i + startOffset]);
                }

                if (i >= (nLoops - 1) * loopLen && i < ((nLoops - 1) * loopLen) + quartOfLength)
                {
                    e05.Add(verts[i + startOffset]);
                }

                if (i >= ((nLoops - 1) * loopLen) + quartOfLength && i < ((nLoops - 1) * loopLen) + (quartOfLength * 2))
                {
                    e06.Add(verts[i + startOffset]);
                }

                if (i >= ((nLoops - 1) * loopLen) + (quartOfLength * 2) && i < ((nLoops - 1) * loopLen) + (quartOfLength * 3))
                {
                    e07.Add(verts[i + startOffset]);
                }

                if (i >= ((nLoops - 1) * loopLen) + (quartOfLength * 3) && i <= ((nLoops - 1) * loopLen) + (quartOfLength * 4))
                {
                    e08.Add(verts[i + startOffset]);
                }

                int loopIndex = i % loopLen;
                if (loopIndex == 0)
                    e09.Add(verts[i + startOffset]);
                else if (loopIndex == quartOfLength)
                    e10.Add(verts[i + startOffset]);
                else if (loopIndex == quartOfLength * 2)
                    e11.Add(verts[i + startOffset]);
                else if (loopIndex == quartOfLength * 3)
                    e12.Add(verts[i + startOffset]);
            }

            ////Add verts that get missed by the loop
            //e04.Add(verts[0]);
            //e08.Add(verts[(nLoops - 1) * loopLen]);

            List<Vector3> e01_Rev = new List<Vector3>(e01);
            List<Vector3> e02_Rev = new List<Vector3>(e02);
            List<Vector3> e03_Rev = new List<Vector3>(e03);
            List<Vector3> e04_Rev = new List<Vector3>(e04);
            List<Vector3> e05_Rev = new List<Vector3>(e05);
            List<Vector3> e06_Rev = new List<Vector3>(e06);
            List<Vector3> e07_Rev = new List<Vector3>(e07);
            List<Vector3> e08_Rev = new List<Vector3>(e08);
            List<Vector3> e09_Rev = new List<Vector3>(e09);
            List<Vector3> e10_Rev = new List<Vector3>(e10);
            List<Vector3> e11_Rev = new List<Vector3>(e11);
            List<Vector3> e12_Rev = new List<Vector3>(e12);

            e01_Rev.Reverse();
            e02_Rev.Reverse();
            e03_Rev.Reverse();
            e04_Rev.Reverse();
            e05_Rev.Reverse();
            e06_Rev.Reverse();
            e07_Rev.Reverse();
            e08_Rev.Reverse();
            e09_Rev.Reverse();
            e10_Rev.Reverse();
            e11_Rev.Reverse();
            e12_Rev.Reverse();

            //Debug.Log($"e01: {e01.Count}");
            //Debug.Log($"e02: {e02.Count}");
            //Debug.Log($"e03: {e03.Count}");
            //Debug.Log($"e04: {e04.Count}");
            //Debug.Log($"e05: {e05.Count}");
            //Debug.Log($"e06: {e06.Count}");
            //Debug.Log($"e07: {e07.Count}");
            //Debug.Log($"e08: {e08.Count}");
            //Debug.Log($"e09: {e09.Count}");
            //Debug.Log($"e10: {e10.Count}");
            //Debug.Log($"e11: {e11.Count}");
            //Debug.Log($"e12: {e12.Count}");

            bottom_loop.AddRange(e01);
            bottom_loop.AddRange(e02);
            bottom_loop.AddRange(e03);
            bottom_loop.AddRange(e04);
            bottom_loop = bottom_loop.Distinct().ToList();
            bottom_loop.Insert(0, bottom_loop.Last());
            bottom_loop.RemoveAt(bottom_loop.Count-1);

            top_loop.AddRange(e05);
            top_loop.AddRange(e06);
            top_loop.AddRange(e07);
            top_loop.AddRange(e08);
            top_loop = top_loop.Distinct().ToList();
            top_loop.Insert(0, top_loop.Last());
            top_loop.RemoveAt(top_loop.Count - 1);


            left_loop.AddRange(e08_Rev);
            left_loop.AddRange(e12_Rev);
            left_loop.AddRange(e04);
            left_loop.AddRange(e09);
            left_loop = left_loop.Distinct().ToList();
            left_loop.Insert(0, left_loop.Last());
            left_loop.RemoveAt(left_loop.Count - 1);


            front_loop.AddRange(e07_Rev);
            front_loop.AddRange(e11_Rev);
            front_loop.AddRange(e03);
            front_loop.AddRange(e12);
            front_loop = front_loop.Distinct().ToList();
            front_loop.Insert(0, front_loop.Last());
            front_loop.RemoveAt(front_loop.Count - 1);


            right_loop.AddRange(e06_Rev);
            right_loop.AddRange(e10_Rev);
            right_loop.AddRange(e02);
            right_loop.AddRange(e11);
            right_loop = right_loop.Distinct().ToList();
            right_loop.Insert(0, right_loop.Last());
            right_loop.RemoveAt(right_loop.Count - 1);

            back_loop.AddRange(e05_Rev);
            back_loop.AddRange(e09_Rev);
            back_loop.AddRange(e01);
            back_loop.AddRange(e10);
            back_loop = back_loop.Distinct().ToList();
            back_loop.Insert(0, back_loop.Last());
            back_loop.RemoveAt(back_loop.Count - 1);


            //Debug.Log($"left_loop count: {left_loop.Count}");
            //for (int i = 0; i < top_loop.Count; i++)
            //{
            //    Debug.Log($"{i}:{top_loop[i]}");
            //}


            facePlanarNormals = new FacePlanarNormals(
                bottom: Vector3.Cross(d - a + c - b, b - a + c - d).normalized,
                top: Vector3.Cross(a1 - d1 + b1 - c1, a1 - b1 + d1 - c1).normalized,
                left: Vector3.Cross(a - d + a1 - d1, a - a1 + d - d1).normalized,
                front: Vector3.Cross(d - c + d1 - c1, d - d1 + c - c1).normalized,
                right: Vector3.Cross(c - b + c1 - b1, c - c1 + b - b1).normalized,
                back: Vector3.Cross(b - a + b1 - a1, b - b1 + a - a1).normalized,
                bottom_centre: (a+b+c+d)/4,
                top_centre:    (a1+b1+c1+d1)/4,
                left_centre:   (a+a1+d+d1)/4,
                front_centre:  (c+c1+d+d1)/4,
                right_centre:  (b+b1+c+c1)/4,
                back_centre:   (a+a1+b+b1)/4,
                bottom_loop: bottom_loop.ToArray(),
                top_loop: top_loop.ToArray(),
                left_loop: left_loop.ToArray(),
                front_loop: front_loop.ToArray(),
                right_loop: right_loop.ToArray(),
                back_loop: back_loop.ToArray()
                );
            return facePlanarNormals;
        }
        public struct FacePlanarNormals
        {
            //Directions
            public Vector3 bo;
            public Vector3 t;
            public Vector3 l;
            public Vector3 f;
            public Vector3 r;
            public Vector3 ba;

            //Centres
            public Vector3 bo_centre;
            public Vector3 t_centre;
            public Vector3 l_centre;
            public Vector3 f_centre;
            public Vector3 r_centre;
            public Vector3 ba_centre;

            //Loops
            public Vector3[] bo_loop;
            public Vector3[] t_loop;
            public Vector3[] l_loop;
            public Vector3[] f_loop;
            public Vector3[] r_loop;
            public Vector3[] ba_loop;
            public FacePlanarNormals(Vector3 bottom, Vector3 top, Vector3 left, Vector3 front, Vector3 right, Vector3 back,
                                     Vector3 bottom_centre, Vector3 top_centre, Vector3 left_centre, Vector3 front_centre, Vector3 right_centre, Vector3 back_centre,
                                     Vector3[] bottom_loop, Vector3[] top_loop, Vector3[] left_loop, Vector3[] front_loop, Vector3[] right_loop, Vector3[] back_loop)
            {
                bo = bottom;
                t = top;
                l = left;
                f = front;
                r = right;
                ba = back;

                bo_centre = bottom_centre;
                t_centre = top_centre;
                l_centre = left_centre;
                f_centre = front_centre;
                r_centre = right_centre;
                ba_centre = back_centre;

                bo_loop = bottom_loop;
                t_loop = top_loop;
                l_loop = left_loop;
                f_loop = front_loop;
                r_loop = right_loop;
                ba_loop = back_loop;
            }

        }
        protected Vector2 TipOffset => _storedParameters.curveParams.tipOffset;

        protected float GetTaperScale(Vector3 size, int nLoops, int loopIndex, float relativeForwardTaper, float relativeBackwardTaper)
        {
            if (loopIndex > nLoops / 2)
                return Mathf.Clamp((Mathf.InverseLerp(nLoops - 1, nLoops / 2, loopIndex) + relativeForwardTaper), 0f, 1f);
            else if (loopIndex < nLoops / 2)
                return Mathf.Clamp((Mathf.InverseLerp(0, nLoops / 2, loopIndex) + relativeBackwardTaper), 0f, 1f);
            else
                return 1f;
        }
    }

    [Serializable]
    public class SSegment : SElement
    {
        public override void Build(int subdiv, float rounding, Vector3 size, float relativeForwardTaper, float relativeBackwardTaper, int nLoops, Vector3[] deforms, BezierParams sParams, VisibleFaces visibleFaces)
        {
            _mesh = new Mesh();
            int[][] roundingRanges = new int[0][];
            List<Vector3> allVerts = new List<Vector3>();
            int loopLen = 4 * (int)Math.Pow(2, subdiv);
            nLoops += 2;

            List<Vector2> uvs = new List<Vector2>();
            float uFraction = 1f / (loopLen);
            float vFraction = 1f / (nLoops + 1);
            //Ensure deforms matches looplen
            if (deforms.Length != loopLen)
            {
                deforms = Redeform(deforms, loopLen, out roundingRanges);
            }

            //Create Verts
            for (int i = 0; i < nLoops; i++)
            {
                //Taper
                float taperScale = GetTaperScale(size, nLoops, i, relativeForwardTaper, relativeBackwardTaper);
                Vector3 sizeAdjustedForTaper = size;
                sizeAdjustedForTaper.x *= taperScale;
                sizeAdjustedForTaper.z *= taperScale;

                Vector3[] newLoop = BuildLoop(loopLen, nLoops, i, sizeAdjustedForTaper, deforms, taperScale);
                allVerts.AddRange(newLoop);

                for (int j = 0; j < newLoop.Length; j++)
                {
                    uvs.Add(new Vector2(Mathf.InverseLerp(0, loopLen-1, j), Mathf.InverseLerp(0, nLoops - 1, i)));
                }

                //Add end-cap vert
                if (visibleFaces.top && i == nLoops - 1)
                {
                    Vector3 endCapVert = new Vector3();
                    for (int j = 0; j < newLoop.Length; j++)
                    {
                        endCapVert += newLoop[j];
                    }
                    endCapVert /= newLoop.Length;
                    allVerts.Add(endCapVert);
                    uvs.Add(new Vector2(.5f, .5f));
                }
            }

            //Create Tris
            List<int> allTris = new List<int>();
            for (int i = 1; i < nLoops; i++)
            {
                for (int j = 0; j < loopLen; j++)
                {
                    allTris.AddRange(BuildTriangles(loopLen, i, j));
                }
            }


            _mesh.vertices = allVerts.ToArray();
            _mesh.triangles = allTris.ToArray();
            _mesh.RecalculateNormals();

            //Apply rounding
            for (int i = 0; i < nLoops; i++)
            {
                for (int j = 0; j < roundingRanges.Length; j++)
                {
                    for (int k = 0; k < roundingRanges[j].Length; k++)
                    {
                        float roundingAmount = GetRoundingAmount(roundingRanges, j, k);
                        allVerts[roundingRanges[j][k] + (i * loopLen)] +=
                        _mesh.normals[roundingRanges[j][k] +
                        (i * loopLen)].normalized *
                        Mathf.Lerp(
                            0,
                            rounding,
                            roundingAmount + (rounding * .3f)
                        );
                    }
                }
            }
            int capLoopLen = visibleFaces.top ? 1 : 0;
            GenerateFacePlanarNormals(nLoops, allVerts.ToArray(), loopLen, capLoopLen);

            if (visibleFaces.bottom)
            {
                //Start-cap vert
                Vector3 startCapVert = new Vector3();
                for (int j = 0; j < loopLen; j++)
                {
                    startCapVert += allVerts[j];
                }
                startCapVert /= loopLen;
                allVerts.Insert(0, startCapVert);
                uvs.Add(new Vector2(.5f, .5f));

                //Shift tris up to account for start cap vert
                for (int i = 0; i < allTris.Count; i++)
                {
                    allTris[i]++;
                }

                //Start-cap triangles
                int[] startCap = new int[loopLen * 3];
                for (int i = 0; i < startCap.Length / 3; i++)
                {
                    if (i == loopLen - 1)
                    {
                        startCap[i * 3] = 0;
                        startCap[(i * 3) + 1] = i + 1;
                        startCap[(i * 3) + 2] = 1;
                    }
                    else
                    {
                        startCap[i * 3] = 0;
                        startCap[(i * 3) + 1] = i + 1;
                        startCap[(i * 3) + 2] = i + 2;
                    }
                }
                allTris.AddRange(startCap);
            }
            //End-cap triangles
            else if (visibleFaces.top)
            {
                int refPoint = nLoops - 1;
                int[] endCap = new int[loopLen * 3];
                for (int i = 0; i < endCap.Length / 3; i++)
                {
                    endCap[i * 3] = (refPoint * loopLen) + i;
                    endCap[(i * 3) + 1] = (refPoint + 1) * loopLen;
                    if (i == loopLen - 1)
                        endCap[(i * 3) + 2] = refPoint * loopLen;
                    else
                        endCap[(i * 3) + 2] = (refPoint * loopLen) + i + 1;
                }
                allTris.AddRange(endCap);
            }

            _mesh.vertices = allVerts.ToArray();
            _mesh.triangles = allTris.ToArray();
            _mesh.SetUVs(0, uvs.ToArray());
            _mesh.RecalculateNormals();
            _mesh.RecalculateTangents();
        }
    }

    [Serializable]
    public class SBranch : SElement
    {

        public override void Build(int subdiv, float rounding, Vector3 size, float relativeForwardTaper, float relativeBackwardTaper, int nLoops, Vector3[] deforms, BezierParams sParams, VisibleFaces visibleFaces)
        {
            _mesh = new Mesh();
            int[][] roundingRanges = new int[0][];
            List<Vector3> allVerts = new List<Vector3>();
            int loopLen = 4 * (int)Math.Pow(2, subdiv);
            int quartLen = loopLen / 4;
            nLoops = (loopLen / 4) + 1;

            List<Vector2> uvs = new List<Vector2>();
            float uFraction = 1f / loopLen;
            float vFraction = 1f / nLoops;

            //Ensure deforms matches looplen
            if (deforms.Length != loopLen)
            {
                deforms = Redeform(deforms, loopLen, out roundingRanges);
            }

            //Create Verts
            int lengthOfCap = 0;

            for (int i = 0; i < nLoops; i++)
            {
                //Taper
                //float taperScale = GetTaperScale(size, nLoops, i, relativeForwardTaper, relativeBackwardTaper);
                //Vector3 sizeAdjustedForTaper = size;
                //sizeAdjustedForTaper.x *= taperScale;
                //sizeAdjustedForTaper.z *= taperScale;

                Vector3[] newLoop = BuildLoop(loopLen, nLoops, i, size, deforms, 1f);
                allVerts.AddRange(newLoop);

                for (int j = 0; j < newLoop.Length; j++)
                {
                    uvs.Add(new Vector2(uFraction * j, vFraction * i));
                }

                //Add end-cap vert
                if (i == nLoops - 1 && visibleFaces.top)
                {
                    int capLoopLen = loopLen;
                    while(capLoopLen > 8)
                    {
                        capLoopLen -= 8;
                        Vector3 currentCapLoopSize = size * ((float)capLoopLen/(float)loopLen);
                        currentCapLoopSize.y = size.y;
                        Vector3[] currentCapLoop = BuildLoop(capLoopLen, nLoops, i, currentCapLoopSize, deforms, 1f);
                        for (int j = 0; j < currentCapLoop.Length; j++)
                        {
                            uvs.Add(new Vector2(uFraction * j, vFraction * i));
                        }
                        lengthOfCap += capLoopLen;
                        allVerts.AddRange(currentCapLoop);
                    }
                    if(loopLen >= 8)
                    {
                        Vector3 capLoopTip = new Vector3(0f, size.y, 0f);
                        lengthOfCap++;
                        allVerts.Add(capLoopTip);
                        uvs.Add(new Vector2(.5f, .5f));
                    }
                }
            }

            //Debug.Log($"NUMBER OF VERTS: {allVerts.Count}");

            //Create Tris
            List<int> allTris = new List<int>();
            for (int i = 1; i < nLoops; i++)
            {
                for (int j = 0; j < loopLen; j++)
                {
                    if (j >= 0 && j < quartLen && !visibleFaces.back)
                        continue;
                    if (j >= quartLen && j < quartLen * 2 && !visibleFaces.right)
                        continue;
                    if (j >= quartLen * 2 && j < quartLen * 3 && !visibleFaces.front)
                        continue;
                    if (j >= quartLen * 3 && j < quartLen * 4 && !visibleFaces.left)
                        continue;
                    allTris.AddRange(BuildTriangles(loopLen, i, j));
                }
            }


            _mesh.vertices = allVerts.ToArray();
            _mesh.triangles = allTris.ToArray();
            _mesh.RecalculateNormals();

            //Apply rounding
            for (int i = 0; i < nLoops; i++)
            {
                for (int j = 0; j < roundingRanges.Length; j++)
                {
                    for (int k = 0; k < roundingRanges[j].Length; k++)
                    {
                        float roundingAmount = GetRoundingAmount(roundingRanges, j, k);
                        allVerts[roundingRanges[j][k] + (i * loopLen)] +=
                        _mesh.normals[roundingRanges[j][k] +
                        (i * loopLen)].normalized *
                        Mathf.Lerp(
                            0,
                            rounding,
                            roundingAmount + (rounding * .3f)
                        );
                    }
                }
            }

            GenerateFacePlanarNormals(nLoops, allVerts.ToArray(), loopLen, lengthOfCap);

            //End-cap triangles
            if (visibleFaces.top)
            {
                if (allVerts.Count > (loopLen * nLoops) + 1)
                {
                    int refPoint = (nLoops * loopLen) - 1;
                    int quartLenSq = (quartLen - 1) * (quartLen - 1);
                    int currentFaceLen = quartLen;
                    int faceCount = 1;
                    int curFaceCounter = 0;
                    int offset = 0;

                    for (int i = 0; i < quartLenSq; i++)
                    {
                        int cur = refPoint + i;

                        int nxt1 = cur + 1;

                        int nxt2 = nxt1 - (loopLen - 1 - offset);

                        int nxt3 = cur - (loopLen - 1 - offset);

                        curFaceCounter++;

                        if (curFaceCounter == currentFaceLen)
                        {
                            offset += 2;
                            nxt1 = cur - (loopLen - 1 - (offset));
                            nxt2 = nxt1 - 1;
                            nxt3 = nxt1 - 2;
                            faceCount++;
                            curFaceCounter = 0;
                            if (i != quartLenSq - 1)
                                i--;
                        }

                        if (faceCount == 2)
                        {
                            faceCount = 0;
                            currentFaceLen -= 1;
                        }

                        allTris.AddRange(new int[6] { cur, nxt1, nxt2,
                                              cur, nxt2, nxt3 });
                    }
                }
                if (allVerts.Count < 16)
                {
                    allTris.AddRange(new int[6] {
                        allVerts.Count - 1, allVerts.Count - 2, allVerts.Count - 3,
                        allVerts.Count - 1, allVerts.Count - 3, allVerts.Count - 4
                    });
                }
                else
                {
                    //Last four polys
                    allTris.AddRange(new int[24] {
                        allVerts.Count - 1, allVerts.Count - 2, allVerts.Count - 3,
                        allVerts.Count - 1, allVerts.Count - 3, allVerts.Count - 4,
                        allVerts.Count - 1, allVerts.Count - 4, allVerts.Count - 5,
                        allVerts.Count - 1, allVerts.Count - 5, allVerts.Count - 6,
                        allVerts.Count - 1, allVerts.Count - 6, allVerts.Count - 7,
                        allVerts.Count - 1, allVerts.Count - 7, allVerts.Count - 8,
                        allVerts.Count - 1, allVerts.Count - 8, allVerts.Count - 9,
                        allVerts.Count - 1, allVerts.Count - 9, allVerts.Count - 2
                    });
                }
            }
            _mesh.vertices = allVerts.ToArray();
            _mesh.triangles = allTris.ToArray();
            _mesh.SetUVs(0, uvs.ToArray());
            _mesh.RecalculateNormals();
            _mesh.RecalculateTangents();
        }
    }

    [Serializable]
    public class STerminal : SElement
    {
        public override void Build(int subdiv, float rounding, Vector3 size, float relativeForwardTaper, float relativeBackwardTaper, int nLoops, Vector3[] deforms, BezierParams sParams, VisibleFaces visibleFaces)
        {
            _mesh = new Mesh();
            int[][] roundingRanges = new int[0][];
            List<Vector3> allVerts = new List<Vector3>();
            int loopLen = 4 * (int)Math.Pow(2, subdiv);
            nLoops += 2;

            //Ensure deforms matches looplen
            if (deforms.Length != loopLen)
            {
                deforms = Redeform(deforms, loopLen, out roundingRanges);
            }

            //Create Verts
            for (int i = 0; i < nLoops; i++)
            {
                //Taper
                float taperScale = GetTaperScale(size, nLoops, i, relativeForwardTaper, relativeBackwardTaper);
                Vector3 sizeAdjustedForTaper = size;
                sizeAdjustedForTaper.x *= taperScale;
                sizeAdjustedForTaper.z *= taperScale;

                Vector3[] newLoop = BuildLoop(loopLen, nLoops, i, sizeAdjustedForTaper, deforms, taperScale);
                allVerts.AddRange(newLoop);

                if(i == nLoops - 1)
                {
                    //Add end-cap vert
                    Vector3 endCapVert = new Vector3();
                    for (int j = 0; j < newLoop.Length; j++)
                    {
                        endCapVert += newLoop[j];
                    }
                    endCapVert /= newLoop.Length;
                    allVerts.Add(endCapVert);
                }
            }

            //Create Tris
            List<int> allTris = new List<int>();
            for (int i = 1; i < nLoops; i++)
            {
                for (int j = 0; j < loopLen; j++)
                {
                    allTris.AddRange(BuildTriangles(loopLen, i, j));
                }
            }


            _mesh.vertices = allVerts.ToArray();
            _mesh.triangles = allTris.ToArray();
            _mesh.RecalculateNormals();

            //Apply rounding
            for (int i = 0; i < nLoops; i++)
            {
                for (int j = 0; j < roundingRanges.Length; j++)
                {
                    for (int k = 0; k < roundingRanges[j].Length; k++)
                    {
                        float roundingAmount = GetRoundingAmount(roundingRanges, j, k);
                        allVerts[roundingRanges[j][k] + (i * loopLen)] +=
                        _mesh.normals[roundingRanges[j][k] +
                        (i * loopLen)].normalized *
                        Mathf.Lerp(
                            0,
                            rounding,
                            roundingAmount + (rounding * .3f)
                        );
                    }
                }
            }


            //End-cap triangles
            int refPoint = nLoops - 1;
            int[] endCap = new int[loopLen * 3];
            for (int j = 0; j < endCap.Length / 3; j++)
            {
                endCap[j * 3] = (refPoint * loopLen) + j;
                endCap[(j * 3) + 1] = (refPoint + 1) * loopLen;
                if (j == loopLen - 1)
                    endCap[(j * 3) + 2] = refPoint * loopLen;
                else
                    endCap[(j * 3) + 2] = (refPoint * loopLen) + j + 1;
            }
            allTris.AddRange(endCap);

            _mesh.vertices = allVerts.ToArray();
            _mesh.triangles = allTris.ToArray();
            _mesh.RecalculateNormals();
            _mesh.RecalculateTangents();
        }
    }
}
