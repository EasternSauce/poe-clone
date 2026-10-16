using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.EditorTools
{
    /// <summary>Continuous arm and leg surfaces weighted to the existing elbow/knee pivots.</summary>
    internal static class AnatomyLimbMeshes
    {
        internal static Mesh Arm(float side)
        {
            return Build("AnatomyConnectedArm"+(side<0?"L":"R"), true, new[] {
                R(.03f,0,0), R(.015f,.065f,.07f), R(-.02f,.087f,.09f),
                R(-.08f,.088f,.092f), R(-.19f,.08f,.085f), R(-.30f,.067f,.073f),
                R(-.38f,.061f,.064f), R(-.43f,.059f,.061f), R(-.49f,.060f,.063f,-.004f),
                R(-.56f,.055f,.058f,-.005f), R(-.63f,.044f,.046f),
                R(-.69f,.034f,.035f), R(-.735f,.027f,.028f), R(-.755f,.026f,.0265f), R(-.78f,0,0) },side);
        }

        internal static Mesh Leg()
        {
            return Build("AnatomyConnectedLeg", false, new[] {
                R(.10f,0,0), R(.07f,.09f,.10f), R(.00f,.115f,.125f),
                R(-.12f,.124f,.13f), R(-.24f,.112f,.12f,.006f),
                R(-.34f,.089f,.096f,.012f), R(-.42f,.078f,.088f,.016f),
                R(-.48f,.079f,.086f,.002f), R(-.55f,.089f,.098f,-.013f),
                R(-.62f,.085f,.095f,-.018f), R(-.70f,.066f,.075f,-.013f),
                R(-.78f,.048f,.053f), R(-.83f,.045f,.05f), R(-.86f,0,0) });
        }

        private static LowPolyMeshes.Ring R(float y,float x,float z,float forward=0) => new LowPolyMeshes.Ring(y,x,z,forward);

        private static Mesh Build(string name,bool arm,LowPolyMeshes.Ring[] sections,float side=0)
        {
            const int sides=32, subdivisions=3;
            var rings=new List<LowPolyMeshes.Ring>();
            for(int k=0;k<sections.Length-1;k++)
            {
                var a=sections[k];var b=sections[k+1];
                var before=sections[Mathf.Max(0,k-1)];var after=sections[Mathf.Min(sections.Length-1,k+2)];
                for(int j=0;j<subdivisions;j++)
                {
                    float t=j/(float)subdivisions;
                    float Curve(float p0,float p1,float p2,float p3) => Mathf.Max(0,.5f*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t));
                    rings.Add(R(Mathf.Lerp(a.Y,b.Y,t),Curve(before.X,a.X,b.X,after.X),Curve(before.Z,a.Z,b.Z,after.Z),Mathf.Lerp(a.Forward,b.Forward,t)));
                }
            }
            rings.Add(sections[sections.Length-1]);
            var positions=new List<Vector3>();var uv=new List<Vector2>();var weights=new List<BoneWeight>();
            var sleeve=new List<int>();var skin=new List<int>();
            foreach(var ring in rings)
                for(int column=0;column<=sides;column++)
                {
                    float u=column/(float)sides,angle=u*2*Mathf.PI;
                    float shoulder=arm?Mathf.Clamp01((ring.Y+.16f)/.19f):0;
                    positions.Add(new Vector3(Mathf.Sin(angle)*ring.X-side*.12f*shoulder,ring.Y,Mathf.Cos(angle)*ring.Z+ring.Forward));
                    uv.Add(new Vector2(u,(ring.Y+.86f)/.96f));
                    float lower=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.32f,-.52f,ring.Y));
                    float torso=arm?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.015f,-.15f,ring.Y)):0;
                    int firstBone=arm?1:0,secondBone=arm?2:1;
                    float firstWeight=1-lower,secondWeight=lower;
                    if(torso>0){firstBone=0;secondBone=1;firstWeight=torso;secondWeight=1-torso;}
                    if(secondWeight>firstWeight)
                    {
                        int swap=firstBone;firstBone=secondBone;secondBone=swap;
                        float weight=firstWeight;firstWeight=secondWeight;secondWeight=weight;
                    }
                    if(!arm && ring.Y<-.64f)
                    {
                        float ankle=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.64f,-.80f,ring.Y));
                        firstBone=ankle>.5f?2:1;secondBone=ankle>.5f?1:2;
                        firstWeight=Mathf.Max(ankle,1-ankle);secondWeight=1-firstWeight;
                    }
                    weights.Add(new BoneWeight {boneIndex0=firstBone,weight0=firstWeight,boneIndex1=secondBone,weight1=secondWeight});
                }
            void Tri(List<int> indices,int a,int b,int c)
            {
                if(Vector3.Cross(positions[b]-positions[a],positions[c]-positions[a]).sqrMagnitude<1e-16f) return;
                indices.Add(a);indices.Add(b);indices.Add(c);
            }
            for(int row=0;row<rings.Count-1;row++)
            {
                var target=arm && (rings[row].Y+rings[row+1].Y)*.5f>-.34f?sleeve:skin;
                for(int column=0;column<sides;column++)
                {
                    int a=row*(sides+1)+column,b=a+1,c=a+sides+1,d=c+1;
                    // Rows descend from shoulder/hip to wrist/ankle; +z is the front.
                    Tri(target,a,c,b);Tri(target,b,c,d);
                }
            }
            var mesh=new Mesh {name=name};mesh.SetVertices(positions);mesh.SetUVs(0,uv);
            mesh.subMeshCount=arm?2:1;
            if(arm){mesh.SetTriangles(sleeve,0);mesh.SetTriangles(skin,1);}else mesh.SetTriangles(skin,0);
            mesh.boneWeights=weights.ToArray();
            mesh.bindposes=arm?new[] {Matrix4x4.identity,Matrix4x4.identity,Matrix4x4.Translate(Vector3.up*.42f)}:
                new[] {Matrix4x4.identity,Matrix4x4.Translate(Vector3.up*.42f),Matrix4x4.Translate(Vector3.up*.80f)};
            mesh.RecalculateNormals();
            var normals=mesh.normals;
            for(int row=0;row<rings.Count;row++)
            {
                int first=row*(sides+1),last=first+sides;
                Vector3 normal=(normals[first]+normals[last]).normalized;
                normals[first]=normal;normals[last]=normal;
            }
            mesh.normals=normals;
            var colors=new Color[positions.Count];for(int i=0;i<colors.Length;i++)colors[i]=Color.white;
            mesh.colors=colors;mesh.RecalculateTangents();mesh.RecalculateBounds();
            return mesh;
        }
    }
}
