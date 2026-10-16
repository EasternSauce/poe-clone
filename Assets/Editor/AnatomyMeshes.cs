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
                Save(Simplified(Head("AnatomyHead" + suffix, detailed ? 160 : 40, detailed ? 160 : 32)));
                Save(Eyes("AnatomyEyes" + suffix, detailed ? 32 : 12));
                Save(Simplified(Foot("AnatomyFoot" + suffix, detailed ? .035f : .075f)));
                foreach (bool left in new[] { true, false })
                foreach (bool grip in new[] { false, true })
                    Save(Simplified(Hand("AnatomyHand" + (left ? "L" : "R") + (grip ? "Grip" : "") + suffix,
                        left, grip, detailed ? 12 : 6)));
            }
            Save(Simplified(Scalp()));
            Save(AnatomyLimbMeshes.Arm(-1));
            Save(AnatomyLimbMeshes.Arm(1));
            Save(AnatomyLimbMeshes.Leg());
            foreach(string side in new[] {"L","R"})
                foreach(string suffix in new[] {"","Low","Grip","GripLow"})
                    Save(AnatomyWristMeshes.Join("AnatomyArmHand"+side+suffix,
                        meshes["AnatomyConnectedArm"+side],meshes["AnatomyHand"+side+suffix]));
            Save(LowPolyMeshes.Loft("AnatomyNeck",32,2,false,
                R(.5f,0,0),R(.5f,.38f,.40f),R(.15f,.42f,.43f),R(-.25f,.48f,.48f),R(-.5f,.5f,.5f),R(-.5f,0,0)));
            Save(LowPolyMeshes.Loft("AnatomyYoke",40,2,false,
                R(.5f,0,0),R(.5f,.20f,.27f),R(.2f,.29f,.34f),R(-.5f,.5f,.5f),R(-.5f,0,0)));
            Save(LowPolyMeshes.Shell("AnatomyMantle",48,.035f,0,360,false,
                R(.5f,.09f,.17f,.06f),R(.32f,.19f,.27f,.05f),R(-.05f,.38f,.44f,.015f),R(-.5f,.5f,.5f)));
            return meshes;
        }

        // The dense bakes spend most vertices on flat or gently curved skin. Collapsing those keeps
        // the surface within ~0.1% of its size and roughly quarters the build download for them.
        private const float SimplifyError = .004f;

        private static Mesh Simplified(Mesh mesh)
        {
            Mesh simplified = AnatomySimplifier.Simplify(mesh, SimplifyError);
            Object.DestroyImmediate(mesh);
            return simplified;
        }

        private static LowPolyMeshes.Ring R(float y, float x, float z, float forward = 0) =>
            new LowPolyMeshes.Ring(y, x, z, forward);

        private static float G(float x, float y, float cx, float cy, float rx, float ry)
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            return Mathf.Exp(-2 * (dx * dx + dy * dy));
        }

        private static float Between(float from,float to,float value) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(from,to,value));

        // Skull, temple, cheekbone, jaw and chin profiles of the front half; interpolation avoids lathe
        // ridges. The cheeks and jaw taper well inside the temples, as a broad lower face reads as fat.
        private static readonly LowPolyMeshes.Ring[] Skull = {
            R(-.5f, 0, 0, .10f), R(-.465f, .135f, .22f, .13f), R(-.40f, .225f, .30f, .08f),
            R(-.30f, .31f, .35f, .035f), R(-.17f, .36f, .385f, .005f), R(-.03f, .395f, .41f, -.015f),
            R(.12f, .405f, .43f, -.03f), R(.27f, .395f, .44f, -.035f),
            R(.39f, .33f, .375f, -.03f), R(.47f, .19f, .22f, -.02f), R(.5f, 0, 0, -.01f) };
        // Back depth per Skull ring: a full occiput that tucks under into the neck instead of a jowl.
        private static readonly float[] BackZ = { 0, .14f, .26f, .33f, .38f, .41f, .43f, .43f, .38f, .22f, 0 };

        private static float Spline(float y, System.Func<int,float> value)
        {
            int k = 0;
            while (k < Skull.Length - 2 && y > Skull[k + 1].Y) k++;
            float t = Mathf.InverseLerp(Skull[k].Y, Skull[k + 1].Y, y);
            float p0 = value(Mathf.Max(0, k - 1)), p1 = value(k), p2 = value(k + 1), p3 = value(Mathf.Min(Skull.Length - 1, k + 2));
            return Mathf.Max(0, .5f * ((2*p1) + (-p0+p2)*t + (2*p0-5*p1+4*p2-p3)*t*t + (-p0+3*p1-3*p2+p3)*t*t*t));
        }

        private static LowPolyMeshes.Ring Profile(float y)
        {
            int k = 0;
            while (k < Skull.Length - 2 && y > Skull[k + 1].Y) k++;
            float t = Mathf.InverseLerp(Skull[k].Y, Skull[k + 1].Y, y);
            float jaw = profile != null ? Mathf.Lerp(profile.JawWidth,1,Mathf.SmoothStep(0,1,(y+.38f)/.35f)) : 1;
            return R(y, Spline(y, i => Skull[i].X)*jaw, Spline(y, i => Skull[i].Z), Mathf.Lerp(Skull[k].Forward, Skull[k + 1].Forward, t));
        }

        /// <summary>Bare skull without facial features; the back half has its own depth and narrows into the neck.</summary>
        private static Vector3 SkullPoint(float angle, float y)
        {
            var ring = Profile(y);
            float back = Mathf.Max(0, -Mathf.Cos(angle));
            float x = Mathf.Sin(angle) * ring.X * (1 - .22f * back * Between(-.1f, -.4f, y));
            float depth = Mathf.Cos(angle) > 0 ? ring.Z : Spline(y, i => BackZ[i]);
            return new Vector3(x, y, Mathf.Cos(angle) * depth + ring.Forward);
        }

        /// <summary>Continuous front surface, also used to seat eyes, lids and brows.</summary>
        public static float FaceZ(float x, float y)
        {
            var ring = Profile(y);
            float z = ring.Forward + ring.Z * Mathf.Sqrt(Mathf.Max(0, 1 - x*x / Mathf.Max(.0001f, ring.X*ring.X)));
            // Integrated bridge, nasal tip, alar wings, philtrum, cheekbones, lips and chin.
            float nose = profile != null ? profile.NoseProjection : 1;
            z += nose * (.042f * G(x,y,0,.015f,.047f,.14f) + .078f * G(x,y,0,-.085f,.075f,.068f)
                + .012f * G(x,y,0,-.135f,.026f,.028f));
            z += nose * .026f * (G(x,y,-.063f,-.11f,.038f,.038f) + G(x,y,.063f,-.11f,.038f,.038f));
            z -= .012f * G(x,y,0,-.165f,.030f,.04f);
            z -= .010f*(G(x,y,-.081f,-.11f,.013f,.031f)+G(x,y,.081f,-.11f,.013f,.031f));
            z += .006f*(G(x,y,-.021f,-.185f,.012f,.038f)+G(x,y,.021f,-.185f,.012f,.038f));
            // Cheekbones with only a faint dip beneath; a deep hollow and a heavy brow over sunken
            // sockets read as gaunt and menacing, especially seen from above.
            z += .016f * (G(x,y,-.27f,-.01f,.11f,.05f) + G(x,y,.27f,-.01f,.11f,.05f));
            z -= .004f * (G(x,y,-.25f,-.19f,.08f,.07f) + G(x,y,.25f,-.19f,.08f,.07f));
            z -= .017f * (G(x,y,-EyeCenter,.055f,.095f,.045f) + G(x,y,EyeCenter,.055f,.095f,.045f));
            z += .008f * (G(x,y,-EyeCenter,.14f,.13f,.05f) + G(x,y,EyeCenter,.14f,.13f,.05f));
            float seam=MouthSeam(x);
            z += .014f * G(x,y,0,seam+.011f,.135f,.014f) + .018f * G(x,y,0,seam-.012f,.13f,.018f);
            z -= .010f * G(x,y,0,seam,.145f,.004f);
            z += .018f * G(x,y,0,-.385f,.16f,.065f);
            return z;
        }

        private static Color FaceColor(float x, float y)
        {
            Color c = Color.white;
            float blush = G(x,y,-.27f,-.03f,.11f,.07f) + G(x,y,.27f,-.03f,.11f,.07f);
            c = Color.Lerp(c, new Color(1.05f,.86f,.83f), blush * .14f);
            float lips = G(x,y,0,-.245f,.15f,.045f);
            c = Color.Lerp(c, new Color(.96f,.80f,.76f), lips * .25f);
            float nostrils = G(x,y,-.046f,-.117f,.021f,.012f) + G(x,y,.046f,-.117f,.021f,.012f);
            c = Color.Lerp(c, new Color(.30f,.20f,.18f), Mathf.Clamp01(nostrils) * .8f);
            // Painted occlusion: without it the sockets, nose base and lip read flat and doll-like.
            float socket = G(x,y,-EyeCenter,.06f,.13f,.07f) + G(x,y,EyeCenter,.06f,.13f,.07f);
            float shade = socket * .07f + G(x,y,0,-.135f,.07f,.022f) * .22f + G(x,y,0,-.29f,.11f,.022f) * .16f
                + (G(x,y,-.11f,-.17f,.025f,.06f) + G(x,y,.11f,-.17f,.025f,.06f)) * .10f;
            c *= 1 - Mathf.Clamp01(shade);
            c.a = 1;
            float browY = BrowArch(Mathf.Clamp01((Mathf.Abs(x) - .085f) / .165f)) + .004f;
            float brow = G(x,y,-.168f,browY,.085f,.018f) + G(x,y,.168f,browY,.085f,.018f);
            float hairs = .8f + .2f * Mathf.Sin(x * 1200 + y * 220);
            return Color.Lerp(c, new Color(.16f,.11f,.08f), Mathf.Clamp01(brow * hairs * .8f));
        }

        private static float HeadAngle(float u) => u*2*Mathf.PI-.65f*Mathf.Sin(u*2*Mathf.PI);
        // Corners lift a little: a level or downturned seam reads as a sneer under a shadowed brow.
        private static float MouthSeam(float x) => -.24f+.008f*Mathf.Pow(x/.145f,2);

        private static Mesh Head(string name, int sides, int rows)
        {
            var b = new Surface();
            b.Grid(sides, rows, (u,v) => {
                float y = -.5f + v, angle = HeadAngle(u);
                Vector3 p = SkullPoint(angle, y);
                if (Mathf.Cos(angle) > 0) p.z = Mathf.Lerp(p.z, FaceZ(p.x,y), Mathf.SmoothStep(0,1,Mathf.Cos(angle)));
                return p;
            }, (u,v) => Mathf.Cos(HeadAngle(u)) > .15f ? FaceColor(Mathf.Sin(HeadAngle(u))*Profile(v-.5f).X,v-.5f) : Color.white,
                true);
            float earBase = Profile(-.03f).X - .055f;
            foreach (float side in new[] { -1f, 1f })
            {
                // A folded auricle with an irregular outline, not an ellipsoid or open hole.
                var outline=new[] {
                    new Vector2(.085f,-.005f),new Vector2(.073f,.040f),new Vector2(.035f,.065f),
                    new Vector2(-.018f,.063f),new Vector2(-.052f,.038f),new Vector2(-.080f,.036f),
                    new Vector2(-.10f,.048f),new Vector2(-.145f,.028f),new Vector2(-.157f,0),
                    new Vector2(-.142f,-.030f),new Vector2(-.095f,-.053f),
                    new Vector2(-.027f,-.072f),new Vector2(.045f,-.058f) };
                Vector2 Boundary(float u)
                {
                    float f=u*outline.Length;int k=Mathf.FloorToInt(f)%outline.Length;float t=f-Mathf.Floor(f);
                    Vector2 p0=outline[(k+outline.Length-1)%outline.Length],p1=outline[k],
                        p2=outline[(k+1)%outline.Length],p3=outline[(k+2)%outline.Length];
                    return .5f*(2*p1+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t);
                }
                float Fold(Vector2 p,Vector2 from,Vector2 to)
                {
                    Vector2 axis=to-from;
                    float t=Mathf.Clamp01(Vector2.Dot(p-from,axis)/axis.sqrMagnitude);
                    return .018f*Mathf.Exp(-(p-from-axis*t).sqrMagnitude/(.009f*.009f));
                }
                foreach(bool front in new[] {true,false})
                    b.Grid(80,28,(u,r)=> {
                        Vector2 p=Vector2.Lerp(new Vector2(-.043f,-.002f),Boundary(u),r);
                        float helix=.075f*Mathf.Exp(-Mathf.Pow((r-.82f)/.13f,2))*Mathf.Clamp01((1-r)/.1f);
                        float concha=.005f*G(p.x,p.y,-.045f,.015f,.05f,.035f);
                        float fold=Mathf.Max(Fold(p,new Vector2(-.10f,-.018f),new Vector2(-.027f,-.033f)),
                            Mathf.Max(Fold(p,new Vector2(-.027f,-.033f),new Vector2(.044f,-.015f)),
                            Fold(p,new Vector2(-.027f,-.033f),new Vector2(.017f,.021f))));
                        float x=front?earBase+.070f*(1-r*r)+helix-concha+fold:earBase-.010f*(1-r*r);
                        return new Vector3(side*x,p.x,p.y);
                    },(u,r)=>Color.Lerp(new Color(.96f,.87f,.84f),Color.white,r),false,new Vector3(front?side:-side,0,0));
                // A continuous root joins the anterior rim to the temple, closing the frontal gap.
                b.Grid(12,24,(u,v)=> {
                    float y=Mathf.Lerp(-.12f,.055f,v);
                    float root=Profile(y).X*.97f,t=u*Mathf.Sin(v*Mathf.PI);
                    float x=Mathf.Lerp(root,earBase+.073f,t);
                    float z=Mathf.Lerp(FaceZ(root,y)+.0005f,.032f,t)+.003f*Mathf.Sin(v*Mathf.PI)*Mathf.Sin(u*Mathf.PI);
                    return new Vector3(side*x,y,z);
                },(u,v)=>Color.white,false,Vector3.forward);
            }
            // Explicit high resolution feature surfaces keep their edges readable in closeups.
            b.Grid(sides>40?72:24,20,(u,v)=> {
                float x=(u*2-1)*.145f,envelope=Mathf.Sqrt(Mathf.Max(0,1-Mathf.Pow(x/.145f,2)));
                float seam=MouthSeam(x),upper=seam+envelope*(.017f+.003f*Mathf.Cos(x*65)),lower=seam-envelope*.023f;
                float y=v<.55f?Mathf.Lerp(lower,seam,v/.55f):Mathf.Lerp(seam,upper,(v-.55f)/.45f);
                return new Vector3(x,y,FaceZ(x,y)+.0013f);
            },(u,v)=>Mathf.Abs(v-.55f)<.012f?new Color(.31f,.13f,.12f):
                Color.Lerp(new Color(.84f,.51f,.47f),new Color(.66f,.32f,.29f),v),false,Vector3.forward);
            foreach(float side in new[] {-1f,1f})
            {
                b.Grid(sides>40?48:20,6,(u,v)=> {
                    // Raised well above the lid with the inner end above the outer: seen from the game
                    // camera the face's curve lifts the outer end, so level brows read as a scowling V.
                    // The tail also stops before the face turns away.
                    float x=side*(.085f+u*.165f),arch=BrowArch(u)+.008f*Mathf.Sin(u*Mathf.PI);
                    float width=.010f*Mathf.Sin(u*Mathf.PI*.8f+.35f)+.0025f;
                    float y=arch+(v*2-1)*width;
                    return new Vector3(x,y,FaceZ(x,y)+.002f);
                },(u,v)=>new Color(.19f,.115f,.075f)*(.88f+.12f*Mathf.Sin(u*170+v*7)),false,Vector3.forward);
                b.Grid(32,5,(u,v)=> {
                    float angle=u*2*Mathf.PI,x=side*.047f+Mathf.Cos(angle)*.017f*v;
                    float y=-.124f+Mathf.Sin(angle)*.006f*v;
                    return new Vector3(x,y,FaceZ(x,y)+.0015f);
                },(u,v)=>Color.Lerp(new Color(.22f,.12f,.10f),new Color(.55f,.35f,.29f),v),false,Vector3.forward);
            }
            // Smaller, partially hooded openings keep the iris under the upper lid.
            foreach (float side in new[] { -1f, 1f })
            foreach (float upper in new[] { -1f, 1f })
            {
                var path=new List<Vector3>();
                for(int j=0;j<=sides/3;j++)
                {
                    float t=j/(float)(sides/3);
                    Vector3 p=EyePoint(side,t,upper>0?1:0);
                    path.Add(p);
                }
                // A dark lash line along the upper lid gives the eye its outline.
                b.Tube(path,upper>0?.006f:.0045f,8,upper>0?new Color(.32f,.22f,.18f):new Color(.92f,.80f,.76f));
            }
            return b.Mesh(name);
        }

        private const float EyeWidth=.076f, EyeUpper=.032f, EyeLower=.017f, EyeY=.055f;
        // Brow line from the inner (0) to the outer (1) end, falling outward.
        private static float BrowArch(float u) => .142f - .028f * u;

        private static Vector3 EyePoint(float side,float u,float v)
        {
            float x=side*EyeCenter+(u*2-1)*EyeWidth;
            // Outer corners sit slightly low for the same reason; an upward tilt reads as a glare from above.
            float outward=side>0?u:1-u;
            float y=EyeY+.005f*(.5f-outward)+Mathf.Sin(u*Mathf.PI)*Mathf.Lerp(-EyeLower,EyeUpper,v);
            float z=FaceZ(x,y)+.004f+Mathf.Sin(u*Mathf.PI)*Mathf.Sin(v*Mathf.PI)*.006f;
            return new Vector3(x,y,z);
        }

        private static Mesh Eyes(string name,int steps)
        {
            var b=new Surface();
            foreach(float side in new[] {-1f,1f})
                b.Grid(steps*2,16,(u,v)=>EyePoint(side,u,v),(u,v)=> {
                    Vector3 p=EyePoint(side,u,v);
                    float dx=p.x-side*EyeCenter,dy=(p.y-EyeY)*1.12f;
                    float radius=Mathf.Sqrt(dx*dx+dy*dy);
                    float iris=1-Between(.0225f,.025f,radius);
                    float pupil=1-Between(.0075f,.009f,radius);
                    Color irisColor=Color.Lerp(new Color(.28f,.16f,.075f),new Color(.065f,.045f,.028f),Between(.017f,.024f,radius));
                    // Off-white sclera, a little shaded towards the corners as under real lids.
                    Color sclera=Color.Lerp(new Color(.80f,.77f,.72f),new Color(.62f,.55f,.50f),Between(.04f,.075f,Mathf.Abs(dx)));
                    Color color=Color.Lerp(sclera,irisColor,iris);
                    return Color.Lerp(color,new Color(.018f,.014f,.012f),pupil);
                },false,Vector3.forward);
            return b.Mesh(name);
        }

        // Hairline height by degrees from the front: forehead, temples, sideburn, then a tight arch
        // hugging the ear (which spans roughly 81-100 degrees, top at y .085) that drops straight
        // back down behind it into a low nape. A loose arch left a bare patch and a helmet edge.
        private static readonly Vector2[] HairlineKeys = {
            new Vector2(0,.29f), new Vector2(28,.27f), new Vector2(50,.22f), new Vector2(62,.13f),
            new Vector2(68,0), new Vector2(72,-.05f), new Vector2(76,-.05f), new Vector2(80,.07f),
            new Vector2(85,.12f), new Vector2(93,.125f), new Vector2(100,.10f), new Vector2(104,.06f),
            new Vector2(109,-.03f), new Vector2(116,-.11f), new Vector2(135,-.22f), new Vector2(158,-.29f),
            new Vector2(180,-.31f) };

        private static float Hairline(float angle)
        {
            float degrees=Mathf.Abs(Mathf.DeltaAngle(0,angle*Mathf.Rad2Deg));
            int k=0;
            while(k<HairlineKeys.Length-2&&degrees>HairlineKeys[k+1].x) k++;
            float t=Mathf.SmoothStep(0,1,Mathf.InverseLerp(HairlineKeys[k].x,HairlineKeys[k+1].x,degrees));
            return Mathf.Lerp(HairlineKeys[k].y,HairlineKeys[k+1].y,t)+.004f*Mathf.Sin(angle*17)+.002f*Mathf.Sin(angle*37);
        }

        private static Vector3 HairPoint(float angle,float v,float relief=0)
        {
            // Sweep toward the side part instead of radiating identical strands from the crown. Only the
            // top sweeps: on the short sides it dragged the sideburn back over the ear. Weighting by v
            // rather than height keeps the surface from folding where the hairline steps at the sideburn.
            float hairline=Hairline(angle),y=Mathf.Lerp(hairline,.5f,v);
            float a=angle+.30f*Mathf.Sin(v*Mathf.PI)*Mathf.Sin(angle-.45f)*Between(.2f,.5f,v);
            // Thins to the scalp at the hairline instead of ending in a ledge; short sides, fuller top.
            float grow=Between(0,.14f,y-hairline),top=Mathf.Lerp(.5f,1,Between(.12f,.38f,y));
            float flow=angle*73+Mathf.Sin(angle*9)*.65f+v*2;
            float ridge=(.006f*Mathf.Sin(angle*23+v*4+Mathf.Sin(angle*5))+.0025f*Mathf.Sin(flow)+.001f*Mathf.Sin(flow*2.7f))*Mathf.Sin(v*Mathf.PI);
            float thick=.006f+(.022f*top+ridge)*grow+relief;
            // No sideways crown offset: shifting the whole cap toward +x sank the thin left side and the
            // crown into the skull, which showed through as a slit above the left ear and streaks on top.
            float end=v==1?0:1;
            float lift=.022f*Mathf.Sin(v*Mathf.PI)*Mathf.Max(0,Mathf.Cos(a));
            Vector3 skull=SkullPoint(a,y);
            float forward=Profile(y).Forward;
            return new Vector3((skull.x+Mathf.Sin(a)*thick)*end,y+thick*Between(.2f,.5f,y)+lift,
                (skull.z-forward+Mathf.Cos(a)*thick)*end+forward);
        }

        private static Mesh Scalp()
        {
            var b=new Surface();
            b.Grid(192,48,(u,v)=>HairPoint(u*2*Mathf.PI,v),
                (u,v)=>Color.white*(.94f+.045f*Mathf.Sin(u*458+v*2)+.025f*Mathf.Sin(u*91+v*5)),true);
            // Loose tips across the front soften the fringe; the short sides and back stay smooth.
            for(int index=0;index<40;index++)
            {
                float angle=Mathf.Lerp(-1.35f,1.35f,index/39f)+.03f*Mathf.Sin(index*3.7f);
                float length=.30f+.10f*(.5f+.5f*Mathf.Sin(index*2.17f));
                var path=new List<Vector3>();
                for(int j=0;j<=10;j++)
                {
                    float t=j/10f;
                    Vector3 point=HairPoint(angle,Mathf.Lerp(length,.03f,t),.003f);
                    point.y-=.006f*Mathf.Pow(t,5);
                    path.Add(point);
                }
                float tone=.94f+.055f*Mathf.Sin(index*2.17f);
                b.Lock(path,.010f+.003f*(.5f+.5f*Mathf.Sin(index)),.003f,new Color(tone,tone,tone));
            }
            return b.Mesh("AnatomyScalp");
        }

        private static Mesh Hand(string name, bool left, bool grip, int sides)
        {
            var volume = new AnatomyVolume(.020f);
            float mirror = left ? -1 : 1;
            Vector3 offset=grip ? new Vector3(mirror*.18f,-.28f,.33f) : Vector3.zero;
            // The palm is a thin metacarpal plate: broad at the knuckles, tapered at the wrist.
            volume.Palm(new Vector3(0,.035f,0)+offset,new Vector3(.105f,.245f,.30f),.045f);
            volume.Ellipsoid(new Vector3(0,.3125f,0),new Vector3(.1925926f,.19f,.165625f));
            volume.Ellipsoid(new Vector3(-mirror*.065f,.01f,.19f)+offset,new Vector3(.065f,.16f,.105f));
            float[] fingerZ={.2225f,.0775f,-.0675f,-.2125f};
            for(int i=0;i<4;i++)
            {
                volume.Capsule(new Vector3(mirror*.09f,.16f,fingerZ[i]*.64f)+offset,
                    new Vector3(mirror*.095f,-.17f,fingerZ[i])+offset,.022f,.030f);
                volume.Ellipsoid(new Vector3(mirror*.015f,-.17f,fingerZ[i])+offset,new Vector3(.105f,.065f,.065f));
            }
            var creases=new[] {
                new[] {new Vector2(-.10f,-.25f),new Vector2(-.07f,-.08f),new Vector2(-.095f,.08f),new Vector2(-.13f,.22f)},
                new[] {new Vector2(.015f,-.24f),new Vector2(.045f,-.08f),new Vector2(.005f,.075f)},
                new[] {new Vector2(.16f,.16f),new Vector2(.075f,.10f),new Vector2(-.035f,.085f),new Vector2(-.14f,.12f)} };
            float[] lengths = { .48f,.55f,.51f,.39f };
            var nails = new List<Vector3>();
            var nailNormals = new List<Vector3>();
            var nailTangents = new List<Vector3>();
            for (int finger=0;finger<4;finger++)
            {
                float z=fingerZ[finger],length=lengths[finger];
                var path=new List<Vector3>();
                const int segments=6;
                if(grip)
                    for(int j=0;j<=segments;j++)
                    {
                        float t=j/(float)segments,bend=2.65f*t;
                        path.Add(new Vector3(-mirror*length*(1-Mathf.Cos(bend))/2.65f,
                            -.29f-length*Mathf.Sin(bend)/2.65f,z)+offset);
                    }
                else
                {
                    // MCP, PIP and DIP curl progressively; digits have slightly different rest arcs.
                    Vector3 point=new Vector3(0,-.18f,z);
                    path.Add(point);
                    float[] angles={18f+finger*2,58f+finger*3,78f+finger*3};
                    float[] proportions={.45f,.33f,.22f};
                    for(int joint=0;joint<3;joint++)
                        for(int step=0;step<2;step++)
                        {
                            float angle=angles[joint]*Mathf.Deg2Rad;
                            point+=new Vector3(-mirror*Mathf.Sin(angle),-Mathf.Cos(angle),0)*length*proportions[joint]*.5f;
                            path.Add(point);
                        }
                }
                float radius=finger==3?.056f:.066f;
                volume.Tube(path,t=>radius*Mathf.Lerp(1,.78f,t));
                Vector3 tangent=(path[segments]-path[segments-2]).normalized;
                Vector3 outward=new Vector3(mirror,0,0);
                outward=(outward-tangent*Vector3.Dot(outward,tangent)).normalized;
                nails.Add(path[segments-1]);nailNormals.Add(outward);nailTangents.Add(tangent);
            }
            var thumb=new List<Vector3>();
            for(int j=0;j<=16;j++)
            {
                float t=j/16f;
                if(grip)
                    thumb.Add(new Vector3(-mirror*(.08f+.33f*t),.12f-.43f*t,.26f+.15f*Mathf.Sin(t*1.4f))+offset);
                else
                {
                    Vector3 a=new Vector3(-mirror*.045f,.09f,.18f),c1=new Vector3(-mirror*.09f,-.04f,.31f),
                        c2=new Vector3(-mirror*.13f,-.24f,.29f),end=new Vector3(-mirror*.10f,-.34f,.2225f);
                    float q=1-t;
                    thumb.Add(q*q*q*a+3*q*q*t*c1+3*q*t*t*c2+t*t*t*end);
                }
            }
            // Broad at the root where it meets the thenar pad, tapering to a rounded tip.
            volume.Tube(thumb,t=>Mathf.Lerp(.068f,.050f,Mathf.SmoothStep(0,1,t)));
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
                Vector3 palm=p-offset;
                if(palm.x*mirror<-.085f)
                    foreach(var crease in creases)
                        for(int i=0;i<crease.Length-1;i++)
                        {
                            Vector2 axis=crease[i+1]-crease[i],delta=new Vector2(palm.y,palm.z)-crease[i];
                            float t=Mathf.Clamp01(Vector2.Dot(delta,axis)/axis.sqrMagnitude);
                            if((delta-axis*t).sqrMagnitude<.009f*.009f)return new Color(.78f,.65f,.58f);
                        }
                return Color.white;
            }
            return GrowFromWrist(volume.Bake(name,new Vector3(-.65f,-.85f,-.40f),new Vector3(.65f,.3125f,.90f),sides>6?.018f:.06f,Pigment));
        }

        // Palm and fingers were small for the forearm. Scale the hand about the wrist, easing in
        // just below the cut so the opening still matches the forearm it is stitched to.
        public const float HandGrowth = 1.2f;
        private const float WristY = .3125f;

        private static Mesh GrowFromWrist(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i];
                float below = WristY - p.y;
                float growth = Mathf.Lerp(1, HandGrowth, Mathf.SmoothStep(0, 1, below / .15f));
                vertices[i] = new Vector3(p.x * growth, WristY - below * growth, p.z * growth);
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            return mesh;
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

            public void Lock(List<Vector3> path,float width,float thickness,Color color)
            {
                int start=vertices.Count;
                const int sides=10;
                Grid(sides,path.Count-1,(u,v)=> {
                    int k=Mathf.RoundToInt(v*(path.Count-1));
                    Vector3 p=path[k];
                    Vector3 tangent=(path[Mathf.Min(k+1,path.Count-1)]-path[Mathf.Max(0,k-1)]).normalized;
                    Vector3 normal=new Vector3(p.x,p.y*.7f,p.z).normalized;
                    Vector3 across=Vector3.Cross(tangent,normal).normalized;
                    normal=Vector3.Cross(across,tangent).normalized;
                    float taper=Mathf.Lerp(1,.06f,Mathf.Pow(v,3));
                    float a=u*2*Mathf.PI;
                    return p+(across*Mathf.Cos(a)*width+normal*Mathf.Sin(a)*thickness)*taper;
                },(u,v)=>color,false);
                OrientTube(start,path,sides);
            }

            public void Tube(List<Vector3> path,float radius,int sides,Color color)
            {
                int start=vertices.Count;
                Grid(sides,path.Count-1,(u,v) => {
                    int k=Mathf.RoundToInt(v*(path.Count-1));
                    Vector3 tangent=(path[Mathf.Min(k+1,path.Count-1)]-path[Mathf.Max(0,k-1)]).normalized;
                    Vector3 axis=Vector3.Cross(tangent, Mathf.Abs(tangent.z)<.9f ? Vector3.forward:Vector3.up).normalized;
                    Vector3 other=Vector3.Cross(tangent,axis);
                    float a=u*2*Mathf.PI;
                    float r=radius*Mathf.Lerp(1,.76f,v);
                    return path[k]+(axis*Mathf.Cos(a)+other*Mathf.Sin(a))*r;
                },(u,v)=>color,false);
                OrientTube(start,path,sides);
                Ellipsoid(path[0],Vector3.one*radius,color,sides,6);
                Ellipsoid(path[path.Count-1],Vector3.one*radius*.76f,color,sides,6);
            }

            private void OrientTube(int start,List<Vector3> path,int sides)
            {
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
