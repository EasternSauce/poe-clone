using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PoeClone.EditorTools
{
    /// <summary>Smooth implicit unions for connected palms, knuckles and digits, baked only in the Editor.</summary>
    internal sealed class AnatomyVolume
    {
        private struct Shape
        {
            public Vector3 A, B, Radii;
            public float Radius, EndRadius;
            public bool Capsule, Palm, Carve;
            public Vector3[] Points;
            public float[] PointRadii;

            public float Distance(Vector3 p)
            {
                if (Points != null)
                {
                    // One tube along the whole path: a hard minimum over segments that share their
                    // end radii is seamless, where blending separate capsules bulges at every joint.
                    float nearest = 1000;
                    for (int i = 0; i < Points.Length - 1; i++)
                    {
                        Vector3 ab = Points[i+1]-Points[i];
                        float t = Mathf.Clamp01(Vector3.Dot(p-Points[i],ab)/Mathf.Max(1e-8f,ab.sqrMagnitude));
                        nearest = Mathf.Min(nearest, (p-Points[i]-ab*t).magnitude-Mathf.Lerp(PointRadii[i],PointRadii[i+1],t));
                    }
                    return nearest;
                }
                if (Capsule)
                {
                    Vector3 ab = B-A;
                    float t = Mathf.Clamp01(Vector3.Dot(p-A,ab)/Mathf.Max(1e-8f,ab.sqrMagnitude));
                    return (p-A-ab*t).magnitude-Mathf.Lerp(Radius,EndRadius,t);
                }
                Vector3 q=p-A;
                if(Palm)
                {
                    float width=Mathf.Lerp(Radii.z,Radii.z*.66f,Mathf.Clamp01((q.y+Radii.y)/(2*Radii.y)));
                    Vector3 d=new Vector3(Mathf.Abs(q.x)-(Radii.x-Radius),Mathf.Abs(q.y)-(Radii.y-Radius),Mathf.Abs(q.z)-(width-Radius));
                    Vector3 outside=Vector3.Max(d,Vector3.zero);
                    return outside.magnitude+Mathf.Min(Mathf.Max(d.x,Mathf.Max(d.y,d.z)),0)-Radius;
                }
                Vector3 v=new Vector3(q.x/Radii.x,q.y/Radii.y,q.z/Radii.z);
                Vector3 w=new Vector3(v.x/Radii.x,v.y/Radii.y,v.z/Radii.z);
                float k=v.magnitude;
                if(k<.00001f) return -Mathf.Min(Radii.x,Mathf.Min(Radii.y,Radii.z));
                return k*(k-1)/Mathf.Max(.00001f,w.magnitude);
            }
        }

        private readonly float blendRadius;
        public AnatomyVolume(float blendRadius=.055f) => this.blendRadius=blendRadius;

        private readonly List<Shape> shapes = new List<Shape>();
        public void Palm(Vector3 center,Vector3 radii,float rounding) => shapes.Add(new Shape {A=center,Radii=radii,Radius=rounding,Palm=true});
        public void Ellipsoid(Vector3 center,Vector3 radii) => shapes.Add(new Shape {A=center,Radii=radii});
        public void Capsule(Vector3 a,Vector3 b,float radius,float endRadius) =>
            shapes.Add(new Shape {A=a,B=b,Radius=radius,EndRadius=endRadius,Capsule=true});
        public void Tube(IList<Vector3> points,Func<float,float> radius)
        {
            var radii=new float[points.Count];
            for(int i=0;i<radii.Length;i++) radii[i]=radius(i/(float)(radii.Length-1));
            shapes.Add(new Shape {Points=new List<Vector3>(points).ToArray(),PointRadii=radii});
        }
        /// <summary>Hollows an ellipsoid out of everything added before it (eye sockets, a nasal cavity).</summary>
        public void Carve(Vector3 center,Vector3 radii) => shapes.Add(new Shape {A=center,Radii=radii,Carve=true});

        /// <summary>Signed distance to the surface; negative inside.</summary>
        public float Distance(Vector3 point) => Field(point);

        private float Field(Vector3 point)
        {
            float distance=1000;
            foreach(Shape shape in shapes)
            {
                float next=shape.Distance(point);
                float blend=blendRadius;
                if(shape.Carve)
                {
                    // Smooth subtraction with a tighter fillet, so socket rims stay crisp.
                    float k=blend*.5f,c=Mathf.Clamp01(.5f-.5f*(distance+next)/k);
                    distance=Mathf.Lerp(distance,-next,c)+k*c*(1-c);
                    continue;
                }
                float h=Mathf.Max(blend-Mathf.Abs(distance-next),0)/blend;
                distance=Mathf.Min(distance,next)-h*h*blend*.25f;
            }
            return distance;
        }

        private static readonly int[,] Tetrahedra = {{0,1,2,6},{0,2,3,6},{0,3,7,6},{0,7,4,6},{0,4,5,6},{0,5,1,6}};
        private static readonly int[,] Edges = {{0,1},{0,2},{0,3},{1,2},{1,3},{2,3}};
        private static readonly int[] Dx={0,1,1,0,0,1,1,0}, Dy={0,0,1,1,0,0,1,1}, Dz={0,0,0,0,1,1,1,1};

        public Mesh Bake(string name,Vector3 minimum,Vector3 maximum,float spacing,Func<Vector3,Color> pigment)
        {
            int nx=Mathf.CeilToInt((maximum.x-minimum.x)/spacing),ny=Mathf.CeilToInt((maximum.y-minimum.y)/spacing),nz=Mathf.CeilToInt((maximum.z-minimum.z)/spacing);
            Vector3 step=new Vector3((maximum.x-minimum.x)/nx,(maximum.y-minimum.y)/ny,(maximum.z-minimum.z)/nz);
            int Index(int x,int y,int z) => (z*(ny+1)+y)*(nx+1)+x;
            Vector3 Position(int x,int y,int z) => minimum+Vector3.Scale(new Vector3(x,y,z),step);
            var field=new float[(nx+1)*(ny+1)*(nz+1)];
            for(int z=0;z<=nz;z++) for(int y=0;y<=ny;y++) for(int x=0;x<=nx;x++) field[Index(x,y,z)]=Field(Position(x,y,z));
            var vertices=new List<Vector3>();var triangles=new List<int>();
            var shared=new Dictionary<Vector3,int>();
            int Vertex(Vector3 p)
            {
                Vector3 key=new Vector3(Mathf.Round(p.x*100000),Mathf.Round(p.y*100000),Mathf.Round(p.z*100000));
                if(shared.TryGetValue(key,out int index)) return index;
                index=vertices.Count;vertices.Add(p);shared[key]=index;return index;
            }
            var cube=new Vector3[8];var values=new float[8];var polygon=new Vector3[4];var angles=new float[4];
            for(int z=0;z<nz;z++) for(int y=0;y<ny;y++) for(int x=0;x<nx;x++)
            {
                float lo=1000,hi=-1000;
                for(int i=0;i<8;i++) {cube[i]=Position(x+Dx[i],y+Dy[i],z+Dz[i]);values[i]=field[Index(x+Dx[i],y+Dy[i],z+Dz[i])];lo=Mathf.Min(lo,values[i]);hi=Mathf.Max(hi,values[i]);}
                if(lo>0 || hi<0) continue;
                for(int t=0;t<6;t++)
                {
                    int count=0;
                    for(int e=0;e<6;e++)
                    {
                        int a=Tetrahedra[t,Edges[e,0]],b=Tetrahedra[t,Edges[e,1]];
                        if((values[a]<0)==(values[b]<0)) continue;
                        polygon[count++]=Vector3.LerpUnclamped(cube[a],cube[b],values[a]/(values[a]-values[b]));
                    }
                    if(count<3) continue;
                    Vector3 center=Vector3.zero;for(int i=0;i<count;i++) center+=polygon[i];center/=count;
                    const float epsilon=.001f;
                    Vector3 normal=new Vector3(Field(center+Vector3.right*epsilon)-Field(center-Vector3.right*epsilon),Field(center+Vector3.up*epsilon)-Field(center-Vector3.up*epsilon),Field(center+Vector3.forward*epsilon)-Field(center-Vector3.forward*epsilon)).normalized;
                    Vector3 axis=(polygon[0]-center).normalized,other=Vector3.Cross(normal,axis);
                    for(int i=0;i<count;i++) {Vector3 delta=polygon[i]-center;angles[i]=Mathf.Atan2(Vector3.Dot(delta,other),Vector3.Dot(delta,axis));}
                    // A tetrahedron's intersection is convex. Sort its 3 or 4 corners around the outward normal.
                    for(int i=1;i<count;i++) for(int j=i;j>0 && angles[j]<angles[j-1];j--)
                    {float a=angles[j];angles[j]=angles[j-1];angles[j-1]=a;Vector3 p=polygon[j];polygon[j]=polygon[j-1];polygon[j-1]=p;}
                    for(int i=1;i<count-1;i++)
                    {
                        int a=Vertex(polygon[0]),b=Vertex(polygon[i]),c=Vertex(polygon[i+1]);
                        if(a==b || b==c || a==c) continue;
                        if(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).sqrMagnitude<1e-20f) continue;
                        triangles.Add(a);triangles.Add(b);triangles.Add(c);
                    }
                }
            }
            var uv=new Vector2[vertices.Count];var colors=new Color[vertices.Count];
            for(int i=0;i<vertices.Count;i++)
            {
                Vector3 p=vertices[i];uv[i]=new Vector2(p.z+.5f,p.y+.5f);colors[i]=pigment(p);
            }
            var mesh=new Mesh {name=name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.uv=uv;mesh.colors=colors;
            var normals=new Vector3[vertices.Count];
            const float normalStep=.001f;
            for(int i=0;i<vertices.Count;i++)
            {
                Vector3 p=vertices[i];
                normals[i]=new Vector3(Field(p+Vector3.right*normalStep)-Field(p-Vector3.right*normalStep),
                    Field(p+Vector3.up*normalStep)-Field(p-Vector3.up*normalStep),
                    Field(p+Vector3.forward*normalStep)-Field(p-Vector3.forward*normalStep)).normalized;
            }
            mesh.normals=normals;mesh.RecalculateTangents();mesh.RecalculateBounds();
            return mesh;
        }
    }
}
