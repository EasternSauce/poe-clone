using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.EditorTools
{
    /// <summary>Deterministic anatomical surfaces. Coordinates are unit-sized; +z is the face.</summary>
    public static class AnatomyMeshes
    {
        private static AnatomyProfile profile;
        private static float EyeCenter => .185f * (profile != null ? profile.EyeSpacing : 1);
        // Separate from BodyHead/BodyLimb: those generic shapes also build armour and creatures.
        public static Dictionary<string, Mesh> Build()
        {
            profile = AnatomyProfile.Load();
            var meshes = new Dictionary<string, Mesh>();
            void Save(Mesh mesh) => meshes[mesh.name] = LowPolyMeshes.Save(mesh.name, mesh);
            foreach (bool detailed in new[] { true, false })
            {
                string suffix = detailed ? "" : "Low";
                Save(Head("AnatomyHead" + suffix, detailed ? 96 : 32, detailed ? 80 : 24));
                Save(Eyes("AnatomyEyes" + suffix, detailed ? 32 : 12));
                Save(Foot("AnatomyFoot" + suffix, detailed ? .035f : .075f));
                foreach (bool left in new[] { true, false })
                foreach (bool grip in new[] { false, true })
                    Save(Hand("AnatomyHand" + (left ? "L" : "R") + (grip ? "Grip" : "") + suffix,
                        left, grip, detailed ? 12 : 6));
            }
            Save(Scalp());
            Save(Limb("AnatomyUpperArm", 24, new[] {
                R(.5f, 0, 0), R(.46f, .28f, .31f), R(.32f, .49f, .48f), R(.12f, .46f, .5f, .025f),
                R(-.08f, .40f, .46f, .035f), R(-.30f, .32f, .34f), R(-.46f, .30f, .29f), R(-.5f, 0, 0) }));
            Save(Limb("AnatomyForearm", 24, new[] {
                R(.5f, 0, 0), R(.46f, .34f, .31f), R(.32f, .46f, .44f, -.025f), R(.12f, .49f, .46f, -.035f),
                R(-.12f, .38f, .35f), R(-.34f, .28f, .27f), R(-.48f, .27f, .24f), R(-.5f, 0, 0) }));
            Save(Limb("AnatomyThigh", 28, new[] {
                R(.5f, 0, 0), R(.45f, .40f, .40f), R(.28f, .5f, .49f), R(.06f, .46f, .5f, .03f),
                R(-.18f, .37f, .42f, .045f), R(-.36f, .29f, .32f, .035f), R(-.48f, .28f, .28f), R(-.5f, 0, 0) }));
            Save(Limb("AnatomyCalf", 28, new[] {
                R(.5f, 0, 0), R(.47f, .32f, .31f, .04f), R(.30f, .40f, .39f, -.035f), R(.1f, .49f, .5f, -.075f),
                R(-.1f, .40f, .43f, -.07f), R(-.30f, .25f, .29f, -.025f), R(-.46f, .23f, .24f), R(-.5f, 0, 0) }));
            return meshes;
        }

        private static LowPolyMeshes.Ring R(float y, float x, float z, float forward = 0) =>
            new LowPolyMeshes.Ring(y, x, z, forward);

        private static Mesh Limb(string name, int sides, LowPolyMeshes.Ring[] rings)
        {
            // Resample anatomical sections so the silhouette has continuous curvature.
            var sections=new List<LowPolyMeshes.Ring>();
            for(int k=0;k<rings.Length-1;k++)
            {
                var a=rings[k];var c=rings[k+1];
                var before=rings[Mathf.Max(0,k-1)];var after=rings[Mathf.Min(rings.Length-1,k+2)];
                for(int j=0;j<3;j++)
                {
                    float t=j/3f;
                    float Smooth(float p0,float p1,float p2,float p3) => Mathf.Max(0,.5f*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t));
                    sections.Add(R(Mathf.Lerp(a.Y,c.Y,t),Smooth(before.X,a.X,c.X,after.X),Smooth(before.Z,a.Z,c.Z,after.Z),Mathf.Lerp(a.Forward,c.Forward,t)));
                }
            }
            sections.Add(rings[rings.Length-1]);
            Mesh mesh = LowPolyMeshes.Loft(name, Mathf.Max(32,sides), 2f, false, sections.ToArray());
            Vector3[] vertices = mesh.vertices;
            var uv = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p=vertices[i];
                float front=Mathf.Clamp01(p.z*6),back=Mathf.Clamp01(-p.z*6);
                if(name=="AnatomyUpperArm")
                    p.z+=front*.045f*G(p.x,p.y,0,.07f,.34f,.28f)-back*.035f*G(p.x,p.y,0,.14f,.32f,.30f);
                else if(name=="AnatomyForearm")
                    p.z+=front*.025f*G(p.x,p.y,0,.20f,.25f,.22f)-back*.018f*G(p.x,p.y,0,-.12f,.12f,.32f);
                else if(name=="AnatomyThigh")
                    p.z+=front*(.04f*(G(p.x,p.y,-.17f,.03f,.16f,.33f)+G(p.x,p.y,.17f,.03f,.16f,.33f))+.045f*G(p.x,p.y,0,-.35f,.18f,.09f));
                else if(name=="AnatomyCalf")
                    p.z+=front*.045f*G(p.x,p.y,0,-.08f,.09f,.37f)-back*.045f*(G(p.x,p.y,-.14f,.13f,.16f,.23f)+G(p.x,p.y,.14f,.13f,.16f,.23f));
                vertices[i]=p;
                uv[i] = new Vector2(Mathf.Atan2(vertices[i].z, vertices[i].x) / (2 * Mathf.PI) + .5f, vertices[i].y + .5f);
            }
            mesh.vertices=vertices;
            mesh.uv = uv;
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static float G(float x, float y, float cx, float cy, float rx, float ry)
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            return Mathf.Exp(-2 * (dx * dx + dy * dy));
        }

        // Skull, temple, cheek, jaw and chin profiles; interpolation avoids lathe ridges.
        private static readonly LowPolyMeshes.Ring[] Skull = {
            R(-.5f, 0, 0, .02f), R(-.46f, .18f, .24f, .035f), R(-.38f, .29f, .32f, .01f),
            R(-.25f, .38f, .37f, -.005f), R(-.10f, .445f, .415f, -.025f),
            R(.08f, .445f, .425f, -.035f), R(.26f, .43f, .44f, -.035f),
            R(.39f, .35f, .375f, -.03f), R(.47f, .19f, .22f, -.02f), R(.5f, 0, 0, -.01f) };

        private static LowPolyMeshes.Ring Profile(float y)
        {
            int k = 0;
            while (k < Skull.Length - 2 && y > Skull[k + 1].Y) k++;
            var a = Skull[k]; var b = Skull[k + 1];
            float t = Mathf.InverseLerp(a.Y, b.Y, y);
            float Smooth(float p0, float p1, float p2, float p3) => Mathf.Max(0,
                .5f * ((2*p1) + (-p0+p2)*t + (2*p0-5*p1+4*p2-p3)*t*t + (-p0+3*p1-3*p2+p3)*t*t*t));
            var before = Skull[Mathf.Max(0, k - 1)]; var after = Skull[Mathf.Min(Skull.Length - 1, k + 2)];
            float jaw = profile != null ? Mathf.Lerp(profile.JawWidth,1,Mathf.SmoothStep(0,1,(y+.38f)/.35f)) : 1;
            return R(y, Smooth(before.X, a.X, b.X, after.X)*jaw, Smooth(before.Z, a.Z, b.Z, after.Z), Mathf.Lerp(a.Forward, b.Forward, t));
        }

        /// <summary>Continuous front surface, also used to seat eyes, lids and brows.</summary>
        public static float FaceZ(float x, float y)
        {
            var ring = Profile(y);
            float z = ring.Forward + ring.Z * Mathf.Sqrt(Mathf.Max(0, 1 - x*x / Mathf.Max(.0001f, ring.X*ring.X)));
            // Integrated bridge, nasal tip, alar wings, philtrum, cheekbones, lips and chin.
            float nose = profile != null ? profile.NoseProjection : 1;
            z += nose * (.065f * G(x,y,0,.025f,.052f,.115f) + .125f * G(x,y,0,-.075f,.065f,.065f));
            z += nose * .035f * (G(x,y,-.063f,-.095f,.038f,.035f) + G(x,y,.063f,-.095f,.038f,.035f));
            z -= .025f * G(x,y,0,-.16f,.027f,.04f);
            z += .025f * (G(x,y,-.29f,-.08f,.12f,.095f) + G(x,y,.29f,-.08f,.12f,.095f));
            z -= .055f * (G(x,y,-EyeCenter,.06f,.105f,.062f) + G(x,y,EyeCenter,.06f,.105f,.062f));
            z += .026f * (G(x,y,-.18f,.145f,.13f,.055f) + G(x,y,.18f,.145f,.13f,.055f));
            z += .016f * G(x,y,0,-.225f,.145f,.022f) + .018f * G(x,y,0,-.267f,.13f,.027f);
            z -= .023f * G(x,y,0,-.244f,.15f,.009f);
            z += .018f * G(x,y,0,-.385f,.16f,.065f);
            return z;
        }

        private static Color FaceColor(float x, float y)
        {
            Color c = Color.white;
            float blush = G(x,y,-.29f,-.09f,.13f,.11f) + G(x,y,.29f,-.09f,.13f,.11f);
            c = Color.Lerp(c, new Color(1.05f,.86f,.83f), blush * .32f);
            float lips = G(x,y,0,-.245f,.15f,.045f);
            c = Color.Lerp(c, new Color(.84f,.45f,.43f), lips * .85f);
            c = Color.Lerp(c, new Color(.27f,.13f,.12f), G(x,y,0,-.245f,.14f,.008f) * .9f);
            float nostrils = G(x,y,-.046f,-.117f,.021f,.012f) + G(x,y,.046f,-.117f,.021f,.012f);
            c = Color.Lerp(c, new Color(.30f,.20f,.18f), Mathf.Clamp01(nostrils) * .8f);
            float brow = G(x,y,-.18f,.155f,.12f,.022f) + G(x,y,.18f,.155f,.12f,.022f);
            float hairs = .8f + .2f * Mathf.Sin(x * 1200 + y * 220);
            return Color.Lerp(c, new Color(.13f,.10f,.08f), Mathf.Clamp01(brow * hairs));
        }

        private static Mesh Head(string name, int sides, int rows)
        {
            var b = new Surface();
            b.Grid(sides, rows, (u,v) => {
                float y = -.5f + v, angle = u * Mathf.PI * 2;
                var ring = Profile(y);
                float x = Mathf.Sin(angle) * ring.X;
                float z = Mathf.Cos(angle) * ring.Z + ring.Forward;
                if (Mathf.Cos(angle) > 0) z = Mathf.Lerp(z, FaceZ(x,y), Mathf.SmoothStep(0,1,Mathf.Cos(angle)));
                return new Vector3(x,y,z);
            }, (u,v) => Mathf.Cos(u * Mathf.PI * 2) > .15f ? FaceColor(Mathf.Sin(u*Mathf.PI*2)*Profile(v-.5f).X,v-.5f) : Color.white,
                true);
            foreach (float side in new[] { -1f, 1f })
            {
                // Auricle with a raised helix and recessed concha, not a sphere stuck to the skull.
                b.Grid(24, 14, (u,v) => {
                    float a = u*2*Mathf.PI, r = v;
                    float y = -.045f + Mathf.Cos(a)*.13f*r;
                    float z = -.015f + Mathf.Sin(a)*.09f*r;
                    float helix = Mathf.Exp(-Mathf.Pow((r-.83f)/.13f,2)) * .026f;
                    float x = side * (.426f + .038f*(1-r*r) + helix);
                    return new Vector3(x,y,z);
                }, (u,v) => Color.Lerp(new Color(.8f,.65f,.61f), Color.white, v), false, new Vector3(side,0,0));
            }
            // Eyelid rims seat around the recessed whites, including the inner tear duct.
            foreach (float side in new[] { -1f, 1f })
            foreach (float upper in new[] { -1f, 1f })
            {
                var path = new List<Vector3>();
                for (int j = 0; j <= (sides / 3); j++)
                {
                    float t = j / (float)(sides/3), x = side*EyeCenter + (t*2-1)*.09f;
                    float y = .06f + upper * Mathf.Sin(t*Mathf.PI)* (upper > 0 ? .036f : .026f);
                    path.Add(new Vector3(x,y,EyeZ(side,t) + .002f));
                }
                b.Tube(path, .008f, 8, Color.white);
            }
            return b.Mesh(name);
        }

        private static float EyeZ(float side, float t) => FaceZ(side*EyeCenter + (t*2-1)*.09f, .06f) + .012f + Mathf.Sin(t*Mathf.PI)*.014f;

        private static Mesh Eyes(string name, int steps)
        {
            var b = new Surface();
            foreach (float side in new[] { -1f, 1f })
            {
                b.Grid(steps, 8, (u,v) => {
                    float x = side*EyeCenter + (u*2-1)*.09f;
                    float y = .06f + Mathf.Sin(u*Mathf.PI) * Mathf.Lerp(-.026f,.036f,v);
                    return new Vector3(x,y,EyeZ(side,u) + Mathf.Sin(u*Mathf.PI)*Mathf.Sin(v*Mathf.PI)*.009f);
                }, (u,v) => new Color(.88f,.89f,.83f), false, Vector3.forward);
                b.Grid(steps, 6, (u,v) => {
                    float a = u*2*Mathf.PI, r = v*.027f;
                    float x=side*EyeCenter+Mathf.Cos(a)*r,y=.063f+Mathf.Sin(a)*r;
                    float across=.5f+Mathf.Cos(a)*r/.18f;
                    float height=Mathf.Sin(across*Mathf.PI);
                    float vertical=Mathf.Clamp01(((y-.06f)/Mathf.Max(.001f,height)+.026f)/.062f);
                    float z=EyeZ(side,across)+height*Mathf.Sin(vertical*Mathf.PI)*.009f+.0015f;
                    return new Vector3(x,y,z);
                }, (u,v) => v < .42f ? new Color(.016f,.021f,.025f) :
                    Color.Lerp(new Color(.19f,.31f,.23f), new Color(.045f,.085f,.075f), v*v), false, Vector3.forward);
            }
            return b.Mesh(name);
        }

        private static Mesh Scalp()
        {
            var b = new Surface();
            b.Grid(64,32,(u,v) => {
                float a = u*2*Mathf.PI;
                float hairline = Mathf.Lerp(-.22f,.23f, Mathf.Pow(Mathf.Max(0,Mathf.Cos(a)),3));
                hairline += .012f*Mathf.Sin(a*13);
                float y = Mathf.Lerp(hairline,.5f,v);
                var ring = Profile(y);
                return new Vector3(Mathf.Sin(a)*(ring.X+.018f)*(v==1 ? 0:1),y+.012f,
                    Mathf.Cos(a)*(ring.Z+.025f)*(v==1 ? 0:1)+ring.Forward);
            }, (u,v) => Color.white, true);
            return b.Mesh("AnatomyScalp");
        }

        private static Mesh Hand(string name, bool left, bool grip, int sides)
        {
            var volume = new AnatomyVolume();
            float mirror = left ? -1 : 1;
            Vector3 offset=grip ? new Vector3(mirror*.18f,-.28f,.33f) : Vector3.zero;
            volume.Ellipsoid(new Vector3(0,.08f,0)+offset,new Vector3(.30f,.35f,.40f));
            // Extend the wrist inside the forearm, overlapping its taper rather than leaving
            // a sharp capped edge at the wrist. The visible palm and all fingers are one surface.
            volume.Ellipsoid(new Vector3(0,.55f,-.035f),new Vector3(.45f,.65f,.35f));
            volume.Ellipsoid(new Vector3(-mirror*.09f,.02f,.22f)+offset,new Vector3(.18f,.23f,.22f));
            float[] lengths = { .48f,.55f,.51f,.39f };
            var nails = new List<Vector3>();
            var nailNormals = new List<Vector3>();
            var nailTangents = new List<Vector3>();
            for (int finger=0;finger<4;finger++)
            {
                float z=.285f-finger*.19f,length=lengths[finger];
                var path=new List<Vector3>();
                const int segments=6;
                for(int j=0;j<=segments;j++)
                {
                    float t=j/(float)segments,bend=(grip?2.65f:.32f)*t;
                    float x=-mirror*length*(1-Mathf.Cos(bend))/(grip?2.65f:.32f);
                    float y=-(grip?.29f:.18f)-length*Mathf.Sin(bend)/(grip?2.65f:.32f);
                    path.Add(new Vector3(x,y,z+(grip?0:(z-.04f)*t*.13f))+offset);
                }
                float radius=finger==3?.068f:.080f;
                for(int j=0;j<segments;j++)
                    volume.Capsule(path[j],path[j+1],radius*Mathf.Lerp(1,.77f,j/(float)segments),radius*Mathf.Lerp(1,.77f,(j+1)/(float)segments));
                Vector3 tangent=(path[segments]-path[segments-2]).normalized;
                Vector3 outward=new Vector3(mirror,0,0);
                outward=(outward-tangent*Vector3.Dot(outward,tangent)).normalized;
                nails.Add(path[segments-1]);nailNormals.Add(outward);nailTangents.Add(tangent);
            }
            var thumb=new List<Vector3>();
            for(int j=0;j<=6;j++)
            {
                float t=j/6f;
                thumb.Add(new Vector3(-mirror*(.08f+(grip?.33f:.16f)*t),
                    .12f-(grip?.43f:.30f)*t,.26f+(grip?.15f:.31f)*Mathf.Sin(t*1.4f))+offset);
            }
            for(int j=0;j<6;j++) volume.Capsule(thumb[j],thumb[j+1],Mathf.Lerp(.105f,.078f,j/6f),Mathf.Lerp(.105f,.078f,(j+1)/6f));
            Color Pigment(Vector3 p)
            {
                for(int i=0;i<nails.Count;i++)
                {
                    Vector3 delta=p-nails[i];
                    float along=Vector3.Dot(delta,nailTangents[i]);
                    Vector3 across=Vector3.Cross(nailTangents[i],nailNormals[i]);
                    float sideways=Vector3.Dot(delta,across);
                    if(Vector3.Dot(delta,nailNormals[i])>.043f && along*along/(.055f*.055f)+sideways*sideways/(.043f*.043f)<1)
                        return new Color(1.05f,.90f,.86f);
                }
                return Color.white;
            }
            return volume.Bake(name,new Vector3(-.75f,-.95f,-.52f),new Vector3(.75f,1.25f,1.1f),sides>6?.035f:.09f,Pigment);
        }

        private static Mesh Foot(string name,float spacing)
        {
            var volume=new AnatomyVolume();
            volume.Ellipsoid(new Vector3(0,.05f,-.18f),new Vector3(.36f,.44f,.30f));
            volume.Ellipsoid(new Vector3(0,-.16f,.05f),new Vector3(.42f,.27f,.38f));
            for(int i=0;i<5;i++)
                volume.Ellipsoid(new Vector3(-.29f+i*.14f,-.19f,.36f-i*.025f),new Vector3(i==0?.105f:.075f,.15f,.15f-i*.014f));
            return volume.Bake(name,new Vector3(-.55f,-.5f,-.55f),new Vector3(.55f,.55f,.6f),spacing,p=>Color.white);
        }

        private sealed class Surface
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<int> triangles = new List<int>();

            public void Grid(int columns, int rows, System.Func<float,float,Vector3> point,
                System.Func<float,float,Color> color, bool radial, Vector3 outward = default)
            {
                int first=vertices.Count;
                for(int j=0;j<=rows;j++) for(int i=0;i<=columns;i++)
                {
                    float u=i/(float)columns,v=j/(float)rows;
                    vertices.Add(point(u,v)); uv.Add(new Vector2(u,v)); colors.Add(color(u,v));
                }
                for(int j=0;j<rows;j++) for(int i=0;i<columns;i++)
                {
                    int a=first+j*(columns+1)+i, b=a+1, c=a+columns+1, d=c+1;
                    Vector3 n = radial ? new Vector3(vertices[a].x,0,vertices[a].z) : outward;
                    Tri(a,b,c,n); Tri(b,d,c,n);
                }
            }

            private void Tri(int a,int b,int c,Vector3 outward)
            {
                Vector3 n=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);
                if(n.sqrMagnitude<1e-14f) return;
                if(Vector3.Dot(n,outward)<0) {int t=b;b=c;c=t;}
                triangles.Add(a);triangles.Add(b);triangles.Add(c);
            }

            public void Ellipsoid(Vector3 center,Vector3 radii,Color color,int sides,int rows,Quaternion rotation = default)
            {
                if(rotation==default) rotation=Quaternion.identity;
                int start=vertices.Count;
                Grid(sides,rows,(u,v) => {
                    float a=u*2*Mathf.PI,p=v*Mathf.PI;
                    return center+rotation*Vector3.Scale(radii,new Vector3(Mathf.Sin(p)*Mathf.Cos(a),Mathf.Cos(p),Mathf.Sin(p)*Mathf.Sin(a)));
                },(u,v)=>color,false);
                // Orient this surface around its own centre, not the body's axis.
                for(int i=triangles.Count-1;i>=0 && triangles[i]>=start;i-=3)
                {
                    int ia=triangles[i-2],ib=triangles[i-1],ic=triangles[i];
                    if(Vector3.Dot(Vector3.Cross(vertices[ib]-vertices[ia],vertices[ic]-vertices[ia]),vertices[ia]-center)<0)
                    {triangles[i-1]=ic;triangles[i]=ib;}
                }
            }

            public void Tube(List<Vector3> path,float radius,int sides,Color color,bool knuckles=false)
            {
                int start=vertices.Count;
                Grid(sides,path.Count-1,(u,v) => {
                    int k=Mathf.RoundToInt(v*(path.Count-1));
                    Vector3 tangent=(path[Mathf.Min(k+1,path.Count-1)]-path[Mathf.Max(0,k-1)]).normalized;
                    Vector3 axis=Vector3.Cross(tangent, Mathf.Abs(tangent.z)<.9f ? Vector3.forward:Vector3.up).normalized;
                    Vector3 other=Vector3.Cross(tangent,axis);
                    float a=u*2*Mathf.PI;
                    float r=radius*Mathf.Lerp(1,.76f,v);
                    if(knuckles) r*=1+.12f*Mathf.Pow(Mathf.Cos(v*3*Mathf.PI),8);
                    return path[k]+(axis*Mathf.Cos(a)+other*Mathf.Sin(a))*r;
                },(u,v)=>color,false);
                int triStart=triangles.Count;
                // Correct winding along the local curve, including fingers curling toward the palm.
                for(int i=0;i<triStart;i+=3)
                {
                    if(triangles[i]<start) continue;
                    int index=triangles[i]; int row=(index-start)/(sides+1);
                    Vector3 away=vertices[index]-path[Mathf.Min(row,path.Count-1)];
                    int a=triangles[i],bb=triangles[i+1],c=triangles[i+2];
                    if(Vector3.Dot(Vector3.Cross(vertices[bb]-vertices[a],vertices[c]-vertices[a]),away)<0)
                    {triangles[i+1]=c;triangles[i+2]=bb;}
                }
                Ellipsoid(path[0],Vector3.one*radius,color,sides,6);
                Ellipsoid(path[path.Count-1],Vector3.one*radius*.76f,color,sides,6);
            }

            public Mesh Mesh(string name)
            {
                var mesh=new Mesh {name=name};
                mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.SetColors(colors);
                mesh.RecalculateNormals();
                // Weld only normals at duplicated UV seams; retain the UVs and disconnected surfaces.
                var sums=new Dictionary<Vector3,Vector3>(); var normals=mesh.normals;
                for(int i=0;i<vertices.Count;i++)
                {
                    Vector3 key=Quantize(vertices[i]);
                    sums.TryGetValue(key,out Vector3 sum);sums[key]=sum+normals[i];
                }
                for(int i=0;i<vertices.Count;i++) normals[i]=sums[Quantize(vertices[i])].normalized;
                mesh.normals=normals;mesh.RecalculateTangents();mesh.RecalculateBounds();
                return mesh;
            }

            private static Vector3 Quantize(Vector3 p) => new Vector3(Mathf.Round(p.x*100000),Mathf.Round(p.y*100000),Mathf.Round(p.z*100000));
        }
    }
}
