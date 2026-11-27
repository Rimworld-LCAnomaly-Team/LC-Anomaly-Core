# Deterministic, text-free Codex artwork. Run with Windows PowerShell 5.1.
# All geometry is drawn at 2x, then downsampled to straight-alpha RGBA PNGs.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\Cont\Textures\UI\Codex'))
[void][System.IO.Directory]::CreateDirectory($assetDirectory)

if (-not ('LCCodexArtwork' -as [type])) {
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Collections.Generic;

public static class LCCodexArtwork
{
    static readonly Color Gold = C("F2D874"), Red = C("E63232");
    static readonly List<string> Report = new List<string>();
    static Color C(string hex, int a = 255) {
        return Color.FromArgb(a, Convert.ToInt32(hex.Substring(0,2),16),
            Convert.ToInt32(hex.Substring(2,2),16), Convert.ToInt32(hex.Substring(4,2),16));
    }
    sealed class Canvas : IDisposable {
        public Bitmap Bitmap;
        public Graphics G;
        public int W, H;
        public Canvas(int w, int h) {
            W=w; H=h;
            Bitmap=new Bitmap(w*2,h*2,PixelFormat.Format32bppArgb);
            G=Graphics.FromImage(Bitmap);
            Quality(G); G.Clear(Color.Transparent); G.ScaleTransform(2,2);
        }
        public void Dispose() { G.Dispose(); Bitmap.Dispose(); }
    }
    static void Quality(Graphics g) {
        g.InterpolationMode=InterpolationMode.HighQualityBicubic;
        g.SmoothingMode=SmoothingMode.AntiAlias;
        g.PixelOffsetMode=PixelOffsetMode.HighQuality;
        g.CompositingQuality=CompositingQuality.HighQuality;
    }
    static PointF[] P(params float[] xy) {
        PointF[] p=new PointF[xy.Length/2];
        for(int i=0;i<p.Length;i++) p[i]=new PointF(xy[i*2],xy[i*2+1]);
        return p;
    }
    static GraphicsPath Poly(params float[] xy) {
        GraphicsPath p=new GraphicsPath(); p.AddPolygon(P(xy)); return p;
    }
    static GraphicsPath Chamfer(float x,float y,float w,float h,float cut) {
        return Poly(x,y+cut,x+cut,y,x+w,y,x+w,y+h,x,y+h);
    }
    static void Fill(Graphics g,Color c,params float[] xy) {
        using(SolidBrush b=new SolidBrush(c)) g.FillPolygon(b,P(xy));
    }
    static void Rect(Graphics g,Color c,float x,float y,float w,float h) {
        using(SolidBrush b=new SolidBrush(c)) g.FillRectangle(b,x,y,w,h);
    }
    static void Line(Graphics g,Color c,float width,params float[] xy) {
        using(Pen p=new Pen(c,width)) { p.LineJoin=LineJoin.Miter; g.DrawLines(p,P(xy)); }
    }
    static void Stroke(Graphics g,GraphicsPath path,Color c,float width) {
        using(Pen p=new Pen(c,width)) { p.LineJoin=LineJoin.Miter; g.DrawPath(p,path); }
    }
    static void Gradient(Graphics g,GraphicsPath path,RectangleF area,Color a,Color b,float angle) {
        using(LinearGradientBrush brush=new LinearGradientBrush(area,a,b,angle)) g.FillPath(brush,path);
    }
    static void Scan(Graphics g,int w,int h,int alpha) {
        for(int y=3;y<h;y+=4) Rect(g,C("A1ADB8",alpha),0,y,w,0.5f);
    }
    // Opaque dots taper in radius, giving the hard printed halftone of the source art.
    static void Halftone(Graphics g,float x,float y,float w,float h,bool flip,int opacity) {
        GraphicsState state=g.Save();
        g.SetClip(new RectangleF(x,y,w,h),CombineMode.Intersect);
        for(float yy=0;yy<h+6;yy+=5) {
            for(float xx=0;xx<w+6;xx+=5) {
                float u=xx/w, v=yy/h;
                float strength=Math.Max(0,Math.Min(1,1-u*0.95f-v*0.68f));
                float r=2.4f*strength;
                if(r<0.18f) continue;
                float px=flip?x+w-xx:x+xx, py=flip?y+h-yy:y+yy;
                using(SolidBrush b=new SolidBrush(C("EB3232",opacity)))
                    g.FillEllipse(b,px-r,py-r,r*2,r*2);
            }
        }
        g.Restore(state);
    }
    static void Reticle(Graphics g,int w,int h,Color c,float length,float inset,float width) {
        Line(g,c,width,inset,inset+length,inset,inset+12,inset+12,inset,inset+length,inset);
        Line(g,c,width,w-inset-length,inset,w-inset,inset,w-inset,inset+length);
        Line(g,c,width,inset,h-inset-length,inset,h-inset,inset+length,h-inset);
        Line(g,c,width,w-inset-length,h-inset,w-inset,h-inset,w-inset,h-inset-length);
    }
    static void Save(Canvas c,string dir,string name,string check) {
        string file=Path.Combine(dir,name+".png");
        using(Bitmap result=new Bitmap(c.W,c.H,PixelFormat.Format32bppArgb)) {
            using(Graphics g=Graphics.FromImage(result)) {
                Quality(g); g.CompositingMode=CompositingMode.SourceCopy;
                // TileFlipXY avoids transparent sampling seams at full-canvas edges.
                using(ImageAttributes attributes=new ImageAttributes()) {
                    attributes.SetWrapMode(WrapMode.TileFlipXY);
                    g.DrawImage(c.Bitmap,new Rectangle(0,0,c.W,c.H),0,0,c.W*2,c.H*2,GraphicsUnit.Pixel,attributes);
                }
            }
            result.Save(file,ImageFormat.Png);
        }
        // Read the actual encoded PNG; report and enforce alpha rather than assuming it.
        using(Bitmap png=new Bitmap(file)) {
            if(png.Width!=c.W || png.Height!=c.H || png.PixelFormat!=PixelFormat.Format32bppArgb)
                throw new InvalidDataException("Unexpected PNG dimensions or pixel format: "+file);
            BitmapData data=png.LockBits(new Rectangle(0,0,c.W,c.H),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            int min=255,max=0,transparent=0,centerMax=0,edgeMax=0;
            try {
                byte[] row=new byte[c.W*4];
                for(int y=0;y<c.H;y++) {
                    Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),row,0,row.Length);
                    for(int x=0;x<c.W;x++) {
                        int a=row[x*4+3]; min=Math.Min(min,a); max=Math.Max(max,a);
                        if(a==0) transparent++;
                        if(x>=20 && x<c.W-20 && y>=20 && y<c.H-20) centerMax=Math.Max(centerMax,a);
                        if(x<2 || y<2 || x>=c.W-2 || y>=c.H-2) edgeMax=Math.Max(edgeMax,a);
                    }
                }
            } finally { png.UnlockBits(data); }
            string extra="";
            if(check=="interior") {
                if(centerMax!=0) throw new InvalidDataException("Portrait/tile interior is not transparent: "+file);
                extra="; inner [20,20.."+(c.W-21)+","+(c.H-21)+"] alpha=0 PASS";
            }
            if(check=="background") {
                if(min!=0 || transparent<c.W*c.H/5 || edgeMax!=0)
                    throw new InvalidDataException("Icon/divider background is not transparent: "+file);
                extra="; background alpha=0 PASS ("+transparent+" clear pixels; 2px outer margin clear)";
            }
            Report.Add("Cont/Textures/UI/Codex/"+name+".png | "+c.W+"x"+c.H+" | alpha "+min+".."+max+extra);
        }
    }
    static void Window(string dir) {
        using(Canvas c=new Canvas(1120,760)) {
            Graphics g=c.G;
            using(GraphicsPath p=Chamfer(1,1,1118,758,24)) {
                Gradient(g,p,new RectangleF(0,0,1120,760),C("090B0E"),C("101319"),90);
                GraphicsState s=g.Save(); g.SetClip(p);
                for(int x=24;x<1120;x+=32) Line(g,C("4A5A70",10),0.5f,x,0,x,760);
                for(int y=24;y<760;y+=32) Line(g,C("4A5A70",10),0.5f,0,y,1120,y);
                Scan(g,1120,760,12);
                Halftone(g,0,0,370,180,false,85);
                Halftone(g,790,590,330,170,true,65);
                g.Restore(s);
                Stroke(g,p,Gold,2);
            }
            using(GraphicsPath p=Chamfer(7,7,1106,746,23)) Stroke(g,p,C("9A833F",65),0.6f);
            Line(g,C("D93636"),3,2,62,2,25,25,2,107,2);
            Line(g,C("D93636"),3,1015,758,1118,758,1118,705);
            Line(g,Gold,3,1075,2,1118,2,1118,41);
            Line(g,Gold,3,2,708,2,758,49,758);
            Rect(g,Red,40,8,28,3); Rect(g,Red,1053,749,27,3);
            Save(c,dir,"Codex_WindowBg","");
        }
    }
    static void Header(string dir) {
        using(Canvas c=new Canvas(1120,84)) {
            Graphics g=c.G; Rect(g,C("0B0D12"),0,0,1120,84);
            Fill(g,C("EB3232"),0,0,360,0,132,74,0,74);
            Halftone(g,100,0,370,78,false,255);
            // A hard slash cuts across the red field, echoing CreatureInfo_Title.
            Fill(g,C("0B0D12"),0,55,281,0,332,0,0,74);
            Line(g,C("EB3232"),1,0,75,333,0);
            for(int x=958,i=0;x<1100;x+=9,i++)
                Line(g,C("C4AA62",i%4==0?125:55),0.7f,x,60,x,i%4==0?45:53);
            Rect(g,C("EB3232"),0,80,1120,2); Rect(g,Gold,0,83,1120,1);
            Save(c,dir,"Codex_HeaderBanner","");
        }
    }
    static void Dossier(string dir) {
        using(Canvas c=new Canvas(490,118)) {
            Graphics g=c.G;
            using(GraphicsPath p=Chamfer(1,1,488,116,20)) {
                using(SolidBrush b=new SolidBrush(C("0C0E13"))) g.FillPath(b,p);
                GraphicsState s=g.Save(); g.SetClip(p);
                Fill(g,C("A31F27"),0,0,192,0,86,118,0,118);
                Halftone(g,28,0,210,118,false,245);
                Fill(g,C("0C0E13"),0,89,131,0,153,0,26,118,0,118);
                Line(g,C("E63232"),1.5f,33,118,162,0);
                Rect(g,C("E63232"),0,114,490,3);
                g.Restore(s); Stroke(g,p,Gold,1.2f);
            }
            Line(g,C("847440",100),0.6f,157,16,157,97);
            Save(c,dir,"Codex_DossierBanner","");
        }
    }
    static void Portrait(string dir) {
        using(Canvas c=new Canvas(128,128)) {
            Graphics g=c.G;
            using(GraphicsPath p=Chamfer(7,7,114,114,16)) Stroke(g,p,C("14191F"),9);
            Reticle(g,128,128,C("30343A"),27,4,4);
            using(GraphicsPath p=Chamfer(7,7,114,114,16)) Stroke(g,p,C("F5DC78"),1.2f);
            Line(g,C("F5DC78",105),0.7f,12,34,12,115,43,115);
            Line(g,Red,2,60,5,68,5); Line(g,Red,2,60,123,68,123);
            Line(g,Red,2,5,60,5,68); Line(g,Red,2,123,60,123,68);
            Line(g,Red,0.8f,64,9,64,14); Line(g,Red,0.8f,64,114,64,119);
            Line(g,Red,0.8f,9,64,14,64); Line(g,Red,0.8f,114,64,119,64);
            Save(c,dir,"Codex_PortraitFrame","interior");
        }
    }
    static void Panel(string dir) {
        using(Canvas c=new Canvas(512,512)) {
            Graphics g=c.G;
            using(GraphicsPath p=Chamfer(1,1,510,510,25)) {
                using(SolidBrush b=new SolidBrush(C("0A0C10",225))) g.FillPath(b,p);
                GraphicsState s=g.Save(); g.SetClip(p); Scan(g,512,512,4); g.Restore(s);
                Stroke(g,p,C("EBD26E"),2);
            }
            using(GraphicsPath p=Chamfer(5,5,502,502,24)) Stroke(g,p,C("6D6138",65),0.5f);
            Line(g,Red,2,1,42,1,26,26,1,43,1);
            Line(g,Red,2,495,1,511,1,511,17);
            Line(g,Red,2,1,495,1,511,17,511);
            Line(g,Red,2,495,511,511,511,511,495);
            Save(c,dir,"Codex_PanelFrame","");
        }
    }
    static void Section(string dir) {
        using(Canvas c=new Canvas(480,30)) {
            Graphics g=c.G; Fill(g,C("10131A",240),0,10,10,0,480,0,480,30,0,30);
            Fill(g,C("F0D36A"),1,11,12,0,26,0,13,29,1,29);
            Line(g,C("AD954A",130),0.7f,31,4,19,26);
            GraphicsState s=g.Save(); g.SetClip(new RectangleF(320,0,160,29));
            for(int x=316;x<510;x+=17) {
                int a=(int)(75*(1-(x-316)/220.0));
                Fill(g,C("A72C30",Math.Max(10,a)),x,29,x+29,0,x+36,0,x+7,29);
            }
            g.Restore(s); Rect(g,Gold,0,29,480,1);
            Save(c,dir,"Codex_SectionBar","");
        }
    }
    static void Tab(string dir,bool active) {
        using(Canvas c=new Canvas(220,40)) {
            Graphics g=c.G;
            using(GraphicsPath p=Poly(1,13,13,1,207,1,207,5,219,5,219,39,1,39)) {
                using(SolidBrush b=new SolidBrush(active?C("261014",250):C("13171F",235))) g.FillPath(b,p);
                if(active) {
                    GraphicsState s=g.Save(); g.SetClip(p);
                    Rect(g,C("EB3838",24),9,1,201,9);
                    Rect(g,C("EB3838",60),11,1,198,5);
                    Rect(g,C("EB3838"),12,2,195,2.5f);
                    Fill(g,C("FCE47E"),3,14,13,4,13,29,3,38);
                    g.Restore(s);
                }
                Stroke(g,p,active?C("FCE47E"):C("7A6D42"),1);
            }
            Line(g,active?C("E63535",180):C("A1905A",55),0.7f,18,35,211,35);
            Save(c,dir,active?"Codex_TabActive":"Codex_TabNormal","");
        }
    }
    static void Tile(string dir,bool selected) {
        using(Canvas c=new Canvas(128,128)) {
            Graphics g=c.G;
            using(GraphicsPath p=Chamfer(5,5,118,118,14)) Stroke(g,p,C("11151B"),6);
            Reticle(g,128,128,C("252A30"),24,3,3);
            using(GraphicsPath p=Chamfer(5,5,118,118,14)) {
                if(selected) Stroke(g,p,C("FFE678",40),4);
                Stroke(g,p,selected?C("FFE678"):C("8A7A4A"),selected?1.6f:1);
            }
            if(selected) {
                Reticle(g,128,128,C("F03C3C"),30,3,3);
                Line(g,C("FFE678"),1,57,5,71,5);
                Line(g,C("FFE678"),1,57,123,71,123);
            }
            Save(c,dir,selected?"Codex_TileSelected":"Codex_TileFrame","interior");
        }
    }
    static void Locked(string dir) {
        using(Canvas c=new Canvas(480,104)) {
            Graphics g=c.G;
            using(GraphicsPath p=Chamfer(2,2,476,100,12)) {
                using(SolidBrush b=new SolidBrush(C("140C0F",238))) g.FillPath(b,p);
                GraphicsState s=g.Save(); g.SetClip(p);
                for(int x=-110;x<490;x+=24)
                    Fill(g,C("99332D",17),x,104,x+104,0,x+113,0,x+9,104);
                g.Restore(s);
                using(Pen pen=new Pen(C("CD4A3A",225),1.2f)) {
                    pen.DashPattern=new float[] {8,5}; g.DrawPath(pen,p);
                }
            }
            for(int x=29;x<455;x+=52) {
                Line(g,C("D8AC55",220),1.3f,x,2,x+9,2);
                Line(g,C("D8AC55",220),1.3f,x,102,x+9,102);
            }
            Reticle(g,480,104,C("E0B84C"),20,2,2);
            Line(g,Red,2,9,27,9,19,19,9,28,9);
            Save(c,dir,"Codex_LockedCard","");
        }
    }
    static GraphicsPath Shackle() {
        GraphicsPath p=new GraphicsPath();
        p.StartFigure(); p.AddLine(25,43,23,27);
        p.AddBezier(23,27,20,1,67,1,69,25);
        p.AddLine(69,25,67,43); p.AddLine(67,43,57,43); p.AddLine(57,43,59,26);
        p.AddBezier(59,26,59,13,32,12,33,27);
        p.AddLine(33,27,35,43); p.CloseFigure(); return p;
    }
    static void Lock(string dir) {
        using(Canvas c=new Canvas(96,96)) {
            Graphics g=c.G;
            using(GraphicsPath p=Shackle()) {
                using(SolidBrush b=new SolidBrush(C("121216"))) g.FillPath(b,p);
                Stroke(g,p,C("121216"),5);
                Stroke(g,p,C("F5DC78"),1.5f);
            }
            using(GraphicsPath shine=new GraphicsPath()) {
                shine.AddBezier(25,26,23,14,34,7,43,7);
                Stroke(g,shine,C("FFFFFF"),1.6f);
            }
            Line(g,C("FFFFFF"),1.3f,25,28,27,37);
            using(GraphicsPath body=Poly(12,52,24,40,73,40,84,52,78,87,16,87)) {
                using(SolidBrush b=new SolidBrush(C("121216"))) g.FillPath(b,body);
                Stroke(g,body,C("101014"),5);
                Stroke(g,body,C("F5DC78"),1.4f);
            }
            Fill(g,C("F5DC78"),20,51,29,46,66,46,25,53,24,80,21,82);
            Fill(g,C("33302A"),74,46,79,53,74,81,69,81);
            Line(g,C("F5DC78",150),1,27,83,69,83);
            using(SolidBrush b=new SolidBrush(C("E83636",35))) g.FillEllipse(b,35,52,26,28);
            using(SolidBrush b=new SolidBrush(C("E83636",55))) g.FillEllipse(b,38,55,20,21);
            using(SolidBrush b=new SolidBrush(C("E83636"))) g.FillEllipse(b,41,57,14,14);
            Fill(g,C("E83636"),46,67,50,67,53,79,43,79);
            Line(g,C("FF8C73"),1,44,59,49,59);
            Save(c,dir,"Codex_LockIcon","background");
        }
    }
    static void PeBox(string dir) {
        using(Canvas c=new Canvas(64,64)) {
            Graphics g=c.G;
            // A complete perspective cube: three lit planes and a rigid ink contour.
            using(GraphicsPath p=Poly(9,16,39,7,56,21,52,51,24,57,10,43)) {
                using(SolidBrush b=new SolidBrush(C("FAF0A8"))) g.FillPath(b,p);
                Stroke(g,p,C("101014"),4);
            }
            Fill(g,C("FFF8BA"),10,16,39,8,55,21,26,29);
            Fill(g,C("E8D477"),26,29,55,21,51,50,24,56);
            Fill(g,C("FAF0A8"),10,17,26,29,24,55,11,43);
            Line(g,C("101014"),1.7f,10,16,26,29,24,56);
            Line(g,C("101014"),1.7f,26,29,55,21);
            // The collar follows the top plane into the front face.
            Fill(g,C("101014"),22,12,29,10,41,25,39,53,31,55,33,27);
            Fill(g,C("E63535"),24,13,28,12,39,26,37,52,33,53,35,27);
            using(SolidBrush b=new SolidBrush(C("38E89A",30))) g.FillEllipse(b,19,24,31,32);
            using(SolidBrush b=new SolidBrush(C("101014"))) g.FillEllipse(b,20,24,29,31);
            using(SolidBrush b=new SolidBrush(C("E63535"))) g.FillEllipse(b,22,26,25,27);
            using(SolidBrush b=new SolidBrush(C("101014"))) g.FillEllipse(b,24,28,21,23);
            using(SolidBrush b=new SolidBrush(C("38E89A"))) g.FillEllipse(b,25,29,19,21);
            using(Pen p=new Pen(C("9AFFCB",190),0.8f)) g.DrawArc(p,26,30,17,19,190,95);
            Line(g,C("FFFBD3"),1,12,18,20,16);
            Save(c,dir,"Codex_PeBoxIcon","background");
        }
    }
    static void Divider(string dir) {
        using(Canvas c=new Canvas(512,16)) {
            Graphics g=c.G;
            for(int x=4;x<494;x+=25) {
                int a=(int)Math.Round(170-43*(x/494.0));
                Fill(g,C("D9B854",a),x,13,x+10,3,x+22,3,x+12,13);
            }
            Save(c,dir,"Codex_HazardDivider","background");
        }
    }
    public static string[] Generate(string dir) {
        Report.Clear();
        Window(dir); Header(dir); Dossier(dir); Portrait(dir); Panel(dir); Section(dir);
        Tab(dir,false); Tab(dir,true); Tile(dir,false); Tile(dir,true); Locked(dir);
        Lock(dir); PeBox(dir); Divider(dir);
        if(Report.Count!=14) throw new InvalidDataException("Expected exactly 14 textures.");
        return Report.ToArray();
    }
}
'@
}

$verification = [LCCodexArtwork]::Generate($assetDirectory)
Write-Output 'Verified 14 RGBA PNG textures (2x supersampling, high-quality bicubic, Format32bppArgb):'
$verification | ForEach-Object { Write-Output $_ }
