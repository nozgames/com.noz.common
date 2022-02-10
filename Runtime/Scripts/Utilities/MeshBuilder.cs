using System;
using System.Collections.Generic;
using UnityEngine;

namespace NoZ
{
    /// <summary>
    /// Helper class for building meshes at runtime.
    /// </summary>
    public class MeshBuilder
    {
        private List<Vector3> _vertices = new List<Vector3>();
        private List<Vector2> _uvs = new List<Vector2>();
        private List<Vector3> _normals = new List<Vector3>();
        private List<int> _triangles = new List<int>();
        private List<Tuple<int, int>> _submeshes = new List<Tuple<int,int>>();

        private int _convexBegin = 0;

        /// <summary>
        /// Get the current number of vertices in the mesh being built
        /// </summary>
        public int VertexCount => _vertices.Count;

        /// <summary>
        /// Get the current number of triangles in the mesh being built
        /// </summary>
        public int TriangleCount => _triangles.Count;

        /// <summary>
        /// Begin a new submesh.  Note that you do not need to explicitly end a 
        /// submesh, instead another one to end the previous one.
        /// </summary>
        public void BeginSubmesh ()
        {
            _submeshes.Add(new Tuple<int,int>(VertexCount, TriangleCount));
        }

        /// <summary>
        /// Begin a convex shape
        /// </summary>
        public void BeginConvex ()
        {
            _convexBegin = _vertices.Count;
        }

        /// <summary>
        /// End a convex shape by adding the necessary triangles for each Vertex that was added
        /// </summary>
        public void EndConvex ()
        {
            var endIndex = _vertices.Count;
            for (var i = _convexBegin + 2; i < endIndex; i++)
                AddTriangle(_convexBegin, i - 1, i);
        }

        /// <summary>
        /// Explicitly add a triangle
        /// </summary>
        /// <param name="v0">Index of vertex 0</param>
        /// <param name="v1">Index of vertex 1</param>
        /// <param name="v2">Index of vertex 2</param>
        public void AddTriangle (int v0, int v1, int v2)
        {
            _triangles.Add(v0);
            _triangles.Add(v1);
            _triangles.Add(v2);
        }

        /// <summary>
        /// Add a vertex with a UV ad Normal
        /// </summary>
        /// <param name="vertex">Vertex to add</param>
        /// <param name="uv">UV of vertex</param>
        /// <param name="normal">Normal of vertex</param>
        /// <returns>Index of vertex added</returns>
        public int AddVertex (Vector3 vertex, Vector2 uv, Vector3 normal)
        {
            _vertices.Add(vertex);
            _uvs.Add(uv);
            _normals.Add(normal);
            return _vertices.Count - 1;
        }

        /// <summary>
        /// Add a vertex with a UV 
        /// </summary>
        /// <param name="vertex">Vertex to add</param>
        /// <param name="uv">UV of vertex</param>
        /// <returns>Index of the vertex that was added</returns>
        /// <exception cref="InvalidOperationException">Thrown if previous calls to AddVertex included a normal</exception>
        public int AddVertex (Vector3 vertex, Vector2 uv)
        {
            if (_normals.Count > 0)
                throw new InvalidOperationException("Missing normal");

            _vertices.Add(vertex);
            _uvs.Add(uv);
            return _vertices.Count - 1;
        }

        /// <summary>
        /// Add a vertex
        /// </summary>
        /// <param name="vertex">Vertex to add</param>
        /// <returns>Index of the vertex that was added</returns>
        /// <exception cref="InvalidOperationException">Thrown if previous calls to AddVertex included a normal or UV</exception>
        public int AddVertex(Vector3 vertex)
        {
            if (_normals.Count > 0)
                throw new InvalidOperationException("Missing normal");
            if (_uvs.Count > 0)
                throw new InvalidOperationException("Missing uvs");

            _vertices.Add(vertex);
            return _vertices.Count - 1;
        }

        /// <summary>
        /// Create a Mesh object from the current builder state
        /// </summary>
        public Mesh ToMesh ()
        {
            var mesh = new Mesh();
            mesh.vertices = _vertices.ToArray();
            mesh.normals = _normals.ToArray();
            mesh.uv = _uvs.ToArray();
            mesh.triangles = _triangles.ToArray();

            if (_submeshes.Count > 1)
            {
                mesh.subMeshCount = _submeshes.Count;

                for (int i = 1; i < _submeshes.Count; i++)
                {
                    var prev = _submeshes[i - 1];
                    var curr = _submeshes[i];
                    mesh.SetSubMesh(i - 1, new UnityEngine.Rendering.SubMeshDescriptor
                    {
                        baseVertex = 0,
                        firstVertex = prev.Item1,
                        vertexCount = curr.Item1 - prev.Item1,
                        indexStart = prev.Item2,
                        indexCount = curr.Item2 - prev.Item2
                    });
                }

                var last = _submeshes[_submeshes.Count - 1];
                mesh.SetSubMesh(mesh.subMeshCount-1, new UnityEngine.Rendering.SubMeshDescriptor
                {
                    baseVertex = 0,
                    firstVertex = last.Item1,
                    vertexCount = VertexCount - last.Item1,
                    indexStart = last.Item2,
                    indexCount = TriangleCount - last.Item2
                });
            }

            mesh.UploadMeshData(false);

            return mesh;
        }

        /// <summary>
        /// Reset the builder back to default state
        /// </summary>
        public void Clear()
        {
            _convexBegin = 0;
            _vertices.Clear();
            _normals.Clear();
            _uvs.Clear();
            _triangles.Clear();
        }
    }
}

